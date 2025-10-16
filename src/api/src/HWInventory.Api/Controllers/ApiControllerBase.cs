using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
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

    protected string ResolveUserDisplayName()
    {
        return User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "system";
    }

    protected void StampCreation(AuditableEntity entity)
    {
        var actor = ResolveUserDisplayName();
        entity.CreatedBy = actor;
        entity.ModifiedBy = actor;
    }

    protected void StampModification(AuditableEntity entity)
    {
        var actor = ResolveUserDisplayName();
        entity.ModifiedBy = actor;
    }

    private static readonly JsonSerializerOptions AuditSerializerOptions = new(JsonSerializerDefaults.Web);

    protected void AddAuditLog(string entityType, Guid entityId, string action, string? summary = null, object? changedFields = null)
    {
        var actor = ResolveUserDisplayName();
        var roles = string.Join(",", User.FindAll(ClaimTypes.Role).Select(r => r.Value));
        var userAgent = Request?.Headers["User-Agent"].ToString();
        var changedJson = changedFields is null ? null : JsonSerializer.Serialize(changedFields, AuditSerializerOptions);

        var log = new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            PerformedBy = actor,
            Roles = string.IsNullOrWhiteSpace(roles) ? null : roles,
            PerformedAtUtc = DateTime.UtcNow,
            ChangeSummary = summary,
            ChangedFieldsJson = changedJson,
            IpAddress = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent
        };

        DbContext.AuditLogs.Add(log);
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
