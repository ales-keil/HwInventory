using System;
using System.Collections.Generic;
using System.Globalization;
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

public class SftpConnectorStore : ISftpConnectorStore
{
    private const string ConnectorType = "SFTP";
    private const string PasswordKey = "password";
    private const string PrivateKey = "privateKey";
    private const string KnownHostsKey = "knownHosts";

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SftpConnectorStore> _logger;

    public SftpConnectorStore(
        IAppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SftpConnectorStore> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<SftpConnectorModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        if (connector is null)
        {
            return null;
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        return new SftpConnectorModel(
            connector.Id,
            connector.Alias,
            connector.Enabled,
            config.GetValueOrDefault("protocol", "SFTP")!,
            config.GetValueOrDefault("host", string.Empty)!,
            TryParseInt(config, "port") ?? 22,
            config.GetValueOrDefault("remotePath"),
            config.GetValueOrDefault("username"),
            TryParseBool(config, "useKeyAuthentication") ?? false,
            TryParseBool(config, "passiveMode") ?? false,
            TryParseNullableBool(config, "useImplicitFtps"),
            TryParseBool(config, "allowUnknownHosts") ?? false,
            connector.Secrets.Any(x => x.Key == PasswordKey),
            connector.Secrets.Any(x => x.Key == PrivateKey),
            connector.HealthStatus,
            connector.LastTestedAtUtc);
    }

    public async Task<SftpConnectorModel> SaveAsync(SftpConnectorUpdate update, CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        var actor = ResolveActor();

        var configuration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["protocol"] = string.IsNullOrWhiteSpace(update.Protocol) ? "SFTP" : update.Protocol,
            ["host"] = update.Host,
            ["port"] = update.Port.ToString(CultureInfo.InvariantCulture),
            ["remotePath"] = update.RemotePath,
            ["username"] = update.Username,
            ["useKeyAuthentication"] = update.UseKeyAuthentication.ToString(),
            ["passiveMode"] = update.PassiveMode.ToString(),
            ["useImplicitFtps"] = update.UseImplicitFtps?.ToString(),
            ["allowUnknownHosts"] = update.AllowUnknownHosts.ToString(),
        };

        if (connector is null)
        {
            connector = new ConnectorProfile
            {
                Type = ConnectorType,
                Alias = string.IsNullOrWhiteSpace(update.Alias) ? "SFTP/FTPS" : update.Alias,
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

        if (update.RotateSecrets)
        {
            connector.Secrets.Clear();
        }

        if (!string.IsNullOrWhiteSpace(update.Password))
        {
            UpsertSecret(connector, PasswordKey, update.Password!);
        }

        if (!string.IsNullOrWhiteSpace(update.PrivateKey))
        {
            UpsertSecret(connector, PrivateKey, update.PrivateKey!);
        }

        if (!string.IsNullOrWhiteSpace(update.KnownHostsFingerprint))
        {
            UpsertSecret(connector, KnownHostsKey, update.KnownHostsFingerprint!);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken))!;
    }

    public async Task<SftpConnectorTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: false, cancellationToken);
        if (connector is null || !connector.Enabled)
        {
            return new SftpConnectorTestResult(false, "SFTP/FTPS connector is not configured or disabled.");
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        var protocol = config.GetValueOrDefault("protocol", "SFTP")!;
        var host = config.GetValueOrDefault("host");
        if (string.IsNullOrWhiteSpace(host))
        {
            return new SftpConnectorTestResult(false, "Host is required to test the connector.");
        }

        try
        {
            connector.HealthStatus = "Configuration stored";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new SftpConnectorTestResult(true, $"Configuration saved for {protocol} connector. External connectivity should be validated in the target environment.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP connector test failed");
            connector.HealthStatus = "Test failed";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new SftpConnectorTestResult(false, $"SFTP/FTPS test failed: {ex.Message}");
        }
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

    private static bool? TryParseNullableBool(IReadOnlyDictionary<string, string?> config, string key)
    {
        if (config.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed))
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

    private static void UpsertSecret(ConnectorProfile connector, string key, string value)
    {
        var secret = connector.Secrets.FirstOrDefault(x => x.Key == key);
        if (secret is null)
        {
            connector.Secrets.Add(new ConnectorSecret
            {
                Key = key,
                SecretReference = value,
                RotatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            secret.SecretReference = value;
            secret.RotatedAtUtc = DateTime.UtcNow;
        }
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
