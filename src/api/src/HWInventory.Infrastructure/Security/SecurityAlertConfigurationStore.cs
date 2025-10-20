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

public class SecurityAlertConfigurationStore : ISecurityAlertConfigurationStore
{
    private const string SectionName = "Security.Alerts";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SecurityAlertConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<SecurityAlertConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        return new SecurityAlertConfigurationModel(
            ReadBool(settings, "Enabled", true),
            ReadDouble(settings, "MinimumTwoFactorAdoptionPercentage", 75d),
            ReadInt(settings, "LockedAccountThreshold", 5),
            ReadInt(settings, "PendingResetThreshold", 10),
            ReadEmails(settings));
    }

    public async Task<SecurityAlertConfigurationModel> SaveAsync(SecurityAlertConfigurationUpdate update, CancellationToken cancellationToken = default)
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
        Upsert("MinimumTwoFactorAdoptionPercentage", Math.Round(update.MinimumTwoFactorAdoptionPercentage, 2).ToString("F2"), false);
        Upsert("LockedAccountThreshold", update.LockedAccountThreshold.ToString(), false);
        Upsert("PendingResetThreshold", update.PendingResetThreshold.ToString(), false);
        Upsert("NotificationEmails", string.Join(';', update.NotificationEmails ?? Array.Empty<string>()), false);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key, bool fallback)
    {
        if (settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static int ReadInt(IReadOnlyDictionary<string, AppSetting> settings, string key, int fallback)
    {
        if (settings.TryGetValue(key, out var setting) && int.TryParse(setting.Value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static double ReadDouble(IReadOnlyDictionary<string, AppSetting> settings, string key, double fallback)
    {
        if (settings.TryGetValue(key, out var setting) && double.TryParse(setting.Value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static IReadOnlyCollection<string> ReadEmails(IReadOnlyDictionary<string, AppSetting> settings)
    {
        if (settings.TryGetValue("NotificationEmails", out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
