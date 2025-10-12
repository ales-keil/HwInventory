using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Security;

public class PasswordPolicyConfigurationStore : IPasswordPolicyConfigurationStore
{
    private const string SectionName = "Security.PasswordPolicy";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PasswordPolicyConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PasswordPolicyModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        return new PasswordPolicyModel(
            ReadBool(settings, "Enabled"),
            ReadInt(settings, "MinimumLength", 12),
            ReadBool(settings, "RequireUppercase", true),
            ReadBool(settings, "RequireLowercase", true),
            ReadBool(settings, "RequireDigit", true),
            ReadBool(settings, "RequireNonAlphanumeric", false),
            ReadInt(settings, "ExpirationDays", 90),
            ReadInt(settings, "HistoryCount", 5),
            ReadInt(settings, "LockoutAttempts", 5),
            ReadInt(settings, "LockoutDurationMinutes", 15));
    }

    public async Task<PasswordPolicyModel> SaveAsync(PasswordPolicyUpdate update, CancellationToken cancellationToken = default)
    {
        var actor = ResolveActor();
        var existing = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToListAsync(cancellationToken);

        void Upsert(string key, string? value)
        {
            var row = existing.FirstOrDefault(x => x.Key == key);
            if (row is null)
            {
                row = new AppSetting
                {
                    Section = SectionName,
                    Key = key,
                    Value = value,
                    IsSecret = false,
                    CreatedBy = actor,
                    ModifiedBy = actor
                };
                _dbContext.Settings.Add(row);
            }
            else
            {
                row.Value = value;
                row.ModifiedBy = actor;
            }
        }

        Upsert("Enabled", update.Enabled.ToString());
        Upsert("MinimumLength", update.MinimumLength.ToString());
        Upsert("RequireUppercase", update.RequireUppercase.ToString());
        Upsert("RequireLowercase", update.RequireLowercase.ToString());
        Upsert("RequireDigit", update.RequireDigit.ToString());
        Upsert("RequireNonAlphanumeric", update.RequireNonAlphanumeric.ToString());
        Upsert("ExpirationDays", update.ExpirationDays.ToString());
        Upsert("HistoryCount", update.HistoryCount.ToString());
        Upsert("LockoutAttempts", update.LockoutAttempts.ToString());
        Upsert("LockoutDurationMinutes", update.LockoutDurationMinutes.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key, bool fallback = false)
    {
        if (settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static int ReadInt(IReadOnlyDictionary<string, AppSetting> settings, string key, int fallback)
    {
        if (settings.TryGetValue(key, out var setting) && int.TryParse(setting.Value, out var value))
        {
            return value;
        }

        return fallback;
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
