using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Security;

public class CaptchaConfigurationStore : ICaptchaConfigurationStore
{
    private const string SectionName = "Security.Captcha";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CaptchaConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<CaptchaConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        var enabled = ReadBool(settings, "Enabled");
        var siteKey = ReadString(settings, "SiteKey");
        var endpoint = ReadString(settings, "VerificationEndpoint");
        var bypass = ReadNullableString(settings, "BypassToken");
        var hasSecret = settings.TryGetValue("Secret", out var secretSetting) && !string.IsNullOrWhiteSpace(secretSetting.Value);

        return new CaptchaConfigurationModel(enabled, siteKey, hasSecret, endpoint, bypass);
    }

    public async Task<CaptchaConfigurationModel> SaveAsync(CaptchaConfigurationUpdate update, CancellationToken cancellationToken = default)
    {
        var actor = ResolveActor();
        var existing = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToListAsync(cancellationToken);

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
                    IsSecret = isSecret,
                    CreatedBy = actor,
                    ModifiedBy = actor
                };
                _dbContext.Settings.Add(row);
            }
            else
            {
                row.Value = value;
                row.IsSecret = isSecret;
                row.ModifiedBy = actor;
            }
        }

        Upsert("Enabled", update.Enabled.ToString(), false);
        Upsert("SiteKey", update.SiteKey, false);
        Upsert("VerificationEndpoint", update.VerificationEndpoint, false);
        Upsert("BypassToken", update.BypassToken, false);

        if (update.RotateSecret)
        {
            Upsert("Secret", update.Secret, true);
        }
        else if (!string.IsNullOrWhiteSpace(update.Secret))
        {
            Upsert("Secret", update.Secret, true);
        }
        else if (!existing.Any(x => x.Key == "Secret"))
        {
            Upsert("Secret", null, true);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        return settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var value) && value;
    }

    private static string ReadString(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value!;
        }

        return string.Empty;
    }

    private static string? ReadNullableString(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value;
        }

        return null;
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
