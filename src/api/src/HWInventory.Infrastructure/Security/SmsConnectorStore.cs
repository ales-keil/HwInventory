using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Security;

public class SmsConnectorStore : ISmsConnectorStore
{
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SmsConnectorStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<SmsConnectorModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == "SMS", cancellationToken);

        if (connector is null)
        {
            return null;
        }

        var config = ParseConfiguration(connector.ConfigurationJson);
        var hasSecret = connector.Secrets.Any();

        return new SmsConnectorModel(
            connector.Id,
            connector.Alias,
            connector.Enabled,
            config.TryGetValue("endpoint", out var endpoint) ? endpoint ?? string.Empty : string.Empty,
            config.TryGetValue("sender", out var sender) ? sender : null,
            config.TryGetValue("region", out var region) ? region : null,
            hasSecret,
            connector.HealthStatus,
            connector.LastTestedAtUtc);
    }

    public async Task<SmsConnectorModel> SaveAsync(SmsConnectorUpdate update, CancellationToken cancellationToken = default)
    {
        var actor = ResolveActor();
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == "SMS", cancellationToken);

        var config = new Dictionary<string, string?>
        {
            ["endpoint"] = update.Endpoint,
            ["sender"] = update.Sender,
            ["region"] = update.Region
        };

        if (connector is null)
        {
            connector = new ConnectorProfile
            {
                Type = "SMS",
                Alias = string.IsNullOrWhiteSpace(update.Alias) ? "Primary SMS" : update.Alias,
                Enabled = update.Enabled,
                ConfigurationJson = JsonSerializer.Serialize(config),
                CreatedBy = actor,
                ModifiedBy = actor
            };

            _dbContext.ConnectorProfiles.Add(connector);
        }
        else
        {
            connector.Alias = string.IsNullOrWhiteSpace(update.Alias) ? connector.Alias : update.Alias;
            connector.Enabled = update.Enabled;
            connector.ConfigurationJson = JsonSerializer.Serialize(config);
            connector.ModifiedBy = actor;
        }

        if (update.RotateSecret && string.IsNullOrWhiteSpace(update.Secret))
        {
            connector.Secrets.Clear();
        }
        else if (!string.IsNullOrWhiteSpace(update.Secret))
        {
            connector.Secrets.Clear();
            connector.Secrets.Add(new ConnectorSecret
            {
                Key = "token",
                SecretReference = update.Secret!,
                RotatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken))!;
    }

    private static Dictionary<string, string?> ParseConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
