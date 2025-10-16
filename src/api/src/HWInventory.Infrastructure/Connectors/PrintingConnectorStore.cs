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

public class PrintingConnectorStore : IPrintingConnectorStore
{
    private const string ConnectorType = "PRINTING";
    private const string SharedSecretKey = "sharedSecret";

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PrintingConnectorStore> _logger;

    public PrintingConnectorStore(
        IAppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PrintingConnectorStore> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<PrintingConnectorModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        if (connector is null)
        {
            return null;
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        return new PrintingConnectorModel(
            connector.Id,
            connector.Alias,
            connector.Enabled,
            config.GetValueOrDefault("host", string.Empty)!,
            TryParseInt(config, "port") ?? 9100,
            config.GetValueOrDefault("queueType", "RAW")!,
            TryParseInt(config, "timeoutSeconds"),
            TryParseInt(config, "retryCount"),
            connector.Secrets.Any(x => x.Key == SharedSecretKey),
            connector.HealthStatus,
            connector.LastTestedAtUtc);
    }

    public async Task<PrintingConnectorModel> SaveAsync(PrintingConnectorUpdate update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.Host))
        {
            throw new InvalidOperationException("Host or IP address is required.");
        }

        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        var actor = ResolveActor();

        var configuration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["host"] = update.Host,
            ["port"] = update.Port.ToString(CultureInfo.InvariantCulture),
            ["queueType"] = string.IsNullOrWhiteSpace(update.QueueType) ? "RAW" : update.QueueType,
            ["timeoutSeconds"] = update.TimeoutSeconds?.ToString(CultureInfo.InvariantCulture),
            ["retryCount"] = update.RetryCount?.ToString(CultureInfo.InvariantCulture)
        };

        if (connector is null)
        {
            connector = new ConnectorProfile
            {
                Type = ConnectorType,
                Alias = string.IsNullOrWhiteSpace(update.Alias) ? "Printing" : update.Alias,
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

        if (update.RotateSecret)
        {
            connector.Secrets.RemoveWhere(x => x.Key == SharedSecretKey);
        }

        if (!string.IsNullOrWhiteSpace(update.SharedSecret))
        {
            UpsertSecret(connector, SharedSecretKey, update.SharedSecret!);
        }

        if (connector.HealthStatus is not null && !connector.Enabled)
        {
            connector.HealthStatus = "Disabled";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken))!;
    }

    public async Task<PrintingConnectorTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(includeSecrets: true, cancellationToken);
        if (connector is null)
        {
            return new PrintingConnectorTestResult(false, "Printing connector is not configured.");
        }

        if (!connector.Enabled)
        {
            return new PrintingConnectorTestResult(false, "Printing connector is disabled.");
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        var host = config.GetValueOrDefault("host");
        if (string.IsNullOrWhiteSpace(host))
        {
            return new PrintingConnectorTestResult(false, "Host/IP address must be provided before running a test.");
        }

        try
        {
            connector.HealthStatus = "Configuration stored";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new PrintingConnectorTestResult(true, "Configuration stored. Send a sample ZPL print job in the target environment to validate connectivity.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Printing connector test failed");
            connector.HealthStatus = "Test failed";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new PrintingConnectorTestResult(false, "Printing connector test failed. Review application logs for details.");
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

internal static class ConnectorSecretCollectionExtensions
{
    public static void RemoveWhere(this ICollection<ConnectorSecret> secrets, Func<ConnectorSecret, bool> predicate)
    {
        var toRemove = secrets.Where(predicate).ToList();
        foreach (var item in toRemove)
        {
            secrets.Remove(item);
        }
    }
}
