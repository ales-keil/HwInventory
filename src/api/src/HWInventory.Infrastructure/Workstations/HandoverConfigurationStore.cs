using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Workstations;

public class HandoverConfigurationStore : IHandoverConfigurationStore
{
    private const string SectionName = "Handover";

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HandoverConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<HandoverConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        return new HandoverConfigurationModel(
            ReadList(settings, "DefaultTo"),
            ReadList(settings, "DefaultCc"),
            ReadList(settings, "DefaultBcc"),
            ReadString(settings, "DefaultSubject", allowEmpty: true),
            ReadString(settings, "DefaultBody", allowEmpty: true),
            ReadString(settings, "PdfLogoBase64", allowEmpty: true),
            ReadBool(settings, "UseMinimalPdf"),
            ReadString(settings, "PdfFooterNote", allowEmpty: true));
    }

    public async Task<HandoverConfigurationModel> SaveAsync(HandoverConfigurationModel configuration, CancellationToken cancellationToken = default)
    {
        var actor = ResolveActor();
        var existing = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToListAsync(cancellationToken);

        void Upsert(string key, string? value, bool isSecret = false)
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

        Upsert("DefaultTo", SerializeList(configuration.DefaultTo));
        Upsert("DefaultCc", SerializeList(configuration.DefaultCc));
        Upsert("DefaultBcc", SerializeList(configuration.DefaultBcc));
        Upsert("DefaultSubject", NormalizeString(configuration.DefaultSubject));
        Upsert("DefaultBody", NormalizeString(configuration.DefaultBody));
        Upsert("PdfLogoBase64", NormalizeString(configuration.PdfLogoBase64), isSecret: true);
        Upsert("UseMinimalPdf", configuration.UseMinimalPdf.ToString());
        Upsert("PdfFooterNote", NormalizeString(configuration.PdfFooterNote));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static IReadOnlyList<string> ReadList(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value
                .Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static string? ReadString(IReadOnlyDictionary<string, AppSetting> settings, string key, bool allowEmpty = false)
    {
        if (settings.TryGetValue(key, out var setting))
        {
            var value = setting.Value?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            if (allowEmpty && value != null)
            {
                return string.Empty;
            }
        }

        return allowEmpty ? string.Empty : null;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        return settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var value) && value;
    }

    private static string? NormalizeString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? SerializeList(IEnumerable<string> values)
    {
        var normalized = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join('\n', normalized);
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
