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
    private readonly ICaptchaConfigurationStore _captchaConfigurationStore;
    private readonly ISmsConnectorStore _smsConnectorStore;
    private readonly IPasswordPolicyConfigurationStore _passwordPolicyConfigurationStore;

    public SecurityController(
        IAppDbContext dbContext,
        ILdapSyncService ldapSyncService,
        ILdapConfigurationStore ldapConfigurationStore,
        IExternalIdentityService externalIdentityService,
        ISessionTracker sessionTracker,
        ISsprConfigurationStore ssprConfigurationStore,
        ICaptchaConfigurationStore captchaConfigurationStore,
        ISmsConnectorStore smsConnectorStore,
        IPasswordPolicyConfigurationStore passwordPolicyConfigurationStore) : base(dbContext)
    {
        _ldapSyncService = ldapSyncService;
        _ldapConfigurationStore = ldapConfigurationStore;
        _externalIdentityService = externalIdentityService;
        _sessionTracker = sessionTracker;
        _ssprConfigurationStore = ssprConfigurationStore;
        _captchaConfigurationStore = captchaConfigurationStore;
        _smsConnectorStore = smsConnectorStore;
        _passwordPolicyConfigurationStore = passwordPolicyConfigurationStore;
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

        if (request.Enabled)
        {
            if (string.IsNullOrWhiteSpace(request.Authority) || !Uri.TryCreate(request.Authority, UriKind.Absolute, out var authorityUri) || authorityUri.Scheme != Uri.UriSchemeHttps)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Neplatná adresa OIDC",
                    Detail = "Authority musí být platná HTTPS URL adresa."
                });
            }

            if (string.IsNullOrWhiteSpace(request.ClientId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Chybí ClientId",
                    Detail = "Pro aktivaci OIDC je vyžadován identifikátor klienta."
                });
            }

            var responseType = string.IsNullOrWhiteSpace(request.ResponseType) ? "code" : request.ResponseType.Trim();
            if (!string.Equals(responseType, "code", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Nepodporovaný response type",
                    Detail = "Podporována je pouze autorizace s response_type=code."
                });
            }

            if (request.Scopes is not null && !request.Scopes.Contains("openid", StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Chybějící scope",
                    Detail = "Scope 'openid' je pro OIDC povinný."
                });
            }

            if (!request.UsePkce)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "PKCE je povinné",
                    Detail = "Pro bezpečné přihlášení musí být povoleno PKCE."
                });
            }
        }

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

    [HttpGet("summary")]
    public async Task<ActionResult<SecuritySummaryResponse>> GetSecuritySummaryAsync(CancellationToken cancellationToken)
    {
        var oidc = await _externalIdentityService.GetConfigurationAsync(cancellationToken);
        var ldap = await _ldapConfigurationStore.GetAsync(cancellationToken);
        var sspr = await _ssprConfigurationStore.GetAsync(cancellationToken);
        var captcha = await _captchaConfigurationStore.GetAsync(cancellationToken);
        var sms = await _smsConnectorStore.GetAsync(cancellationToken);
        var passwordPolicy = await _passwordPolicyConfigurationStore.GetAsync(cancellationToken);

        var users = await DbContext.Users.AsNoTracking().ToListAsync(cancellationToken);
        var totalUsers = users.Count;
        var twoFactorUsers = users.Count(x => x.TwoFactorEnabled);
        var lockedOutUsers = users.Count(x => x.LockoutEnd.HasValue && x.LockoutEnd.Value > DateTimeOffset.UtcNow);

        var oidcEnabled = oidc?.Enabled == true;
        var ldapEnabled = ldap.Enabled;
        var smsEnabled = sms?.Enabled == true;
        var criticalReady = passwordPolicy.Enabled
            && (oidcEnabled || ldapEnabled)
            && sspr.Enabled
            && sspr.RequireTwoFactor
            && captcha.Enabled
            && smsEnabled;

        var summary = new SecuritySummaryResponse(
            oidcEnabled,
            ldapEnabled,
            sspr.Enabled,
            captcha.Enabled,
            smsEnabled,
            passwordPolicy.Enabled,
            totalUsers,
            twoFactorUsers,
            lockedOutUsers,
            ResolveLatestChange("Security.Oidc"),
            ResolveLatestChange("Security.Ldap"),
            ResolveLatestChange("Security.PasswordPolicy"),
            ResolveLatestChange("Security.SSPR"),
            criticalReady);

        return Ok(summary);
    }

    private SecurityChangeMetadata ResolveLatestChange(string section)
    {
        var settings = DbContext.Settings.AsNoTracking().Where(x => x.Section == section);
        var latest = settings
            .OrderByDescending(x => x.ModifiedAtUtc ?? x.CreatedAtUtc)
            .Select(x => new { x.ModifiedAtUtc, x.CreatedAtUtc, x.ModifiedBy, x.CreatedBy })
            .FirstOrDefault();

        if (latest is null)
        {
            return new SecurityChangeMetadata(null, null);
        }

        var timestamp = latest.ModifiedAtUtc ?? latest.CreatedAtUtc;
        var actor = latest.ModifiedBy ?? latest.CreatedBy;
        return new SecurityChangeMetadata(timestamp, actor);
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

    [HttpGet("password-policy")]
    public async Task<ActionResult<PasswordPolicyModel>> GetPasswordPolicyAsync(CancellationToken cancellationToken)
    {
        var configuration = await _passwordPolicyConfigurationStore.GetAsync(cancellationToken);
        return Ok(configuration);
    }

    [HttpPost("password-policy")]
    public async Task<ActionResult> ConfigurePasswordPolicyAsync([FromBody] PasswordPolicyRequest request, CancellationToken cancellationToken)
    {
        if (request.MinimumLength < 6)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Příliš krátké heslo",
                Detail = "Minimální délka hesla musí být alespoň 6 znaků."
            });
        }

        if (request.LockoutAttempts < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatný limit pokusů",
                Detail = "Počet pokusů pro uzamknutí musí být kladné číslo."
            });
        }

        if (request.LockoutDurationMinutes < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatná doba uzamčení",
                Detail = "Doba uzamčení musí být alespoň 1 minuta."
            });
        }

        if (request.ExpirationDays < 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatná expirace",
                Detail = "Expirace hesla nemůže být záporná."
            });
        }

        if (request.HistoryCount < 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatná historie",
                Detail = "Počet uchovaných hesel nemůže být záporný."
            });
        }

        var previous = await _passwordPolicyConfigurationStore.GetAsync(cancellationToken);
        var update = new PasswordPolicyUpdate(
            request.Enabled,
            request.MinimumLength,
            request.RequireUppercase,
            request.RequireLowercase,
            request.RequireDigit,
            request.RequireNonAlphanumeric,
            request.ExpirationDays,
            request.HistoryCount,
            request.LockoutAttempts,
            request.LockoutDurationMinutes);

        var updated = await _passwordPolicyConfigurationStore.SaveAsync(update, cancellationToken);

        if (!PasswordPoliciesEqual(previous, updated))
        {
            AddAuditLog(
                "Security.PasswordPolicy",
                Guid.Empty,
                "Update",
                "Aktualizace politiky hesel",
                new { Before = previous, After = updated });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("captcha/config")]
    public async Task<ActionResult<CaptchaConfigurationModel>> GetCaptchaConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _captchaConfigurationStore.GetAsync(cancellationToken);
        return Ok(configuration);
    }

    [HttpPost("captcha/config")]
    public async Task<ActionResult> ConfigureCaptchaAsync([FromBody] CaptchaConfigurationRequest request, CancellationToken cancellationToken)
    {
        var previous = await _captchaConfigurationStore.GetAsync(cancellationToken);
        var update = new CaptchaConfigurationUpdate(
            request.Enabled,
            request.SiteKey,
            request.Secret,
            request.RotateSecret,
            request.VerificationEndpoint,
            request.BypassToken);

        var updated = await _captchaConfigurationStore.SaveAsync(update, cancellationToken);

        if (!CaptchaConfigsEqual(previous, updated))
        {
            AddAuditLog(
                "Security.Captcha",
                Guid.Empty,
                "Update",
                "Aktualizace nastavení CAPTCHA",
                new { Before = previous, After = updated });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("sms/config")]
    public async Task<ActionResult<SmsConnectorModel?>> GetSmsConnectorAsync(CancellationToken cancellationToken)
    {
        var connector = await _smsConnectorStore.GetAsync(cancellationToken);
        return Ok(connector);
    }

    [HttpPost("sms/config")]
    public async Task<ActionResult<SmsConnectorModel>> ConfigureSmsAsync([FromBody] SmsConnectorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chybí endpoint",
                Detail = "Je nutné zadat URL endpointu SMS konektoru."
            });
        }

        var update = new SmsConnectorUpdate(
            request.Alias,
            request.Enabled,
            request.Endpoint,
            request.Sender,
            request.Region,
            request.Secret,
            request.RotateSecret);

        var previous = await _smsConnectorStore.GetAsync(cancellationToken);
        var result = await _smsConnectorStore.SaveAsync(update, cancellationToken);

        if (previous is null || !SmsConfigsEqual(previous, result) || request.RotateSecret)
        {
            AddAuditLog(
                "Security.SMS",
                result.Id ?? Guid.Empty,
                "Update",
                "Aktualizace SMS konektoru",
                new
                {
                    Before = previous,
                    After = result,
                    RotatedSecret = request.RotateSecret
                });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(result);
    }

    [HttpGet("ldap/role-mappings")]
    public async Task<ActionResult<IEnumerable<LdapRoleMappingResponse>>> GetLdapRoleMappingsAsync(CancellationToken cancellationToken)
    {
        var mappings = await DbContext.LdapRoleMappings
            .Where(x => x.Status == EntityStatus.Active)
            .OrderBy(x => x.GroupName)
            .Select(x => new LdapRoleMappingResponse(x.Id, x.GroupName, x.RoleName))
            .ToListAsync(cancellationToken);

        return Ok(mappings);
    }

    [HttpPost("ldap/role-mappings")]
    public async Task<ActionResult<LdapRoleMappingResponse>> CreateLdapRoleMappingAsync([FromBody] LdapRoleMappingRequest request, CancellationToken cancellationToken)
    {
        var normalizedGroup = request.GroupName?.Trim();
        var normalizedRole = request.RoleName?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedGroup) || string.IsNullOrWhiteSpace(normalizedRole))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatná data",
                Detail = "Je nutné zadat skupinu i roli."
            });
        }

        if (!IsSupportedRole(normalizedRole))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neznámá role",
                Detail = "Zadaná role není podporována."
            });
        }

        var exists = await DbContext.LdapRoleMappings.AnyAsync(x =>
            x.Status == EntityStatus.Active &&
            x.GroupName.Equals(normalizedGroup, StringComparison.OrdinalIgnoreCase) &&
            x.RoleName.Equals(normalizedRole, StringComparison.OrdinalIgnoreCase), cancellationToken);

        if (exists)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Mapování již existuje",
                Detail = "Kombinace skupiny a role je již definována."
            });
        }

        var mapping = new LdapRoleMapping
        {
            GroupName = normalizedGroup,
            RoleName = normalizedRole,
            Status = EntityStatus.Active
        };

        StampCreation(mapping);
        DbContext.LdapRoleMappings.Add(mapping);
        await DbContext.SaveChangesAsync(cancellationToken);

        AddAuditLog(
            "Security.LdapRoleMapping",
            mapping.Id,
            "Create",
            "Vytvořeno mapování AD skupiny na roli",
            new { mapping.GroupName, mapping.RoleName });

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LdapRoleMappingResponse(mapping.Id, mapping.GroupName, mapping.RoleName));
    }

    [HttpPut("ldap/role-mappings/{id:guid}")]
    public async Task<ActionResult<LdapRoleMappingResponse>> UpdateLdapRoleMappingAsync(Guid id, [FromBody] LdapRoleMappingRequest request, CancellationToken cancellationToken)
    {
        var mapping = await DbContext.LdapRoleMappings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (mapping is null)
        {
            return NotFound();
        }

        var normalizedGroup = request.GroupName?.Trim();
        var normalizedRole = request.RoleName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedGroup) || string.IsNullOrWhiteSpace(normalizedRole))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatná data",
                Detail = "Je nutné zadat skupinu i roli."
            });
        }

        if (!IsSupportedRole(normalizedRole))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neznámá role",
                Detail = "Zadaná role není podporována."
            });
        }

        var before = new { mapping.GroupName, mapping.RoleName, mapping.Status };

        mapping.GroupName = normalizedGroup;
        mapping.RoleName = normalizedRole;
        mapping.Status = EntityStatus.Active;
        StampModification(mapping);

        await DbContext.SaveChangesAsync(cancellationToken);

        AddAuditLog(
            "Security.LdapRoleMapping",
            mapping.Id,
            "Update",
            "Aktualizováno mapování AD skupiny",
            new { Before = before, After = new { mapping.GroupName, mapping.RoleName, mapping.Status } });

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LdapRoleMappingResponse(mapping.Id, mapping.GroupName, mapping.RoleName));
    }

    [HttpDelete("ldap/role-mappings/{id:guid}")]
    public async Task<ActionResult> DeleteLdapRoleMappingAsync(Guid id, CancellationToken cancellationToken)
    {
        var mapping = await DbContext.LdapRoleMappings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (mapping is null)
        {
            return NotFound();
        }

        if (mapping.Status == EntityStatus.Retired)
        {
            return NoContent();
        }

        var before = new { mapping.GroupName, mapping.RoleName, mapping.Status };
        mapping.Status = EntityStatus.Retired;
        StampModification(mapping);

        await DbContext.SaveChangesAsync(cancellationToken);

        AddAuditLog(
            "Security.LdapRoleMapping",
            mapping.Id,
            "Delete",
            "Mapování AD skupiny deaktivováno",
            new { Before = before, After = new { mapping.GroupName, mapping.RoleName, mapping.Status } });

        await DbContext.SaveChangesAsync(cancellationToken);
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
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserSummary>>> SearchUsersAsync([FromQuery] string? query, CancellationToken cancellationToken)
    {
        var normalized = query?.Trim();

        var usersQuery = DbContext.Users
            .Where(x => x.Status == EntityStatus.Active);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            var likePattern = $"%{normalized}%";
            usersQuery = usersQuery.Where(x =>
                EF.Functions.Like(x.DisplayName, likePattern) ||
                (x.Email != null && EF.Functions.Like(x.Email, likePattern)) ||
                (x.UserName != null && EF.Functions.Like(x.UserName, likePattern)));
        }

        var results = await usersQuery
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Email)
            .Take(20)
            .Select(x => new UserSummary(
                x.Id,
                string.IsNullOrWhiteSpace(x.DisplayName) ? (x.UserName ?? x.Email ?? string.Empty) : x.DisplayName,
                x.Email ?? string.Empty,
                x.UserName ?? string.Empty))
            .ToListAsync(cancellationToken);

        return Ok(results);
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

    public record PasswordPolicyRequest(
        bool Enabled,
        int MinimumLength,
        bool RequireUppercase,
        bool RequireLowercase,
        bool RequireDigit,
        bool RequireNonAlphanumeric,
        int ExpirationDays,
        int HistoryCount,
        int LockoutAttempts,
        int LockoutDurationMinutes);

    public record DataScopeRequest(string ScopeType, Guid? LocationId, string? DepartmentKey);

    public record DataScopeResponse(Guid Id, string ScopeType, Guid? LocationId, string? DepartmentKey);

    public record SessionRevokeRequest(string? Reason);

    public record UserSummary(Guid Id, string DisplayName, string Email, string UserName);

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

    public record CaptchaConfigurationRequest(
        bool Enabled,
        string SiteKey,
        string VerificationEndpoint,
        string? Secret,
        bool RotateSecret,
        string? BypassToken);

    public record SmsConnectorRequest(
        string Alias,
        bool Enabled,
        string Endpoint,
        string? Sender,
        string? Region,
        string? Secret,
        bool RotateSecret);

    public record LdapRoleMappingRequest(string GroupName, string RoleName);

    public record LdapRoleMappingResponse(Guid Id, string GroupName, string RoleName);

    private static NormalizedScope Normalize(DataScopeRequest request)
    {
        var scopeType = request.ScopeType.Equals(DataScopeTypes.Department, StringComparison.OrdinalIgnoreCase)
            ? DataScopeTypes.Department
            : DataScopeTypes.Location;

        var locationId = scopeType == DataScopeTypes.Location ? request.LocationId : null;
        var departmentKey = scopeType == DataScopeTypes.Department ? request.DepartmentKey : null;

        return new NormalizedScope(scopeType, locationId, departmentKey);
    }

    private static bool CaptchaConfigsEqual(CaptchaConfigurationModel previous, CaptchaConfigurationModel current)
    {
        return previous.Enabled == current.Enabled &&
            string.Equals(previous.SiteKey, current.SiteKey, StringComparison.Ordinal) &&
            previous.HasSecret == current.HasSecret &&
            string.Equals(previous.VerificationEndpoint, current.VerificationEndpoint, StringComparison.Ordinal) &&
            string.Equals(previous.BypassToken, current.BypassToken, StringComparison.Ordinal);
    }

    private static bool SmsConfigsEqual(SmsConnectorModel before, SmsConnectorModel current)
    {
        return string.Equals(before.Alias, current.Alias, StringComparison.Ordinal) &&
            before.Enabled == current.Enabled &&
            string.Equals(before.Endpoint, current.Endpoint, StringComparison.Ordinal) &&
            string.Equals(before.Sender ?? string.Empty, current.Sender ?? string.Empty, StringComparison.Ordinal) &&
            string.Equals(before.Region ?? string.Empty, current.Region ?? string.Empty, StringComparison.Ordinal) &&
            before.HasSecret == current.HasSecret;
    }

    private static readonly string[] SupportedRoles =
    {
        SystemRoleNames.SuperAdmin,
        SystemRoleNames.ServerAdmin,
        SystemRoleNames.NetworkAdmin,
        SystemRoleNames.AppAdmin,
        SystemRoleNames.Helpdesk,
        SystemRoleNames.HelpdeskRead
    };

    private static bool IsSupportedRole(string roleName)
    {
        return SupportedRoles.Any(r => r.Equals(roleName, StringComparison.OrdinalIgnoreCase));
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

    private static bool PasswordPoliciesEqual(PasswordPolicyModel before, PasswordPolicyModel after)
    {
        return before.Enabled == after.Enabled
            && before.MinimumLength == after.MinimumLength
            && before.RequireUppercase == after.RequireUppercase
            && before.RequireLowercase == after.RequireLowercase
            && before.RequireDigit == after.RequireDigit
            && before.RequireNonAlphanumeric == after.RequireNonAlphanumeric
            && before.ExpirationDays == after.ExpirationDays
            && before.HistoryCount == after.HistoryCount
            && before.LockoutAttempts == after.LockoutAttempts
            && before.LockoutDurationMinutes == after.LockoutDurationMinutes;
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

    public record SecuritySummaryResponse(
        bool OidcEnabled,
        bool LdapEnabled,
        bool SsprEnabled,
        bool CaptchaEnabled,
        bool SmsEnabled,
        bool PasswordPolicyEnabled,
        int TotalUsers,
        int TwoFactorUsers,
        int LockedOutUsers,
        SecurityChangeMetadata OidcLastChange,
        SecurityChangeMetadata LdapLastChange,
        SecurityChangeMetadata PasswordPolicyLastChange,
        SecurityChangeMetadata SsprLastChange,
        bool CriticalReady);

    public record SecurityChangeMetadata(DateTime? TimestampUtc, string? Actor);
}
