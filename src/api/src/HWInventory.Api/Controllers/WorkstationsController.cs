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

[Route("api/workstations")]
public class WorkstationsController : ApiControllerBase
{
    public WorkstationsController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<Workstation>>> GetAsync([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var query = DbContext.Workstations.AsNoTracking().OrderBy(x => x.Name);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync();

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
    public async Task<ActionResult<Workstation>> GetByIdAsync(Guid id)
    {
        var entity = await DbContext.Workstations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? NotFound() : Ok(entity);
    }

    [HttpPost]
    public async Task<ActionResult<Workstation>> CreateAsync([FromBody] WorkstationRequestDto request)
    {
        var entity = Map(request);
        entity.CreatedAtUtc = DateTime.UtcNow;
        entity.CreatedBy = User.Identity?.Name ?? "system";

        DbContext.Workstations.Add(entity);
        await DbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByIdAsync), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Workstation>> UpdateAsync(Guid id, [FromBody] WorkstationRequestDto request)
    {
        var entity = await DbContext.Workstations.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        Update(entity, request);
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return Ok(entity);
    }

    [HttpPost("{id:guid}/handover")]
    public async Task<ActionResult> HandoverAsync(Guid id, [FromBody] HandoverRequest request)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.OwnerId = request.NewOwnerId;
        entity.OwnerDisplayName = request.NewOwnerDisplayName;
        entity.OwnerDepartment = request.NewOwnerDepartment;
        entity.LocationId = request.NewLocationId;
        entity.LocationNote = request.NewLocationNote;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";

        await DbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/retire")]
    public async Task<IActionResult> RetireAsync(Guid id)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Status = EntityStatus.Retired;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreAsync(Guid id)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Status = EntityStatus.Active;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id);
        if (entity is null)
        {
            return NotFound();
        }

        DbContext.Workstations.Remove(entity);
        await DbContext.SaveChangesAsync();
        return NoContent();
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
