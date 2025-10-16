using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ImportExportManage)]
[Route("api/exports")]
public class ExportsController : ApiControllerBase
{
    private readonly IExportService _exportService;

    public ExportsController(IAppDbContext dbContext, IExportService exportService)
        : base(dbContext)
    {
        _exportService = exportService;
    }

    [HttpGet("history")]
    public async Task<ActionResult> GetHistoryAsync([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken cancellationToken = default)
    {
        size = Math.Clamp(size, 1, 100);
        var history = await _exportService.ListHistoryAsync(page, size, cancellationToken);
        return Ok(history.Select(Map));
    }

    [HttpPost]
    public async Task<ActionResult<ExportResponseDto>> QueueExportAsync([FromBody] ExportRequestDto request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ExportScope>(request.Scope, true, out var scope))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatný rozsah exportu",
                Detail = "Zadaný rozsah exportu není podporován.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!Enum.TryParse<ExportFormat>(request.Format, true, out var format))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatný formát",
                Detail = "Formát exportu musí být CSV nebo XLSX.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chybí cílová cesta",
                Detail = "Je nutné zadat složku, do které se export uloží.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var job = await _exportService.QueueExportAsync(new ExportRequest(
            scope,
            format,
            request.StoragePath,
            request.FilterJson,
            request.SendEmail,
            request.EmailRecipients),
            cancellationToken);

        AddAuditLog("Exports", job.Id, "Create", "Zařazení exportu", job);
        await DbContext.SaveChangesAsync(cancellationToken);

        return AcceptedAtAction(nameof(GetExportAsync), new { id = job.Id }, Map(job));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExportResponseDto>> GetExportAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await _exportService.GetAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(Map(job));
    }

    [HttpGet("{id:guid}/artifact")]
    public async Task<ActionResult> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await _exportService.GetAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(job.ArtifactPath) || !System.IO.File.Exists(job.ArtifactPath))
        {
            return NotFound(new ProblemDetails
            {
                Title = "Artefakt nenalezen",
                Detail = "Soubor exportu není k dispozici nebo již expiroval.",
                Status = StatusCodes.Status404NotFound
            });
        }

        var fileName = Path.GetFileName(job.ArtifactPath);
        var mimeType = job.Format.Equals("xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";

        var stream = new FileStream(job.ArtifactPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, mimeType, fileName);
    }

    private static ExportResponseDto Map(ExportJobModel model)
    {
        return new ExportResponseDto
        {
            Id = model.Id,
            Scope = model.Scope,
            Format = model.Format,
            StoragePath = model.StoragePath,
            FileName = model.FileName,
            Status = model.Status,
            FileSizeBytes = model.FileSizeBytes,
            CreatedAtUtc = model.CreatedAtUtc,
            CompletedAtUtc = model.CompletedAtUtc,
            ExpiresAtUtc = model.ExpiresAtUtc,
            CreatedBy = model.CreatedBy,
            FailureReason = model.FailureReason,
            SendEmail = model.SendEmail,
            EmailRecipients = model.EmailRecipients,
            ArtifactPath = model.ArtifactPath
        };
    }
}
