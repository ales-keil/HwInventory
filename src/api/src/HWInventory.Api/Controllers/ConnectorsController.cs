using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.SettingsManage)]
public class ConnectorsController : ApiControllerBase
{
    private readonly IConnectorCatalogService _catalogService;

    public ConnectorsController(IAppDbContext dbContext, IConnectorCatalogService catalogService)
        : base(dbContext)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ConnectorSummaryResponse>>> GetAsync(CancellationToken cancellationToken)
    {
        var connectors = await _catalogService.GetAsync(cancellationToken);
        var response = connectors.Select(Map).ToList();
        return Ok(response);
    }

    [HttpPost("{connectorId:guid}/toggle")]
    public async Task<ActionResult<ConnectorSummaryResponse>> ToggleAsync(Guid connectorId, [FromQuery] bool enabled, CancellationToken cancellationToken)
    {
        var summary = await _catalogService.ToggleAsync(connectorId, enabled, cancellationToken);
        if (summary is null)
        {
            return NotFound();
        }

        AddAuditLog(
            entityType: "ConnectorProfile",
            entityId: connectorId,
            action: "Update",
            summary: enabled ? "Connector enabled" : "Connector disabled",
            changedFields: new { enabled });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(summary));
    }

    [HttpPost("{connectorId:guid}/rotate-secret")]
    public async Task<ActionResult<ConnectorSummaryResponse>> RotateSecretsAsync(Guid connectorId, CancellationToken cancellationToken)
    {
        var summary = await _catalogService.RotateSecretsAsync(connectorId, cancellationToken);
        if (summary is null)
        {
            return NotFound();
        }

        AddAuditLog(
            entityType: "ConnectorProfile",
            entityId: connectorId,
            action: "Update",
            summary: "Connector secret rotation triggered",
            changedFields: new { rotated = true });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(summary));
    }

    [HttpPost("{connectorId:guid}/test")]
    public async Task<ActionResult<ConnectorTestResponse>> TestAsync(Guid connectorId, [FromBody] ConnectorTestRequestDto request, CancellationToken cancellationToken)
    {
        var outcome = await _catalogService.TestAsync(connectorId, new ConnectorTestRequest(request.Target), cancellationToken);
        if (outcome.Summary is not null)
        {
            AddAuditLog(
                entityType: "ConnectorProfile",
                entityId: connectorId,
                action: "Test",
                summary: "Connector test executed",
                changedFields: new
                {
                    outcome.Result.Success,
                    outcome.Result.Message,
                    request.Target
                });
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new ConnectorTestResponse(outcome.Result.Success, outcome.Result.Message, outcome.Summary is null ? null : Map(outcome.Summary)));
    }

    private static ConnectorSummaryResponse Map(ConnectorSummary summary)
    {
        return new ConnectorSummaryResponse(
            summary.Id,
            summary.Key,
            summary.Type,
            summary.Name,
            summary.Enabled,
            summary.HealthStatus,
            summary.LastTestedAtUtc,
            summary.HasSecret,
            summary.SupportsToggle,
            summary.SupportsTest,
            summary.SupportsRotateSecret,
            summary.RequiresConfiguration,
            summary.Description);
    }
}
