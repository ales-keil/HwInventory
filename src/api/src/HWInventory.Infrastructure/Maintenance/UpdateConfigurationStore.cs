using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Maintenance;

public class UpdateConfigurationStore : IUpdateConfigurationStore
{
    private const string SectionName = "Maintenance.Updates";
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UpdateConfigurationStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UpdateDeploymentConfigurationModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings
            .Where(x => x.Section == SectionName)
            .ToDictionaryAsync(x => x.Key, x => x, cancellationToken);

        var deploymentPath = ReadString(settings, "DeploymentRootPath", AppContext.BaseDirectory);
        var webRoot = ReadNullableString(settings, "WebRootPath");
        var useAppOffline = ReadBool(settings, "UseAppOfflineFile");
        var runMigrations = ReadBool(settings, "RunMigrations", defaultValue: true);
        var postScript = ReadNullableString(settings, "PostDeploymentScript");

        return new UpdateDeploymentConfigurationModel(deploymentPath, webRoot, useAppOffline, runMigrations, postScript);
    }

    public async Task<UpdateDeploymentConfigurationModel> SaveAsync(UpdateDeploymentConfigurationUpdate update, CancellationToken cancellationToken = default)
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

        Upsert("DeploymentRootPath", update.DeploymentRootPath, false);
        Upsert("WebRootPath", update.WebRootPath, false);
        Upsert("UseAppOfflineFile", update.UseAppOfflineFile.ToString(), false);
        Upsert("RunMigrations", update.RunMigrations.ToString(), false);
        Upsert("PostDeploymentScript", update.PostDeploymentScript, false);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private static string ReadString(IReadOnlyDictionary<string, AppSetting> settings, string key, string defaultValue)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value!;
        }

        return defaultValue;
    }

    private static string? ReadNullableString(IReadOnlyDictionary<string, AppSetting> settings, string key)
    {
        if (settings.TryGetValue(key, out var setting) && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value;
        }

        return null;
    }

    private static bool ReadBool(IReadOnlyDictionary<string, AppSetting> settings, string key, bool defaultValue = false)
    {
        if (settings.TryGetValue(key, out var setting) && bool.TryParse(setting.Value, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
