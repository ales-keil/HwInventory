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

public class LdapConfigurationStore : ILdapConfigurationStore
{
    private const string SectionName = "Security.Ldap";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LdapConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LdapConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        return new LdapConfigurationModel(
            ReadBool(settings, "Enabled"),
            ReadString(settings, "Host"),
            ReadInt(settings, "Port", 389),
            ReadBool(settings, "UseSsl"),
            ReadString(settings, "BindDn"),
            settings.TryGetValue("Password", out var passwordSetting) && !string.IsNullOrWhiteSpace(passwordSetting.Value),
            ReadBool(settings, "IgnoreCertificateErrors"),
            ReadString(settings, "UsersBaseDn"),
            ReadNullableString(settings, "UsersFilter"),
            ReadList(settings, "AttributeMap"));
    }

    public async Task<LdapConfigurationModel> SaveAsync(LdapConfigurationUpdate update, CancellationToken cancellationToken = default)
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
        Upsert("Host", update.Host, false);
        Upsert("Port", update.Port.ToString(), false);
        Upsert("UseSsl", update.UseSsl.ToString(), false);
        Upsert("BindDn", update.BindDn, false);
        Upsert("IgnoreCertificateErrors", update.IgnoreCertificateErrors.ToString(), false);
        Upsert("UsersBaseDn", update.UsersBaseDn, false);
        Upsert("UsersFilter", update.UsersFilter, false);
        Upsert("AttributeMap", string.Join('\n', update.AttributeMap ?? Array.Empty<string>()), false);

        if (update.ResetPassword)
        {
            Upsert("Password", null, true);
        }
        else if (!string.IsNullOrWhiteSpace(update.Password))
        {
            Upsert("Password", update.Password, true);
        }
        else if (!existing.Any(x => x.Key == "Password"))
        {
            Upsert("Password", null, true);
        }

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

    private static IReadOnlyCollection<string> ReadList(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
