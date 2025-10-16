using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Observability;

public class ObservabilityConfigurationStore : IObservabilityConfigurationStore
{
    private const string SectionName = "Observability";

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ObservabilityConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ObservabilityConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        var model = new ObservabilityConfigurationModel(
            ReadString(settings, "LogLevel", "Information", allowNull: false) ?? "Information",
            ReadBool(settings, "HealthEndpointEnabled", true),
            ReadBool(settings, "MetricsEndpointEnabled", false),
            ReadBool(settings, "CorrelationIdsEnabled", true),
            ReadBool(settings, "IncludeTraceIdentifier", true),
            ReadBool(settings, "OtelExporterEnabled", false),
            ReadString(settings, "OtelEndpoint", null, allowNull: true),
            ReadSecretPresent(settings, "OtelAuthToken"),
            ReadString(settings, "ResourceAttributes", null, allowNull: true));

        ObservabilityLogging.ApplyMinimumLevel(model.LogLevel);
        return model;
    }

    public async Task<ObservabilityConfigurationModel> SaveAsync(ObservabilityConfigurationUpdate update, CancellationToken cancellationToken = default)
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

        Upsert("LogLevel", update.LogLevel, false);
        Upsert("HealthEndpointEnabled", update.HealthEndpointEnabled.ToString(), false);
        Upsert("MetricsEndpointEnabled", update.MetricsEndpointEnabled.ToString(), false);
        Upsert("CorrelationIdsEnabled", update.CorrelationIdsEnabled.ToString(), false);
        Upsert("IncludeTraceIdentifier", update.IncludeTraceIdentifier.ToString(), false);
        Upsert("OtelExporterEnabled", update.OtelExporterEnabled.ToString(), false);
        Upsert("OtelEndpoint", string.IsNullOrWhiteSpace(update.OtelEndpoint) ? null : update.OtelEndpoint, false);
        Upsert("ResourceAttributes", string.IsNullOrWhiteSpace(update.ResourceAttributes) ? null : update.ResourceAttributes, false);

        if (update.RotateOtelAuthToken)
        {
            Upsert("OtelAuthToken", string.IsNullOrWhiteSpace(update.OtelAuthToken) ? null : update.OtelAuthToken, true);
        }
        else if (!string.IsNullOrWhiteSpace(update.OtelAuthToken))
        {
            Upsert("OtelAuthToken", update.OtelAuthToken, true);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        ObservabilityLogging.ApplyMinimumLevel(update.LogLevel);
        return await GetAsync(cancellationToken);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key, bool fallback)
    {
        if (settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var value))
        {
            return value;
        }

        return fallback;
    }

    private static string? ReadString(IReadOnlyDictionary<string, AppSetting> settings, string key, string? fallback, bool allowNull)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value!;
        }

        if (allowNull)
        {
            return fallback;
        }

        return fallback ?? string.Empty;
    }

    private static bool ReadSecretPresent(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        return settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value);
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
