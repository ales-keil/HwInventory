using System.Collections.Generic;
using System.Security.Claims;
using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ApiControllerBase(IAppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected IAppDbContext DbContext { get; }

    protected record DataScopeContext(bool IsScoped, IReadOnlyCollection<Guid> LocationIds, IReadOnlyCollection<string> DepartmentKeys)
    {
        public static DataScopeContext Unrestricted { get; } = new(false, Array.Empty<Guid>(), Array.Empty<string>());

        public bool HasLocationRestrictions => IsScoped && LocationIds.Count > 0;

        public bool HasDepartmentRestrictions => IsScoped && DepartmentKeys.Count > 0;
    }

    protected async Task<DataScopeContext> ResolveDataScopeAsync(CancellationToken cancellationToken = default)
    {
        if (User.IsInRole(SystemRoleNames.SuperAdmin))
        {
            return DataScopeContext.Unrestricted;
        }

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return DataScopeContext.Unrestricted;
        }

        var scopeRows = await DbContext.DataScopes
            .Where(x => x.AppUserId == userId)
            .Select(x => new { x.ScopeType, x.LocationId, x.DepartmentKey })
            .ToListAsync(cancellationToken);

        if (scopeRows.Count == 0)
        {
            return DataScopeContext.Unrestricted;
        }

        var locationIds = new HashSet<Guid>();
        var departmentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in scopeRows)
        {
            if (row.ScopeType == DataScopeTypes.Location && row.LocationId.HasValue)
            {
                locationIds.Add(row.LocationId.Value);
            }

            if (row.ScopeType == DataScopeTypes.Department && !string.IsNullOrWhiteSpace(row.DepartmentKey))
            {
                departmentKeys.Add(row.DepartmentKey);
            }
        }

        if (locationIds.Count == 0 && departmentKeys.Count == 0)
        {
            return DataScopeContext.Unrestricted;
        }

        return new DataScopeContext(true, locationIds, departmentKeys);
    }
}
