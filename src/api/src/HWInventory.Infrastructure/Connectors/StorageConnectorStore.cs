using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Connectors;

public class StorageConnectorStore : IStorageConnectorStore
{
    private const string ConnectorType = "STORAGE";
    private const string PasswordKey = "password";
    private const string AccessKey = "accessKey";
    private const string SecretKey = "secretKey";

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<StorageConnectorStore> _logger;

    public StorageConnectorStore(
        IAppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<StorageConnectorStore> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<StorageConnectorModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        if (connector is null)
        {
            return null;
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        var type = config.TryGetValue("type", out var storageType) && !string.IsNullOrWhiteSpace(storageType)
            ? storageType!
            : "Local";

        return new StorageConnectorModel(
            connector.Id,
            connector.Alias,
            connector.Enabled,
            type,
            config.TryGetValue("path", out var path) ? path : null,
            config.TryGetValue("endpoint", out var endpoint) ? endpoint : null,
            config.TryGetValue("bucket", out var bucket) ? bucket : null,
            config.TryGetValue("folder", out var folder) ? folder : null,
            config.TryGetValue("region", out var region) ? region : null,
            config.TryGetValue("username", out var username) ? username : null,
            config.TryGetValue("domain", out var domain) ? domain : null,
            config.TryGetValue("publicUrlBase", out var publicUrlBase) ? publicUrlBase : null,
            TryParseInt(config, "retentionDays"),
            TryParseBool(config, "useSsl"),
            connector.Secrets.Any(s => s.Key == PasswordKey),
            connector.Secrets.Any(s => s.Key == AccessKey || s.Key == SecretKey),
            connector.HealthStatus,
            connector.LastTestedAtUtc);
    }

    public async Task<StorageConnectorModel> SaveAsync(StorageConnectorUpdate update, CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        var actor = ResolveActor();

        var configuration = new Dictionary<string, string?>
        {
            ["type"] = update.Type,
            ["path"] = update.Path,
            ["endpoint"] = update.Endpoint,
            ["bucket"] = update.Bucket,
            ["folder"] = update.Folder,
            ["region"] = update.Region,
            ["username"] = update.Username,
            ["domain"] = update.Domain,
            ["publicUrlBase"] = update.PublicUrlBase,
            ["retentionDays"] = update.RetentionDays?.ToString(CultureInfo.InvariantCulture),
            ["useSsl"] = update.UseSsl?.ToString() ?? string.Empty
        };

        if (connector is null)
        {
            connector = new ConnectorProfile
            {
                Type = ConnectorType,
                Alias = string.IsNullOrWhiteSpace(update.Alias) ? "Artefact Storage" : update.Alias,
                Enabled = update.Enabled,
                ConfigurationJson = JsonSerializer.Serialize(configuration),
                CreatedBy = actor,
                ModifiedBy = actor
            };

            _dbContext.ConnectorProfiles.Add(connector);
        }
        else
        {
            connector.Alias = string.IsNullOrWhiteSpace(update.Alias) ? connector.Alias : update.Alias;
            connector.Enabled = update.Enabled;
            connector.ConfigurationJson = JsonSerializer.Serialize(configuration);
            connector.ModifiedBy = actor;
        }

        if (connector.HealthStatus is not null && !connector.Enabled)
        {
            connector.HealthStatus = "Disabled";
        }

        if (update.RotateSecret)
        {
            connector.Secrets.Clear();
        }

        if (!string.IsNullOrWhiteSpace(update.Password))
        {
            UpsertSecret(connector, PasswordKey, update.Password!);
        }

        if (!string.IsNullOrWhiteSpace(update.AccessKey))
        {
            UpsertSecret(connector, AccessKey, update.AccessKey!);
        }

        if (!string.IsNullOrWhiteSpace(update.SecretKey))
        {
            UpsertSecret(connector, SecretKey, update.SecretKey!);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken))!;
    }

    public async Task<StorageConnectorTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: false, cancellationToken);
        if (connector is null || !connector.Enabled)
        {
            return new StorageConnectorTestResult(false, "Storage connector is not configured or disabled.");
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        var type = config.TryGetValue("type", out var storageType) && !string.IsNullOrWhiteSpace(storageType)
            ? storageType!
            : "Local";

        try
        {
            switch (type.ToUpperInvariant())
            {
                case "LOCAL":
                    return await TestLocalAsync(connector, config, cancellationToken);
                case "SMB":
                case "FTPS":
                case "SFTP":
                case "S3":
                    connector.HealthStatus = "Configuration saved";
                    connector.LastTestedAtUtc = DateTime.UtcNow;
                    connector.ModifiedBy = ResolveActor();
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return new StorageConnectorTestResult(true, "Configuration stored. External connectivity requires environment-specific validation.");
                default:
                    return new StorageConnectorTestResult(false, $"Unsupported storage type '{type}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage connector test failed for type {Type}", type);
            connector.HealthStatus = "Test failed";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new StorageConnectorTestResult(false, $"Storage test failed: {ex.Message}");
        }
    }

    private async Task<StorageConnectorTestResult> TestLocalAsync(ConnectorProfile connector, Dictionary<string, string?> config, CancellationToken cancellationToken)
    {
        if (!config.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path))
        {
            return new StorageConnectorTestResult(false, "Local storage path is missing.");
        }

        var directory = path!;
        var targetDirectory = Directory.Exists(directory) ? directory : Directory.CreateDirectory(directory).FullName;
        var tempFile = Path.Combine(targetDirectory, $"hwinventory-test-{Guid.NewGuid():N}.tmp");

        await File.WriteAllTextAsync(tempFile, "storage connector test", cancellationToken);
        var contents = await File.ReadAllTextAsync(tempFile, cancellationToken);
        File.Delete(tempFile);

        connector.HealthStatus = contents == "storage connector test" ? "Healthy" : "Verification mismatch";
        connector.LastTestedAtUtc = DateTime.UtcNow;
        connector.ModifiedBy = ResolveActor();
        await _dbContext.SaveChangesAsync(cancellationToken);

        var success = connector.HealthStatus == "Healthy";
        var message = success ? "Local storage connectivity verified." : "Unable to verify contents after roundtrip.";
        return new StorageConnectorTestResult(success, message);
    }

    private async Task<ConnectorProfile?> LoadConnectorAsync(bool includeSecrets, CancellationToken cancellationToken)
    {
        var query = _dbContext.ConnectorProfiles.AsQueryable();
        if (includeSecrets)
        {
            query = query.Include(x => x.Secrets);
        }

        return await query.FirstOrDefaultAsync(x => x.Type == ConnectorType, cancellationToken);
    }

    private static Dictionary<string, string?> ParseConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static int? TryParseInt(IReadOnlyDictionary<string, string?> config, string key)
    {
        if (config.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? TryParseBool(IReadOnlyDictionary<string, string?> config, string key)
    {
        if (config.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static void UpsertSecret(ConnectorProfile connector, string key, string secret)
    {
        var existing = connector.Secrets.FirstOrDefault(x => x.Key == key);
        if (existing is null)
        {
            connector.Secrets.Add(new ConnectorSecret
            {
                Key = key,
                SecretReference = secret,
                RotatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.SecretReference = secret;
            existing.RotatedAtUtc = DateTime.UtcNow;
        }
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
