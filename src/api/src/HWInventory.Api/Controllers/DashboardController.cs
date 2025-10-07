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
    public DashboardController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummaryAsync()
    {
        var servers = await DbContext.Servers.CountAsync();
        var network = await DbContext.NetworkDevices.CountAsync();
        var workstations = await DbContext.Workstations.CountAsync();
        var latestChanges = await DbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(x => x.PerformedAtUtc)
            .Take(10)
            .Select(x => new AuditEntryDto(x.EntityType, x.Action, x.PerformedBy, x.PerformedAtUtc))
            .ToListAsync();

        return Ok(new DashboardSummaryResponse(servers, network, workstations, latestChanges));
    }

    public record DashboardSummaryResponse(int Servers, int NetworkDevices, int Workstations, IReadOnlyCollection<AuditEntryDto> LatestChanges);

    public record AuditEntryDto(string EntityType, string Action, string PerformedBy, DateTime PerformedAtUtc);
}
