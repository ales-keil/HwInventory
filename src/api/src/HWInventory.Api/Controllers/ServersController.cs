using System.Linq;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Application.Common;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

public class ServersController : ApiControllerBase
{
    public ServersController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<Server>>> GetServersAsync([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var query = DbContext.Servers.AsNoTracking().OrderBy(x => x.Name);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();

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
    public async Task<ActionResult<Server>> GetServerAsync(Guid id)
    {
        var server = await DbContext.Servers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return server is null ? NotFound() : Ok(server);
    }

    [HttpPost]
    public async Task<ActionResult<Server>> CreateServerAsync([FromBody] ServerRequestDto request)
    {
        var server = MapServer(request);
        server.CreatedAtUtc = DateTime.UtcNow;
        server.CreatedBy = User.Identity?.Name ?? "system";

        DbContext.Servers.Add(server);
        await DbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetServerAsync), new { id = server.Id }, server);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Server>> UpdateServerAsync(Guid id, [FromBody] ServerRequestDto request)
    {
        var server = await DbContext.Servers.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id);
        if (server is null)
        {
            return NotFound();
        }

        UpdateServer(server, request);
        server.ModifiedAtUtc = DateTime.UtcNow;
        server.ModifiedBy = User.Identity?.Name ?? "system";

        await DbContext.SaveChangesAsync();
        return Ok(server);
    }

    [HttpPost("{id:guid}/retire")]
    public async Task<IActionResult> RetireServerAsync(Guid id)
    {
        var server = await DbContext.Servers.FirstOrDefaultAsync(x => x.Id == id);
        if (server is null)
        {
            return NotFound();
        }

        server.Status = EntityStatus.Retired;
        server.ModifiedAtUtc = DateTime.UtcNow;
        server.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreServerAsync(Guid id)
    {
        var server = await DbContext.Servers.FirstOrDefaultAsync(x => x.Id == id);
        if (server is null)
        {
            return NotFound();
        }

        server.Status = EntityStatus.Active;
        server.ModifiedAtUtc = DateTime.UtcNow;
        server.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteServerAsync(Guid id)
    {
        var server = await DbContext.Servers.FirstOrDefaultAsync(x => x.Id == id);
        if (server is null)
        {
            return NotFound();
        }

        DbContext.Servers.Remove(server);
        await DbContext.SaveChangesAsync();
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
