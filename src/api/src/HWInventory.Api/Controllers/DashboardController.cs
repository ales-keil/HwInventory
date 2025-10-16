using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Route("api/dashboard")]
[Authorize(Policy = AuthorizationPolicies.DashboardView)]
public class DashboardController : ApiControllerBase
{
    private readonly ISecurityMetricsProvider _securityMetricsProvider;

    public DashboardController(IAppDbContext dbContext, ISecurityMetricsProvider securityMetricsProvider) : base(dbContext)
    {
        _securityMetricsProvider = securityMetricsProvider;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var servers = await DbContext.Servers.CountAsync(cancellationToken);
        var network = await DbContext.NetworkDevices.CountAsync(cancellationToken);
        var workstations = await DbContext.Workstations.CountAsync(cancellationToken);
        var latestChanges = await DbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.PerformedAtUtc)
            .Take(10)
            .Select(x => new AuditEntryDto(x.EntityType, x.Action, x.PerformedBy, x.PerformedAtUtc))
            .ToListAsync(cancellationToken);
        var securitySnapshot = await _securityMetricsProvider.GetSnapshotAsync(cancellationToken);

        var security = new SecurityMetricsDto(
            securitySnapshot.TotalUsers,
            securitySnapshot.ActiveUsers,
            securitySnapshot.TotpEnabled,
            securitySnapshot.TotpRequired,
            securitySnapshot.LockedOut,
            securitySnapshot.PendingPasswordResets,
            securitySnapshot.ActiveSessions,
            securitySnapshot.GeneratedAtUtc);

        return Ok(new DashboardSummaryResponse(servers, network, workstations, latestChanges, security));
    }

    public record DashboardSummaryResponse(
        int Servers,
        int NetworkDevices,
        int Workstations,
        IReadOnlyCollection<AuditEntryDto> LatestChanges,
        SecurityMetricsDto Security);

    public record AuditEntryDto(string EntityType, string Action, string PerformedBy, DateTime PerformedAtUtc);

    public record SecurityMetricsDto(
        int TotalUsers,
        int ActiveUsers,
        int TotpEnabled,
        int TotpRequired,
        int LockedOut,
        int PendingPasswordResets,
        int ActiveSessions,
        DateTime GeneratedAtUtc);
}
