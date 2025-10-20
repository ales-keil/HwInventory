using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Route("api/audit")]
[Authorize(Policy = AuthorizationPolicies.AuditRead)]
public class AuditController : ApiControllerBase
{
    public AuditController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLog>>> GetAsync([FromQuery] string? entityType, [FromQuery] string? user, [FromQuery] string? action)
    {
        var query = DbContext.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(x => x.EntityType == entityType);
        }
        if (!string.IsNullOrWhiteSpace(user))
        {
            query = query.Where(x => x.PerformedBy == user);
        }
        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        var items = await query.OrderByDescending(x => x.PerformedAtUtc).Take(500).ToListAsync();
        return Ok(items);
    }
}
