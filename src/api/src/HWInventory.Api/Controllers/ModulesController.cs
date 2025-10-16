using System.Linq;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Route("api/modules")]
[Authorize(Policy = AuthorizationPolicies.ModulesManage)]
public class ModulesController : ApiControllerBase
{
    public ModulesController(IAppDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<FeatureModule>>> GetAsync()
    {
        var modules = await DbContext.FeatureModules.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        return Ok(modules);
    }

    [HttpPost("{key}/toggle")]
    public async Task<IActionResult> ToggleAsync(string key, [FromQuery] bool? enabled, [FromBody] FeatureModuleToggleRequest? body)
    {
        var module = await DbContext.FeatureModules.FirstOrDefaultAsync(x => x.Key == key);
        if (module is null)
        {
            return NotFound();
        }

        var newState = enabled ?? body?.Enabled ?? !module.Enabled;
        module.Enabled = newState;
        module.ModifiedAtUtc = DateTime.UtcNow;
        module.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return Ok(module);
    }
}
