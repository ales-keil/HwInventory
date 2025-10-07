using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.SecurityManage)]
public class SecurityController : ApiControllerBase
{
    private readonly ILdapSyncService _ldapSyncService;
    private readonly ILdapConfigurationStore _ldapConfigurationStore;
    private readonly IExternalIdentityService _externalIdentityService;
    private readonly ISessionTracker _sessionTracker;
    private readonly ISsprConfigurationStore _ssprConfigurationStore;

    public SecurityController(
        IAppDbContext dbContext,
        ILdapSyncService ldapSyncService,
        ILdapConfigurationStore ldapConfigurationStore,
        IExternalIdentityService externalIdentityService,
        ISessionTracker sessionTracker,
        ISsprConfigurationStore ssprConfigurationStore) : base(dbContext)
    {
        _ldapSyncService = ldapSyncService;
        _ldapConfigurationStore = ldapConfigurationStore;
        _externalIdentityService = externalIdentityService;
        _sessionTracker = sessionTracker;
        _ssprConfigurationStore = ssprConfigurationStore;
    }

    [HttpGet("oidc")]
    public async Task<ActionResult<OidcConfiguration?>> GetOidcAsync(CancellationToken cancellationToken)
    {
        var configuration = await _externalIdentityService.GetConfigurationAsync(cancellationToken);
        return Ok(configuration);
    }

    [HttpPost("oidc")]
    public async Task<ActionResult> ConfigureOidcAsync([FromBody] OidcConfigurationRequest request, CancellationToken cancellationToken)
    {
        var previous = await _externalIdentityService.GetConfigurationAsync(cancellationToken);

        var configuration = new OidcConfiguration(
            request.Enabled,
            request.Authority,
            request.ClientId,
            request.ClientSecret,
            request.ResponseType,
            request.Scopes ?? Array.Empty<string>(),
            request.ClaimMappings ?? new Dictionary<string, string>(),
            request.UsePkce);

        await _externalIdentityService.ConfigureOidcAsync(configuration, cancellationToken);

        var previousSnapshot = previous is null ? null : CreateOidcSnapshot(previous);
        var currentSnapshot = CreateOidcSnapshot(configuration);
        if (previousSnapshot is null || !previousSnapshot.Equals(currentSnapshot))
        {
            var auditBefore = previous is null ? null : CreateOidcAuditPayload(previous);
            var auditAfter = CreateOidcAuditPayload(configuration);
            var summary = configuration.Enabled
                ? "Aktualizace nastavení OIDC"
                : "Deaktivace OIDC přihlášení";

            AddAuditLog(
                "Security.OIDC",
                Guid.Empty,
                "Update",
                summary,
                new { Before = auditBefore, After = auditAfter });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("ldap/config")]
    public async Task<ActionResult<LdapConfigurationModel>> GetLdapConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _ldapConfigurationStore.GetAsync(cancellationToken);
        return Ok(configuration);
    }

    [HttpPost("ldap/config")]
    public async Task<ActionResult> ConfigureLdapAsync([FromBody] LdapConfigurationRequest request, CancellationToken cancellationToken)
    {
        var previous = await _ldapConfigurationStore.GetAsync(cancellationToken);
        var update = new LdapConfigurationUpdate(
            request.Enabled,
            request.Host,
            request.Port,
            request.UseSsl,
            request.BindDn,
            request.Password,
            request.ResetPassword,
            request.IgnoreCertificateErrors,
            request.UsersBaseDn,
            request.UsersFilter,
            request.AttributeMap ?? Array.Empty<string>());

        var updated = await _ldapConfigurationStore.SaveAsync(update, cancellationToken);

        if (!LdapConfigsEqual(previous, updated))
        {
            var auditBefore = CreateLdapAuditPayload(previous);
            var auditAfter = CreateLdapAuditPayload(updated);

            AddAuditLog(
                "Security.LDAP",
                Guid.Empty,
                "Update",
                "Aktualizace nastavení LDAP/AD",
                new { Before = auditBefore, After = auditAfter });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("ldap/test")]
    public async Task<ActionResult<LdapConnectionTestResult>> TestLdapAsync([FromBody] LdapConnectionOptions options, CancellationToken cancellationToken)
    {
        var result = await _ldapSyncService.TestConnectionAsync(options, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ldap/dry-run")]
    public async Task<ActionResult<LdapDryRunResult>> DryRunLdapAsync([FromBody] LdapDryRunRequest request, CancellationToken cancellationToken)
    {
        var result = await _ldapSyncService.DryRunAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sspr/config")]
    public async Task<ActionResult<SsprConfigurationModel>> GetSsprConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _ssprConfigurationStore.GetAsync(cancellationToken);
        return Ok(configuration);
    }

    [HttpPost("sspr/config")]
    public async Task<ActionResult> ConfigureSsprAsync([FromBody] SsprConfigurationRequest request, CancellationToken cancellationToken)
    {
        var previous = await _ssprConfigurationStore.GetAsync(cancellationToken);
        var update = new SsprConfigurationUpdate(
            request.Enabled,
            request.RequireTwoFactor,
            request.RequireCaptcha,
            request.RequireSmsOtp,
            request.TokenExpiryMinutes,
            request.ThrottleWindowMinutes,
            request.MaxRequestsPerWindow,
            request.SmsConnectorKey);

        var updated = await _ssprConfigurationStore.SaveAsync(update, cancellationToken);

        if (!SsprConfigsEqual(previous, updated))
        {
            var auditBefore = CreateSsprAuditPayload(previous);
            var auditAfter = CreateSsprAuditPayload(updated);

            AddAuditLog(
                "Security.SSPR",
                Guid.Empty,
                "Update",
                "Aktualizace nastavení SSPR",
                new { Before = auditBefore, After = auditAfter });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("users/{userId:guid}/scopes")]
    public async Task<ActionResult<IEnumerable<DataScopeResponse>>> GetScopesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var scopes = await DbContext.DataScopes
            .Where(x => x.AppUserId == userId && x.Status == EntityStatus.Active)
            .Select(x => new DataScopeResponse(x.Id, x.ScopeType, x.LocationId, x.DepartmentKey))
            .ToListAsync(cancellationToken);

        return Ok(scopes);
    }

    [HttpPut("users/{userId:guid}/scopes")]
    public async Task<ActionResult<IEnumerable<DataScopeResponse>>> UpdateScopesAsync(Guid userId, [FromBody] IEnumerable<DataScopeRequest> scopes, CancellationToken cancellationToken)
    {
        var requested = scopes?.ToList() ?? new List<DataScopeRequest>();
        var validScopeTypes = new[] { DataScopeTypes.Location, DataScopeTypes.Department };
        if (requested.Any(scope => !validScopeTypes.Contains(scope.ScopeType, StringComparer.OrdinalIgnoreCase)))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid scope type",
                Detail = "ScopeType must be either Location or Department."
            });
        }

        var normalizedRequests = requested.Select(Normalize).ToList();
        foreach (var normalized in normalizedRequests)
        {
            if (normalized.ScopeType == DataScopeTypes.Location && normalized.LocationId is null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Missing location",
                    Detail = "Location scopes must include LocationId."
                });
            }

            if (normalized.ScopeType == DataScopeTypes.Department && string.IsNullOrWhiteSpace(normalized.DepartmentKey))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Missing department",
                    Detail = "Department scopes must include DepartmentKey."
                });
            }
        }

        var existing = await DbContext.DataScopes.Where(x => x.AppUserId == userId).ToListAsync(cancellationToken);
        var beforeActive = existing
            .Where(x => x.Status == EntityStatus.Active)
            .Select(x => new ScopeSnapshot(x.ScopeType, x.LocationId, x.DepartmentKey))
            .ToList();

        foreach (var row in existing)
        {
            var match = normalizedRequests.FirstOrDefault(r => r.ScopeType.Equals(row.ScopeType, StringComparison.OrdinalIgnoreCase) &&
                Nullable.Equals(r.LocationId, row.LocationId) &&
                string.Equals(r.DepartmentKey, row.DepartmentKey, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                if (row.Status != EntityStatus.Retired)
                {
                    StampModification(row);
                    row.Status = EntityStatus.Retired;
                }
            }
            else if (row.Status != EntityStatus.Active)
            {
                StampModification(row);
                row.Status = EntityStatus.Active;
            }
        }

        foreach (var normalized in normalizedRequests)
        {
            var hasMatch = existing.Any(row => row.ScopeType.Equals(normalized.ScopeType, StringComparison.OrdinalIgnoreCase) &&
                Nullable.Equals(row.LocationId, normalized.LocationId) &&
                string.Equals(row.DepartmentKey, normalized.DepartmentKey, StringComparison.OrdinalIgnoreCase));

            if (!hasMatch)
            {
                var scope = new DataScope
                {
                    AppUserId = userId,
                    ScopeType = normalized.ScopeType,
                    LocationId = normalized.LocationId,
                    DepartmentKey = normalized.DepartmentKey,
                    Status = EntityStatus.Active
                };

                StampCreation(scope);
                DbContext.DataScopes.Add(scope);
            }
        }

        var afterActive = normalizedRequests
            .Select(x => new ScopeSnapshot(x.ScopeType, x.LocationId, x.DepartmentKey))
            .ToList();

        if (!ScopeSetsEqual(beforeActive, afterActive))
        {
            var beforeAudit = beforeActive.Select(x => new { x.ScopeType, x.LocationId, x.DepartmentKey }).ToList();
            var afterAudit = afterActive.Select(x => new { x.ScopeType, x.LocationId, x.DepartmentKey }).ToList();
            var summary = $"Aktualizovány datové scope uživatele {userId}";

            AddAuditLog(
                "Security.DataScopes",
                userId,
                "Update",
                summary,
                new { Before = beforeAudit, After = afterAudit });
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        var updated = await DbContext.DataScopes
            .Where(x => x.AppUserId == userId && x.Status == EntityStatus.Active)
            .Select(x => new DataScopeResponse(x.Id, x.ScopeType, x.LocationId, x.DepartmentKey))
            .ToListAsync(cancellationToken);

        return Ok(updated);
    }

    [Authorize(Policy = AuthorizationPolicies.SessionsManage)]
    [HttpGet("users/{userId:guid}/sessions")]
    public async Task<ActionResult<IEnumerable<UserSessionDto>>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await _sessionTracker.GetActiveSessionsAsync(userId, cancellationToken);
        return Ok(sessions);
    }

    [Authorize(Policy = AuthorizationPolicies.SessionsManage)]
    [HttpPost("sessions/{sessionId:guid}/revoke")]
    public async Task<ActionResult> RevokeSessionAsync(Guid sessionId, [FromBody] SessionRevokeRequest request, CancellationToken cancellationToken)
    {
        var actor = ResolveUserDisplayName();
        await _sessionTracker.RevokeAsync(sessionId, actor, request?.Reason, cancellationToken);

        AddAuditLog(
            "Security.Session",
            sessionId,
            "Revoke",
            $"Sezení odhlášeno administrátorem {actor}",
            new { SessionId = sessionId, Reason = request?.Reason });

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.SessionsManage)]
    [HttpPost("users/{userId:guid}/sessions/revoke-all")]
    public async Task<ActionResult> RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var actor = ResolveUserDisplayName();
        await _sessionTracker.RevokeAllAsync(userId, actor, cancellationToken);

        AddAuditLog(
            "Security.Session",
            userId,
            "RevokeAll",
            $"Hromadné odhlášení uživatele {userId}",
            new { UserId = userId });

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    public record OidcConfigurationRequest(
        bool Enabled,
        string Authority,
        string ClientId,
        string? ClientSecret,
        string? ResponseType,
        IReadOnlyCollection<string>? Scopes,
        IReadOnlyDictionary<string, string>? ClaimMappings,
        bool UsePkce);

    public record DataScopeRequest(string ScopeType, Guid? LocationId, string? DepartmentKey);

    public record DataScopeResponse(Guid Id, string ScopeType, Guid? LocationId, string? DepartmentKey);

    public record SessionRevokeRequest(string? Reason);

    public record LdapConfigurationRequest(
        bool Enabled,
        string Host,
        int Port,
        bool UseSsl,
        string BindDn,
        string? Password,
        bool ResetPassword,
        bool IgnoreCertificateErrors,
        string UsersBaseDn,
        string? UsersFilter,
        IReadOnlyCollection<string>? AttributeMap);

    public record SsprConfigurationRequest(
        bool Enabled,
        bool RequireTwoFactor,
        bool RequireCaptcha,
        bool RequireSmsOtp,
        int TokenExpiryMinutes,
        int ThrottleWindowMinutes,
        int MaxRequestsPerWindow,
        string? SmsConnectorKey);

    private static NormalizedScope Normalize(DataScopeRequest request)
    {
        var scopeType = request.ScopeType.Equals(DataScopeTypes.Department, StringComparison.OrdinalIgnoreCase)
            ? DataScopeTypes.Department
            : DataScopeTypes.Location;

        var locationId = scopeType == DataScopeTypes.Location ? request.LocationId : null;
        var departmentKey = scopeType == DataScopeTypes.Department ? request.DepartmentKey : null;

        return new NormalizedScope(scopeType, locationId, departmentKey);
    }

    private static OidcSnapshot CreateOidcSnapshot(OidcConfiguration configuration)
    {
        var scopeSignature = string.Join("|", configuration.Scopes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var claimSignature = string.Join("|", configuration.ClaimMappings
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));

        return new OidcSnapshot(
            configuration.Enabled,
            configuration.Authority,
            configuration.ClientId,
            !string.IsNullOrWhiteSpace(configuration.ClientSecret),
            configuration.ResponseType ?? "code",
            scopeSignature,
            claimSignature,
            configuration.UsePkce);
    }

    private static object CreateOidcAuditPayload(OidcConfiguration configuration) => new
    {
        configuration.Enabled,
        configuration.Authority,
        configuration.ClientId,
        ClientSecretSet = !string.IsNullOrWhiteSpace(configuration.ClientSecret),
        ResponseType = configuration.ResponseType,
        Scopes = configuration.Scopes,
        ClaimMappings = configuration.ClaimMappings,
        configuration.UsePkce
    };

    private static bool ScopeSetsEqual(IReadOnlyCollection<ScopeSnapshot> before, IReadOnlyCollection<ScopeSnapshot> after)
    {
        if (before.Count != after.Count)
        {
            return false;
        }

        var beforeKeys = new HashSet<string>(before.Select(CreateScopeKey), StringComparer.OrdinalIgnoreCase);
        var afterKeys = new HashSet<string>(after.Select(CreateScopeKey), StringComparer.OrdinalIgnoreCase);
        return beforeKeys.SetEquals(afterKeys);
    }

    private static string CreateScopeKey(ScopeSnapshot scope)
    {
        var normalizedType = scope.ScopeType.Equals(DataScopeTypes.Department, StringComparison.OrdinalIgnoreCase)
            ? DataScopeTypes.Department
            : DataScopeTypes.Location;
        var location = scope.LocationId?.ToString() ?? string.Empty;
        var department = scope.DepartmentKey?.Trim().ToLowerInvariant() ?? string.Empty;
        return $"{normalizedType}|{location}|{department}";
    }

    private record NormalizedScope(string ScopeType, Guid? LocationId, string? DepartmentKey);

    private record OidcSnapshot(bool Enabled, string Authority, string ClientId, bool ClientSecretSet, string ResponseType, string ScopeSignature, string ClaimSignature, bool UsePkce);

    private record ScopeSnapshot(string ScopeType, Guid? LocationId, string? DepartmentKey);

    private static bool LdapConfigsEqual(LdapConfigurationModel before, LdapConfigurationModel after)
    {
        return before.Enabled == after.Enabled
            && string.Equals(before.Host, after.Host, StringComparison.OrdinalIgnoreCase)
            && before.Port == after.Port
            && before.UseSsl == after.UseSsl
            && string.Equals(before.BindDn, after.BindDn, StringComparison.Ordinal)
            && before.HasPassword == after.HasPassword
            && before.IgnoreCertificateErrors == after.IgnoreCertificateErrors
            && string.Equals(before.UsersBaseDn, after.UsersBaseDn, StringComparison.Ordinal)
            && string.Equals(before.UsersFilter ?? string.Empty, after.UsersFilter ?? string.Empty, StringComparison.Ordinal)
            && before.AttributeMap.SequenceEqual(after.AttributeMap ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool SsprConfigsEqual(SsprConfigurationModel before, SsprConfigurationModel after)
    {
        return before.Enabled == after.Enabled
            && before.RequireTwoFactor == after.RequireTwoFactor
            && before.RequireCaptcha == after.RequireCaptcha
            && before.RequireSmsOtp == after.RequireSmsOtp
            && before.TokenExpiryMinutes == after.TokenExpiryMinutes
            && before.ThrottleWindowMinutes == after.ThrottleWindowMinutes
            && before.MaxRequestsPerWindow == after.MaxRequestsPerWindow
            && string.Equals(before.SmsConnectorKey ?? string.Empty, after.SmsConnectorKey ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static object CreateLdapAuditPayload(LdapConfigurationModel model) => new
    {
        model.Enabled,
        model.Host,
        model.Port,
        model.UseSsl,
        model.BindDn,
        PasswordSet = model.HasPassword,
        model.IgnoreCertificateErrors,
        model.UsersBaseDn,
        model.UsersFilter,
        AttributeMap = model.AttributeMap
    };

    private static object CreateSsprAuditPayload(SsprConfigurationModel model) => new
    {
        model.Enabled,
        model.RequireTwoFactor,
        model.RequireCaptcha,
        model.RequireSmsOtp,
        model.TokenExpiryMinutes,
        model.ThrottleWindowMinutes,
        model.MaxRequestsPerWindow,
        model.SmsConnectorKey
    };
}
