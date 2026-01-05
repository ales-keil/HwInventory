using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.UpdatesManage)]
[Route("api/updates")]
public class UpdatesController : ApiControllerBase
{
    private readonly IUpdateService _updateService;
    private readonly IUpdateConfigurationStore _configurationStore;

    public UpdatesController(IAppDbContext dbContext, IUpdateService updateService, IUpdateConfigurationStore configurationStore)
        : base(dbContext)
    {
        _updateService = updateService;
        _configurationStore = configurationStore;
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<UpdatePackageResponseDto>>> GetHistoryAsync([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken cancellationToken = default)
    {
        size = Math.Clamp(size, 1, 100);
        var history = await _updateService.ListHistoryAsync(page, size, cancellationToken);
        Response.Headers["X-Total-Count"] = history.Count.ToString();
        return Ok(history.Select(Map));
    }

    [HttpGet("configuration")]
    public async Task<ActionResult<UpdateConfigurationResponseDto>> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationStore.GetAsync(cancellationToken);
        return Ok(Map(configuration));
    }

    [HttpPut("configuration")]
    public async Task<ActionResult<UpdateConfigurationResponseDto>> SaveConfigurationAsync([FromBody] UpdateConfigurationRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeploymentRootPath))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chybí cílová složka",
                Detail = "Zadejte cestu, do které se má aktualizace nasazovat.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var result = await _configurationStore.SaveAsync(new UpdateDeploymentConfigurationUpdate(
            request.DeploymentRootPath,
            string.IsNullOrWhiteSpace(request.WebRootPath) ? null : request.WebRootPath,
            request.UseAppOfflineFile,
            request.RunMigrations,
            string.IsNullOrWhiteSpace(request.PostDeploymentScript) ? null : request.PostDeploymentScript),
            cancellationToken);

        AddAuditLog("Maintenance.Updates", Guid.Empty, "Configuration", "Aktualizace nastavení nasazení", result);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UpdatePackageResponseDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var package = await _updateService.GetAsync(id, cancellationToken);
        if (package is null)
        {
            return NotFound();
        }

        return Ok(Map(package));
    }

    [HttpGet("{id:guid}/log")]
    public async Task<ActionResult> DownloadLogAsync(Guid id, CancellationToken cancellationToken)
    {
        var log = await _updateService.GetLogAsync(id, cancellationToken);
        if (log is null)
        {
            return NotFound();
        }

        return File(log, "text/plain", $"update-{id:N}.log");
    }

    [HttpPost]
    [RequestSizeLimit(1024L * 1024 * 512)]
    public async Task<ActionResult<UpdatePackageResponseDto>> UploadAsync([FromForm] UpdatePackageUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.Package is null || request.Package.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chybí soubor",
                Detail = "Je nutné nahrát ZIP nebo PKG soubor s aktualizací.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!request.ConfirmedBackupAvailable && !request.CreateBackupBeforeInstall)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Potvrzení zálohy",
                Detail = "Před spuštěním aktualizace musíte potvrdit, že existuje záloha, nebo zapnout volbu vytvoření zálohy.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var allowedExtensions = new[] { ".zip", ".pkg" };
        if (!allowedExtensions.Contains(Path.GetExtension(request.Package.FileName ?? string.Empty), StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Nepodporovaný formát",
                Detail = "Aktualizační balíček musí být ve formátu .zip nebo .pkg.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var memoryStream = new MemoryStream();
        await request.Package.CopyToAsync(memoryStream, cancellationToken);
        var contentBase64 = Convert.ToBase64String(memoryStream.ToArray());

        var storagePath = Path.Combine(AppContext.BaseDirectory, "updates");
        Directory.CreateDirectory(storagePath);

        var package = await _updateService.QueuePackageAsync(new UpdatePackageRequest(
            request.Package.FileName ?? "update.zip",
            storagePath,
            contentBase64,
            request.Version,
            request.PreserveDatabaseConfiguration,
            request.CreateBackupBeforeInstall,
            string.IsNullOrWhiteSpace(request.BackupStoragePath) ? null : Path.GetFullPath(request.BackupStoragePath),
            request.PerformIntegrityCheck,
            request.ConfirmedBackupAvailable,
            request.Notes),
            cancellationToken);

        AddAuditLog("Maintenance.Updates", package.Id, "Upload", "Nahrán aktualizační balíček", new
        {
            package.Version,
            package.FileName,
            package.Status
        });

        await DbContext.SaveChangesAsync(cancellationToken);

        return AcceptedAtAction(nameof(GetAsync), new { id = package.Id }, Map(package));
    }

    private static UpdatePackageResponseDto Map(UpdatePackageModel model) => new(
        model.Id,
        model.Version,
        model.FileName,
        model.Status,
        model.Sha256,
        model.PreserveDatabaseConfiguration,
        model.CreateBackupBeforeInstall,
        model.PerformIntegrityCheck,
        model.ConfirmedBackupAvailable,
        model.Notes,
        model.CreatedAtUtc,
        model.CompletedAtUtc,
        model.CreatedBy,
        model.FailureReason,
        model.ManifestJson,
        model.LogPath);

    private static UpdateConfigurationResponseDto Map(UpdateDeploymentConfigurationModel model) => new(
        model.DeploymentRootPath,
        model.WebRootPath,
        model.UseAppOfflineFile,
        model.RunMigrations,
        model.PostDeploymentScript);
}
