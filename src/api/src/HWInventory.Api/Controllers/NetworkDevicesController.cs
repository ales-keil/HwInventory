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

[Route("api/network-devices")]
public class NetworkDevicesController : ApiControllerBase
{
    public NetworkDevicesController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.NetworkRead)]
    public async Task<ActionResult<PagedResult<NetworkDevice>>> GetAsync([FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var scope = await ResolveDataScopeAsync(cancellationToken);

        var query = DbContext.NetworkDevices.AsNoTracking().OrderBy(x => x.Name);
        if (scope.HasLocationRestrictions)
        {
            var allowedLocations = scope.LocationIds.ToArray();
            query = query.Where(x => allowedLocations.Contains(x.LocationId));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = total.ToString();
        var links = ServersController.GeneratePaginationLinksStatic("network-devices", page, size, total);
        if (!string.IsNullOrEmpty(links))
        {
            Response.Headers["Link"] = links;
        }

        return Ok(new PagedResult<NetworkDevice>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.NetworkRead)]
    public async Task<ActionResult<NetworkDevice>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NetworkDevices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        return Ok(entity);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.NetworkManage)]
    public async Task<ActionResult<NetworkDevice>> CreateAsync([FromBody] NetworkDeviceRequestDto request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        var entity = Map(request);
        entity.CreatedAtUtc = DateTime.UtcNow;
        entity.CreatedBy = User.Identity?.Name ?? "system";

        DbContext.NetworkDevices.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.NetworkManage)]
    public async Task<ActionResult<NetworkDevice>> UpdateAsync(Guid id, [FromBody] NetworkDeviceRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NetworkDevices.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        Update(entity, request);
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity);
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = AuthorizationPolicies.NetworkManage)]
    public async Task<IActionResult> RetireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NetworkDevices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        entity.Status = EntityStatus.Retired;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = AuthorizationPolicies.NetworkManage)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NetworkDevices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        entity.Status = EntityStatus.Active;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.NetworkManage)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.NetworkDevices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        DbContext.NetworkDevices.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static NetworkDevice Map(NetworkDeviceRequestDto request)
    {
        return new NetworkDevice
        {
            Name = request.Name,
            InventoryNumber = request.InventoryNumber,
            DeviceTypeId = request.DeviceTypeId,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            LocationId = request.LocationId,
            RackPosition = request.RackPosition,
            PrimaryAdministratorId = request.PrimaryAdministratorId,
            SecondaryAdministratorId = request.SecondaryAdministratorId,
            SupportUntil = request.SupportUntil,
            Notes = request.Notes,
            NetworkAssignments = request.NetworkAssignments.Select(x => new NetworkEndpoint
            {
                Label = x.Label,
                VlanId = x.VlanId,
                IpAddress = x.IpAddress
            }).ToList()
        };
    }

    private static void Update(NetworkDevice entity, NetworkDeviceRequestDto request)
    {
        entity.Name = request.Name;
        entity.InventoryNumber = request.InventoryNumber;
        entity.DeviceTypeId = request.DeviceTypeId;
        entity.Manufacturer = request.Manufacturer;
        entity.Model = request.Model;
        entity.LocationId = request.LocationId;
        entity.RackPosition = request.RackPosition;
        entity.PrimaryAdministratorId = request.PrimaryAdministratorId;
        entity.SecondaryAdministratorId = request.SecondaryAdministratorId;
        entity.SupportUntil = request.SupportUntil;
        entity.Notes = request.Notes;

        entity.NetworkAssignments.Clear();
        foreach (var assignment in request.NetworkAssignments)
        {
            entity.NetworkAssignments.Add(new NetworkEndpoint
            {
                Label = assignment.Label,
                VlanId = assignment.VlanId,
                IpAddress = assignment.IpAddress
            });
        }
    }
}
