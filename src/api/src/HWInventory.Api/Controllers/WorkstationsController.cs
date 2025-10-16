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

[Route("api/workstations")]
public class WorkstationsController : ApiControllerBase
{
    public WorkstationsController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsRead)]
    public async Task<ActionResult<PagedResult<Workstation>>> GetAsync(
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? search = null,
        [FromQuery] EntityStatus? status = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? operatingSystemId = null,
        [FromQuery] Guid? workstationTypeId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var scope = await ResolveDataScopeAsync(cancellationToken);

        var query = DbContext.Workstations.AsNoTracking().AsQueryable();
        if (scope.HasLocationRestrictions)
        {
            var allowedLocations = scope.LocationIds.ToArray();
            query = query.Where(x => allowedLocations.Contains(x.LocationId));
        }

        if (scope.HasDepartmentRestrictions)
        {
            var allowedDepartments = scope.DepartmentKeys.ToArray();
            query = query.Where(x => x.OwnerDepartment != null && allowedDepartments.Contains(x.OwnerDepartment));
        }

        if (locationId.HasValue)
        {
            query = query.Where(x => x.LocationId == locationId.Value);
        }

        if (operatingSystemId.HasValue)
        {
            query = query.Where(x => x.OperatingSystemId == operatingSystemId.Value);
        }

        if (workstationTypeId.HasValue)
        {
            query = query.Where(x => x.WorkstationTypeId == workstationTypeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Name, term) ||
                EF.Functions.Like(x.InventoryNumber, term) ||
                EF.Functions.Like(x.OwnerDisplayName ?? string.Empty, term) ||
                EF.Functions.Like(x.OwnerDepartment ?? string.Empty, term));
        }

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = total.ToString();
        var links = ServersController.GeneratePaginationLinksStatic("workstations", page, size, total);
        if (!string.IsNullOrEmpty(links))
        {
            Response.Headers["Link"] = links;
        }

        return Ok(new PagedResult<Workstation>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsRead)]
    public async Task<ActionResult<Workstation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(entity);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<ActionResult<Workstation>> CreateAsync([FromBody] WorkstationRequestDto request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(request.OwnerDepartment) && !scope.DepartmentKeys.Contains(request.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var entity = Map(request);
        entity.CreatedAtUtc = DateTime.UtcNow;
        entity.CreatedBy = User.Identity?.Name ?? "system";

        DbContext.Workstations.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<ActionResult<Workstation>> UpdateAsync(Guid id, [FromBody] WorkstationRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(request.OwnerDepartment) && !scope.DepartmentKeys.Contains(request.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        Update(entity, request);
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity);
    }

    [HttpPost("{id:guid}/handover")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<ActionResult> HandoverAsync(Guid id, [FromBody] HandoverRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.NewLocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(request.NewOwnerDepartment) && !scope.DepartmentKeys.Contains(request.NewOwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        entity.OwnerId = request.NewOwnerId;
        entity.OwnerDisplayName = request.NewOwnerDisplayName;
        entity.OwnerDepartment = request.NewOwnerDepartment;
        entity.LocationId = request.NewLocationId;
        entity.LocationNote = request.NewLocationNote;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RetireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        entity.Status = EntityStatus.Retired;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("batch/retire")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RetireBatchAsync([FromBody] BatchActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest("No identifiers supplied.");
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        var entities = await DbContext.Workstations.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var entity in entities)
        {
            if (!IsWithinScope(scope, entity))
            {
                continue;
            }

            entity.Status = EntityStatus.Retired;
            entity.ModifiedAtUtc = DateTime.UtcNow;
            entity.ModifiedBy = User.Identity?.Name ?? "system";
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        entity.Status = EntityStatus.Active;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("batch/restore")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RestoreBatchAsync([FromBody] BatchActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest("No identifiers supplied.");
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        var entities = await DbContext.Workstations.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var entity in entities)
        {
            if (!IsWithinScope(scope, entity))
            {
                continue;
            }

            entity.Status = EntityStatus.Active;
            entity.ModifiedAtUtc = DateTime.UtcNow;
            entity.ModifiedBy = User.Identity?.Name ?? "system";
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        DbContext.Workstations.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("batch/delete")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> DeleteBatchAsync([FromBody] BatchActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest("No identifiers supplied.");
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        var entities = await DbContext.Workstations.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
        DbContext.Workstations.RemoveRange(entities.Where(entity => IsWithinScope(scope, entity)));

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool IsWithinScope(UserDataScope scope, Workstation entity)
    {
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return false;
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static Workstation Map(WorkstationRequestDto request)
    {
        return new Workstation
        {
            Name = request.Name,
            InventoryNumber = request.InventoryNumber,
            OperatingSystemId = request.OperatingSystemId,
            WorkstationTypeId = request.WorkstationTypeId,
            OwnerId = request.OwnerId,
            OwnerDisplayName = request.OwnerDisplayName,
            OwnerDepartment = request.OwnerDepartment,
            LocationId = request.LocationId,
            LocationNote = request.LocationNote,
            Cpu = request.Cpu,
            Ram = request.Ram,
            Storage = request.Storage,
            MacAddress = request.MacAddress,
            PurchasedAt = request.PurchasedAt,
            SupportUntil = request.SupportUntil,
            PrimaryAdministratorId = request.PrimaryAdministratorId,
            SecondaryAdministratorId = request.SecondaryAdministratorId,
            Notes = request.Notes,
            NetworkAssignments = request.NetworkAssignments.Select(x => new NetworkEndpoint
            {
                Label = x.Label,
                VlanId = x.VlanId,
                IpAddress = x.IpAddress
            }).ToList()
        };
    }

    private static void Update(Workstation entity, WorkstationRequestDto request)
    {
        entity.Name = request.Name;
        entity.InventoryNumber = request.InventoryNumber;
        entity.OperatingSystemId = request.OperatingSystemId;
        entity.WorkstationTypeId = request.WorkstationTypeId;
        entity.OwnerId = request.OwnerId;
        entity.OwnerDisplayName = request.OwnerDisplayName;
        entity.OwnerDepartment = request.OwnerDepartment;
        entity.LocationId = request.LocationId;
        entity.LocationNote = request.LocationNote;
        entity.Cpu = request.Cpu;
        entity.Ram = request.Ram;
        entity.Storage = request.Storage;
        entity.MacAddress = request.MacAddress;
        entity.PurchasedAt = request.PurchasedAt;
        entity.SupportUntil = request.SupportUntil;
        entity.PrimaryAdministratorId = request.PrimaryAdministratorId;
        entity.SecondaryAdministratorId = request.SecondaryAdministratorId;
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

    public record HandoverRequest(
        Guid? NewOwnerId,
        string? NewOwnerDisplayName,
        string? NewOwnerDepartment,
        Guid NewLocationId,
        string? NewLocationNote,
        Guid? NewAdminId,
        IReadOnlyCollection<string>? Emails,
        string? Comment);
}
