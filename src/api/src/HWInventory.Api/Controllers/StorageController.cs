using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Route("api/settings/storage")]
[Authorize(Policy = AuthorizationPolicies.SettingsManage)]
public class StorageController : ApiControllerBase
{
    private readonly IStorageConnectorStore _store;

    public StorageController(IAppDbContext dbContext, IStorageConnectorStore store)
        : base(dbContext)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<StorageConnectorModel?>> GetAsync(CancellationToken cancellationToken)
    {
        var model = await _store.GetAsync(cancellationToken);
        return Ok(model);
    }

    [HttpPut]
    public async Task<ActionResult<StorageConnectorModel>> SaveAsync([FromBody] StorageConnectorUpdate update, CancellationToken cancellationToken)
    {
        var before = await _store.GetAsync(cancellationToken);
        var result = await _store.SaveAsync(update, cancellationToken);

        AddAuditLog(
            entityType: "Settings.Storage",
            entityId: result.Id,
            action: "Update",
            summary: "Storage connector configuration updated",
            changedFields: new
            {
                result.Alias,
                result.Enabled,
                result.Type,
                result.Path,
                result.Endpoint,
                result.Bucket,
                result.Folder,
                result.Region,
                result.Username,
                result.Domain,
                result.PublicUrlBase,
                result.RetentionDays,
                result.UseSsl,
                PreviousAlias = before?.Alias,
                PreviousEnabled = before?.Enabled,
                PreviousType = before?.Type,
                PreviousPath = before?.Path,
                PreviousEndpoint = before?.Endpoint,
                PreviousBucket = before?.Bucket,
                PreviousFolder = before?.Folder,
                PreviousRegion = before?.Region,
                PreviousUsername = before?.Username,
                PreviousDomain = before?.Domain,
                PreviousPublicUrlBase = before?.PublicUrlBase,
                PreviousRetentionDays = before?.RetentionDays,
                PreviousUseSsl = before?.UseSsl
            });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("test")]
    public async Task<ActionResult<StorageConnectorTestResult>> TestAsync(CancellationToken cancellationToken)
    {
        var result = await _store.TestAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        AddAuditLog(
            entityType: "Settings.Storage",
            entityId: Guid.Empty,
            action: "Test",
            summary: "Storage connector test executed",
            changedFields: new { Result = result.Message });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }
}
