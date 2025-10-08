using System.Linq;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Route("api/dictionaries")]
[Authorize(Policy = AuthorizationPolicies.DictionariesManage)]
public class DictionariesController : ApiControllerBase
{
    public DictionariesController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DictionaryEntry>>> GetAsync([FromQuery] string? type)
    {
        var query = DbContext.DictionaryEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => x.DictType == type);
        }

        var result = await query.OrderBy(x => x.DictType).ThenBy(x => x.Order).ThenBy(x => x.Value).ToListAsync();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DictionaryEntry>> GetByIdAsync(Guid id)
    {
        var entry = await DbContext.DictionaryEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpPost]
    public async Task<ActionResult<DictionaryEntry>> CreateAsync([FromBody] DictionaryEntryRequestDto request)
    {
        var entry = new DictionaryEntry
        {
            DictType = request.DictType,
            Key = request.Key,
            Value = request.Value,
            Description = request.Description,
            Order = request.Order,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        };

        DbContext.DictionaryEntries.Add(entry);
        await DbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetByIdAsync), new { id = entry.Id }, entry);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DictionaryEntry>> UpdateAsync(Guid id, [FromBody] DictionaryEntryRequestDto request)
    {
        var entry = await DbContext.DictionaryEntries.FirstOrDefaultAsync(x => x.Id == id);
        if (entry is null)
        {
            return NotFound();
        }

        entry.DictType = request.DictType;
        entry.Key = request.Key;
        entry.Value = request.Value;
        entry.Description = request.Description;
        entry.Order = request.Order;
        entry.ModifiedAtUtc = DateTime.UtcNow;
        entry.ModifiedBy = User.Identity?.Name ?? "system";

        await DbContext.SaveChangesAsync();
        return Ok(entry);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        var entry = await DbContext.DictionaryEntries.FirstOrDefaultAsync(x => x.Id == id);
        if (entry is null)
        {
            return NotFound();
        }

        DbContext.DictionaryEntries.Remove(entry);
        await DbContext.SaveChangesAsync();
        return NoContent();
    }
}
