using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Security;

public class SsprConfigurationStore : ISsprConfigurationStore
{
    private const string SectionName = "Security.Sspr";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SsprConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<SsprConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        return new SsprConfigurationModel(
            ReadBool(settings, "Enabled"),
            ReadBool(settings, "RequireTwoFactor"),
            ReadBool(settings, "RequireCaptcha"),
            ReadBool(settings, "RequireSmsOtp"),
            ReadInt(settings, "TokenExpiryMinutes", 30),
            ReadInt(settings, "ThrottleWindowMinutes", 15),
            ReadInt(settings, "MaxRequestsPerWindow", 3),
            ReadString(settings, "SmsConnectorKey", allowNull: true));
    }

    public async Task<SsprConfigurationModel> SaveAsync(SsprConfigurationUpdate update, CancellationToken cancellationToken = default)
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
        Upsert("RequireTwoFactor", update.RequireTwoFactor.ToString(), false);
        Upsert("RequireCaptcha", update.RequireCaptcha.ToString(), false);
        Upsert("RequireSmsOtp", update.RequireSmsOtp.ToString(), false);
        Upsert("TokenExpiryMinutes", update.TokenExpiryMinutes.ToString(), false);
        Upsert("ThrottleWindowMinutes", update.ThrottleWindowMinutes.ToString(), false);
        Upsert("MaxRequestsPerWindow", update.MaxRequestsPerWindow.ToString(), false);
        Upsert("SmsConnectorKey", string.IsNullOrWhiteSpace(update.SmsConnectorKey) ? null : update.SmsConnectorKey, false);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        return settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var value) && value;
    }

    private static int ReadInt(IReadOnlyDictionary<string, AppSetting> settings, string key, int fallback)
    {
        if (settings.TryGetValue(key, out var setting) && int.TryParse(setting.Value, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static string? ReadString(IReadOnlyDictionary<string, AppSetting> settings, string key, bool allowNull = false)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value;
        }

        return allowNull ? null : string.Empty;
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
