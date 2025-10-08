using System.Linq;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Application.Common;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using HWInventory.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

public class ServersController : ApiControllerBase
{
    public ServersController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ServersRead)]
    public async Task<ActionResult<PagedResult<Server>>> GetServersAsync([FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var scope = await ResolveDataScopeAsync(cancellationToken);

        var query = DbContext.Servers.AsNoTracking().OrderBy(x => x.Name);
        if (scope.HasLocationRestrictions)
        {
            var allowedLocations = scope.LocationIds.ToArray();
            query = query.Where(x => allowedLocations.Contains(x.LocationId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = total.ToString();
        var links = GeneratePaginationLinksStatic("servers", page, size, total);
        if (!string.IsNullOrEmpty(links))
        {
            Response.Headers["Link"] = links;
        }

        return Ok(new PagedResult<Server>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ServersRead)]
    public async Task<ActionResult<Server>> GetServerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await DbContext.Servers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(server.LocationId))
        {
            return Forbid();
        }

        return Ok(server);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ServersManage)]
    public async Task<ActionResult<Server>> CreateServerAsync([FromBody] ServerRequestDto request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        var server = MapServer(request);
        server.CreatedAtUtc = DateTime.UtcNow;
        server.CreatedBy = User.Identity?.Name ?? "system";

        DbContext.Servers.Add(server);
        await DbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetServerAsync), new { id = server.Id }, server);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ServersManage)]
    public async Task<ActionResult<Server>> UpdateServerAsync(Guid id, [FromBody] ServerRequestDto request, CancellationToken cancellationToken = default)
    {
        var server = await DbContext.Servers.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(server.LocationId))
        {
            return Forbid();
        }

        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        UpdateServer(server, request);
        server.ModifiedAtUtc = DateTime.UtcNow;
        server.ModifiedBy = User.Identity?.Name ?? "system";

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(server);
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = AuthorizationPolicies.ServersManage)]
    public async Task<IActionResult> RetireServerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await DbContext.Servers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(server.LocationId))
        {
            return Forbid();
        }

        server.Status = EntityStatus.Retired;
        server.ModifiedAtUtc = DateTime.UtcNow;
        server.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = AuthorizationPolicies.ServersManage)]
    public async Task<IActionResult> RestoreServerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await DbContext.Servers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(server.LocationId))
        {
            return Forbid();
        }

        server.Status = EntityStatus.Active;
        server.ModifiedAtUtc = DateTime.UtcNow;
        server.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ServersManage)]
    public async Task<IActionResult> DeleteServerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await DbContext.Servers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (server is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(server.LocationId))
        {
            return Forbid();
        }

        DbContext.Servers.Remove(server);
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    internal static string GeneratePaginationLinksStatic(string route, int page, int size, int total)
    {
        var links = new List<string>();
        var lastPage = (int)Math.Ceiling(total / (double)size);
        if (page > 1)
        {
            links.Add($"</api/{route}?page=1&size={size}>; rel=\"first\"");
            links.Add($"</api/{route}?page={page - 1}&size={size}>; rel=\"prev\"");
        }
        if (page < lastPage)
        {
            links.Add($"</api/{route}?page={page + 1}&size={size}>; rel=\"next\"");
            links.Add($"</api/{route}?page={lastPage}&size={size}>; rel=\"last\"");
        }
        return string.Join(", ", links);
    }

    private static Server MapServer(ServerRequestDto request)
    {
        return new Server
        {
            Name = request.Name,
            InventoryNumber = request.InventoryNumber,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            EnvironmentId = request.EnvironmentId,
            WsusPriorityId = request.WsusPriorityId,
            OperatingSystemId = request.OperatingSystemId,
            ServerRoleId = request.ServerRoleId,
            PrimaryAdministratorId = request.PrimaryAdministratorId,
            SecondaryAdministratorId = request.SecondaryAdministratorId,
            LocationId = request.LocationId,
            RackPosition = request.RackPosition,
            PurchasedAt = request.PurchasedAt,
            SupportUntil = request.SupportUntil,
            Notes = request.Notes,
            NetworkAssignments = request.NetworkAssignments
                .Select(x => new NetworkEndpoint { Label = x.Label, VlanId = x.VlanId, IpAddress = x.IpAddress })
                .ToList()
        };
    }

    private static void UpdateServer(Server server, ServerRequestDto request)
    {
        server.Name = request.Name;
        server.InventoryNumber = request.InventoryNumber;
        server.Manufacturer = request.Manufacturer;
        server.Model = request.Model;
        server.EnvironmentId = request.EnvironmentId;
        server.WsusPriorityId = request.WsusPriorityId;
        server.OperatingSystemId = request.OperatingSystemId;
        server.ServerRoleId = request.ServerRoleId;
        server.PrimaryAdministratorId = request.PrimaryAdministratorId;
        server.SecondaryAdministratorId = request.SecondaryAdministratorId;
        server.LocationId = request.LocationId;
        server.RackPosition = request.RackPosition;
        server.PurchasedAt = request.PurchasedAt;
        server.SupportUntil = request.SupportUntil;
        server.Notes = request.Notes;

        server.NetworkAssignments.Clear();
        foreach (var assignment in request.NetworkAssignments)
        {
            server.NetworkAssignments.Add(new NetworkEndpoint
            {
                Label = assignment.Label,
                VlanId = assignment.VlanId,
                IpAddress = assignment.IpAddress
            });
        }
    }
}
