using System.Collections.Generic;
using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Security;

public class ExternalIdentityService : IExternalIdentityService
{
    private const string SectionName = "Security.Oidc";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExternalIdentityService> _logger;

    public ExternalIdentityService(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor, ILogger<ExternalIdentityService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task ConfigureOidcAsync(OidcConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToListAsync(cancellationToken);

        var actor = ResolveActor();

        void Upsert(string key, string? value, bool isSecret)
        {
            var row = existing.FirstOrDefault(x => x.Key == key);
            if (row is null)
            {
                row = new AppSetting
                {
                    Section = SectionName,
                    Key = key,
                    Value = value,
                    IsSecret = isSecret
                };

                row.CreatedBy = actor;
                row.ModifiedBy = actor;
                _dbContext.Settings.Add(row);
            }
            else
            {
                row.ModifiedBy = actor;
                row.Value = value;
                row.IsSecret = isSecret;
            }
        }

        Upsert("Enabled", configuration.Enabled.ToString(), false);
        Upsert("Authority", configuration.Authority, false);
        Upsert("ClientId", configuration.ClientId, true);
        Upsert("ClientSecret", configuration.ClientSecret, true);
        Upsert("ResponseType", configuration.ResponseType ?? "code", false);
        Upsert("UsePkce", configuration.UsePkce.ToString(), false);
        Upsert("Scopes", string.Join(' ', configuration.Scopes), false);

        var claimMappings = string.Join(';', configuration.ClaimMappings.Select(pair => $"{pair.Key}:{pair.Value}"));
        Upsert("ClaimMappings", claimMappings, false);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("OIDC configuration updated by {Actor}", actor);
    }

    public async Task<OidcConfiguration?> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        if (settings.Count == 0)
        {
            return null;
        }

        bool enabled = settings.TryGetValue("Enabled", out var enabledSetting) && bool.TryParse(enabledSetting.Value, out var enabledValue) && enabledValue;
        settings.TryGetValue("Authority", out var authoritySetting);
        settings.TryGetValue("ClientId", out var clientIdSetting);
        settings.TryGetValue("ClientSecret", out var secretSetting);
        settings.TryGetValue("ResponseType", out var responseTypeSetting);
        settings.TryGetValue("UsePkce", out var pkceSetting);
        settings.TryGetValue("Scopes", out var scopesSetting);
        settings.TryGetValue("ClaimMappings", out var claimsSetting);

        var scopes = (scopesSetting?.Value ?? "openid profile email").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var claimMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(claimsSetting?.Value))
        {
            foreach (var pair in claimsSetting.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var kvp = pair.Split(':', 2);
                if (kvp.Length == 2)
                {
                    claimMap[kvp[0]] = kvp[1];
                }
            }
        }

        var configuration = new OidcConfiguration(
            enabled,
            authoritySetting?.Value ?? string.Empty,
            clientIdSetting?.Value ?? string.Empty,
            secretSetting?.Value,
            responseTypeSetting?.Value,
            scopes,
            claimMap,
            pkceSetting != null && bool.TryParse(pkceSetting.Value, out var pkce) && pkce);

        _logger.LogDebug("OIDC configuration requested. Enabled={Enabled}", configuration.Enabled);
        return configuration;
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
