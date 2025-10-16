using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Connectors;

public class WebhookConnectorStore : IWebhookConnectorStore
{
    private const string ConnectorType = "WEBHOOK";
    private const string SignatureHeader = "X-HWINV-Signature";
    private const string EventHeader = "X-HWINV-Event";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookConnectorStore> _logger;

    public WebhookConnectorStore(
        IAppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookConnectorStore> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<WebhookConnectorModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType, cancellationToken);

        if (connector is null)
        {
            return null;
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);
        var headers = ParseHeaders(configuration.TryGetValue("headers", out var headerValue) ? headerValue : null);
        var useSignature = configuration.TryGetValue("useSignature", out var signatureValue)
            && bool.TryParse(signatureValue, out var signature)
            && signature;

        return new WebhookConnectorModel(
            connector.Id,
            connector.Alias,
            connector.Enabled,
            configuration.TryGetValue("url", out var url) ? url ?? string.Empty : string.Empty,
            configuration.TryGetValue("method", out var method) ? method ?? "POST" : "POST",
            configuration.TryGetValue("contentType", out var contentType) ? contentType : "application/json",
            headers,
            useSignature,
            connector.Secrets.Any(),
            connector.HealthStatus,
            connector.LastTestedAtUtc);
    }

    public async Task<WebhookConnectorModel> SaveAsync(WebhookConnectorUpdate update, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.Url))
        {
            throw new ArgumentException("Webhook URL must be provided.", nameof(update));
        }

        var actor = ResolveActor();
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType, cancellationToken);

        var configuration = new Dictionary<string, string?>
        {
            ["url"] = update.Url,
            ["method"] = string.IsNullOrWhiteSpace(update.Method) ? "POST" : update.Method,
            ["contentType"] = string.IsNullOrWhiteSpace(update.ContentType) ? "application/json" : update.ContentType,
            ["headers"] = SerializeHeaders(update.Headers),
            ["useSignature"] = update.UseSignature.ToString()
        };

        if (connector is null)
        {
            connector = new ConnectorProfile
            {
                Type = ConnectorType,
                Alias = string.IsNullOrWhiteSpace(update.Alias) ? "Primary Webhook" : update.Alias!,
                Enabled = update.Enabled,
                ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions),
                CreatedBy = actor,
                ModifiedBy = actor
            };

            _dbContext.ConnectorProfiles.Add(connector);
        }
        else
        {
            connector.Alias = string.IsNullOrWhiteSpace(update.Alias) ? connector.Alias : update.Alias!;
            connector.Enabled = update.Enabled;
            connector.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
            connector.ModifiedBy = actor;
        }

        if (update.RotateSecret && string.IsNullOrWhiteSpace(update.SigningSecret))
        {
            connector.Secrets.Clear();
        }
        else if (!string.IsNullOrWhiteSpace(update.SigningSecret))
        {
            connector.Secrets.Clear();
            connector.Secrets.Add(new ConnectorSecret
            {
                Key = "signing",
                SecretReference = update.SigningSecret!,
                RotatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken))!;
    }

    public async Task<WebhookTestResult> SendTestAsync(WebhookTestRequest request, CancellationToken cancellationToken = default)
    {
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType && x.Enabled, cancellationToken);

        if (connector is null)
        {
            return new WebhookTestResult(false, "Webhook connector is not configured or disabled.", null);
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);
        if (!configuration.TryGetValue("url", out var url) || string.IsNullOrWhiteSpace(url))
        {
            return new WebhookTestResult(false, "Webhook URL is missing.", null);
        }

        var method = configuration.TryGetValue("method", out var methodValue) && !string.IsNullOrWhiteSpace(methodValue)
            ? methodValue!
            : "POST";
        var contentType = configuration.TryGetValue("contentType", out var contentTypeValue) && !string.IsNullOrWhiteSpace(contentTypeValue)
            ? contentTypeValue!
            : "application/json";
        var headers = ParseHeaders(configuration.TryGetValue("headers", out var headerValue) ? headerValue : null);
        var useSignature = configuration.TryGetValue("useSignature", out var signatureValue) && bool.TryParse(signatureValue, out var signature) && signature;

        var payload = request.PayloadJson ?? JsonSerializer.Serialize(new
        {
            id = Guid.NewGuid(),
            type = string.IsNullOrWhiteSpace(request.EventType) ? "hwinventory.webhook.test" : request.EventType,
            timestampUtc = DateTime.UtcNow,
            message = "Test webhook event from HW Inventory"
        }, SerializerOptions);

        using var httpRequest = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent(payload, Encoding.UTF8, contentType)
        };

        foreach (var header in headers)
        {
            if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                httpRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        httpRequest.Headers.TryAddWithoutValidation(EventHeader, string.IsNullOrWhiteSpace(request.EventType) ? "hwinventory.webhook.test" : request.EventType);

        if (useSignature)
        {
            var secret = connector.Secrets.FirstOrDefault()?.SecretReference;
            if (string.IsNullOrWhiteSpace(secret))
            {
                return new WebhookTestResult(false, "Signing secret is required but not configured.", null);
            }

            var signatureBytes = ComputeSignature(secret!, payload);
            httpRequest.Headers.TryAddWithoutValidation(SignatureHeader, signatureBytes);
        }

        try
        {
            var client = _httpClientFactory.CreateClient("webhook-test");
            var response = await client.SendAsync(httpRequest, cancellationToken);
            var snippet = response.Content is null
                ? null
                : TrimSnippet(await response.Content.ReadAsStringAsync(cancellationToken));

            connector.HealthStatus = response.IsSuccessStatusCode ? "Healthy" : $"Failed ({(int)response.StatusCode})";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new WebhookTestResult(response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Webhook test delivered." : $"Webhook returned status {(int)response.StatusCode}.", snippet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook test failed for {ConnectorId}", connector.Id);
            connector.HealthStatus = "Test failed";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new WebhookTestResult(false, $"Webhook test failed: {ex.Message}", null);
        }
    }

    private static string SerializeHeaders(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.Count == 0)
        {
            return string.Empty;
        }

        var lines = headers
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .Select(kvp => $"{kvp.Key.Trim()}:{kvp.Value?.Trim()}");
        return string.Join("\n", lines);
    }

    private static Dictionary<string, string> ParseHeaders(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        var lines = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var headerValue = line[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = headerValue;
            }
        }

        return result;
    }

    private static Dictionary<string, string?> ParseConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, SerializerOptions)
                ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? TrimSnippet(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= 512 ? value : value[..512];
    }

    private static string ComputeSignature(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
