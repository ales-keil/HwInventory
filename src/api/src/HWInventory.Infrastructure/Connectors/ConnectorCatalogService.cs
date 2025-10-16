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

public class ConnectorCatalogService : IConnectorCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppDbContext _dbContext;
    private readonly IEmailConnectorStore _emailConnectorStore;
    private readonly ILdapConfigurationStore _ldapConfigurationStore;
    private readonly IExternalIdentityService _externalIdentityService;
    private readonly ISmsGateway _smsGateway;
    private readonly IStorageConnectorStore _storageConnectorStore;
    private readonly ISftpConnectorStore _sftpConnectorStore;
    private readonly IPrintingConnectorStore _printingConnectorStore;
    private readonly IWebhookConnectorStore _webhookConnectorStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ConnectorCatalogService> _logger;

    public ConnectorCatalogService(
        IAppDbContext dbContext,
        IEmailConnectorStore emailConnectorStore,
        ILdapConfigurationStore ldapConfigurationStore,
        IExternalIdentityService externalIdentityService,
        ISmsGateway smsGateway,
        IStorageConnectorStore storageConnectorStore,
        ISftpConnectorStore sftpConnectorStore,
        IPrintingConnectorStore printingConnectorStore,
        IWebhookConnectorStore webhookConnectorStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ConnectorCatalogService> logger)
    {
        _dbContext = dbContext;
        _emailConnectorStore = emailConnectorStore;
        _ldapConfigurationStore = ldapConfigurationStore;
        _externalIdentityService = externalIdentityService;
        _smsGateway = smsGateway;
        _storageConnectorStore = storageConnectorStore;
        _sftpConnectorStore = sftpConnectorStore;
        _printingConnectorStore = printingConnectorStore;
        _webhookConnectorStore = webhookConnectorStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<ConnectorSummary>> GetAsync(CancellationToken cancellationToken = default)
    {
        var summaries = new List<ConnectorSummary>();

        var connectors = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Alias)
            .ToListAsync(cancellationToken);

        summaries.AddRange(connectors.Select(MapConnector));

        var ldapConfig = await _ldapConfigurationStore.GetAsync(cancellationToken);
        summaries.Add(MapLdap(ldapConfig));

        var oidcConfig = await _externalIdentityService.GetConfigurationAsync(cancellationToken);
        summaries.Add(MapOidc(oidcConfig));

        return summaries;
    }

    public async Task<ConnectorSummary?> ToggleAsync(Guid connectorId, bool enabled, CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(connectorId, includeSecrets: false, cancellationToken);
        if (connector is null)
        {
            return null;
        }

        connector.Enabled = enabled;
        connector.ModifiedBy = ResolveActor();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapConnector(connector);
    }

    public async Task<ConnectorSummary?> RotateSecretsAsync(Guid connectorId, CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(connectorId, includeSecrets: true, cancellationToken);
        if (connector is null)
        {
            return null;
        }

        if (connector.Secrets.Count == 0)
        {
            return MapConnector(connector);
        }

        connector.Secrets.Clear();
        connector.ModifiedBy = ResolveActor();
        connector.HealthStatus = "Secret rotation required";
        connector.LastTestedAtUtc = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapConnector(connector);
    }

    public async Task<ConnectorTestOutcome> TestAsync(Guid connectorId, ConnectorTestRequest request, CancellationToken cancellationToken = default)
    {
        var connector = await LoadConnectorAsync(connectorId, includeSecrets: true, cancellationToken);
        if (connector is null)
        {
            return new ConnectorTestOutcome(new ConnectorTestResult(false, "Connector not found."), null);
        }

        if (!connector.Enabled)
        {
            return new ConnectorTestOutcome(new ConnectorTestResult(false, "Connector is disabled."), MapConnector(connector));
        }

        switch (connector.Type.ToUpperInvariant())
        {
            case "SMTP":
                return await ExecuteSmtpTestAsync(connector, request, cancellationToken);
            case "SMS":
                return await ExecuteSmsTestAsync(connector, request, cancellationToken);
            case "WEBHOOK":
                return await ExecuteWebhookTestAsync(connector, cancellationToken);
            case "STORAGE":
                return await ExecuteStorageTestAsync(connector, cancellationToken);
            case "SFTP":
                return await ExecuteSftpTestAsync(connector, cancellationToken);
            case "PRINTING":
                return await ExecutePrintingTestAsync(connector, cancellationToken);
            default:
                return new ConnectorTestOutcome(
                    new ConnectorTestResult(false, $"Testing is not implemented for connector type '{connector.Type}'."),
                    MapConnector(connector));
        }
    }

    private async Task<ConnectorTestOutcome> ExecuteSmtpTestAsync(ConnectorProfile connector, ConnectorTestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return new ConnectorTestOutcome(new ConnectorTestResult(false, "Recipient e-mail address is required."), MapConnector(connector));
        }

        var subject = "HW Inventory SMTP test";
        var body = $"This is an automated SMTP connectivity test triggered at {DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture)}.";
        var result = await _emailConnectorStore.SendTestAsync(new EmailTestRequest(request.Target!, subject, body), cancellationToken);

        var refreshed = await LoadConnectorAsync(connector.Id, includeSecrets: true, cancellationToken);
        return new ConnectorTestOutcome(new ConnectorTestResult(result.Success, result.Message), refreshed is null ? null : MapConnector(refreshed));
    }

    private async Task<ConnectorTestOutcome> ExecuteSmsTestAsync(ConnectorProfile connector, ConnectorTestRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return new ConnectorTestOutcome(new ConnectorTestResult(false, "Destination phone number is required."), MapConnector(connector));
        }

        try
        {
            var message = $"HW Inventory SMS test @ {DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture)}";
            await _smsGateway.SendAsync(request.Target!, message, cancellationToken);
            connector.HealthStatus = "Test dispatched";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new ConnectorTestOutcome(new ConnectorTestResult(true, "SMS test dispatched."), MapConnector(connector));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS connector test failed for {ConnectorId}", connector.Id);
            connector.HealthStatus = "Test failed";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            connector.ModifiedBy = ResolveActor();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new ConnectorTestOutcome(new ConnectorTestResult(false, "SMS test failed. See logs for details."), MapConnector(connector));
        }
    }

    private async Task<ConnectorTestOutcome> ExecuteWebhookTestAsync(ConnectorProfile connector, CancellationToken cancellationToken)
    {
        var result = await _webhookConnectorStore.SendTestAsync(new WebhookTestRequest("connector.test", null), cancellationToken);
        var refreshed = await LoadConnectorAsync(connector.Id, includeSecrets: true, cancellationToken);
        return new ConnectorTestOutcome(
            new ConnectorTestResult(result.Success, result.Message + (string.IsNullOrWhiteSpace(result.ResponseSnippet) ? string.Empty : $" Response: {result.ResponseSnippet}")),
            refreshed is null ? null : MapConnector(refreshed));
    }

    private async Task<ConnectorTestOutcome> ExecuteStorageTestAsync(ConnectorProfile connector, CancellationToken cancellationToken)
    {
        var result = await _storageConnectorStore.TestAsync(cancellationToken);
        var refreshed = await LoadConnectorAsync(connector.Id, includeSecrets: true, cancellationToken);
        return new ConnectorTestOutcome(
            new ConnectorTestResult(result.Success, result.Message),
            refreshed is null ? null : MapConnector(refreshed));
    }

    private async Task<ConnectorTestOutcome> ExecutePrintingTestAsync(ConnectorProfile connector, CancellationToken cancellationToken)
    {
        var result = await _printingConnectorStore.TestAsync(cancellationToken);
        var refreshed = await LoadConnectorAsync(connector.Id, includeSecrets: true, cancellationToken);
        return new ConnectorTestOutcome(
            new ConnectorTestResult(result.Success, result.Message),
            refreshed is null ? null : MapConnector(refreshed));
    }

    private ConnectorSummary MapConnector(ConnectorProfile profile)
    {
        var configuration = ParseConfiguration(profile.ConfigurationJson);
        var description = profile.Type.ToUpperInvariant() switch
        {
            "SMTP" => DescribeSmtp(configuration),
            "SMS" => DescribeSms(configuration),
            "SFTP" => DescribeSftp(configuration),
            "WEBHOOK" or "WEBHOOKS" => configuration.TryGetValue("url", out var url) ? url : null,
            "STORAGE" => DescribeStorage(configuration),
            _ => configuration.TryGetValue("endpoint", out var generic) ? generic : null
        };

        var requiresConfiguration = profile.Type.ToUpperInvariant() switch
        {
            "SMTP" => !configuration.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host),
            "SMS" => !configuration.TryGetValue("endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint),
            "SFTP" => !configuration.TryGetValue("host", out var sftpHost) || string.IsNullOrWhiteSpace(sftpHost),
            "STORAGE" => RequiresStorageConfiguration(configuration),
            _ => string.IsNullOrWhiteSpace(profile.ConfigurationJson)
        };

        return new ConnectorSummary(
            profile.Id,
            $"connector:{profile.Id}",
            profile.Type,
            profile.Alias,
            profile.Enabled,
            profile.HealthStatus,
            profile.LastTestedAtUtc,
            profile.Secrets.Any(),
            supportsToggle: true,
            supportsTest: SupportsTest(profile.Type),
            supportsRotateSecret: profile.Secrets.Any() || SupportsSecretRotation(profile.Type),
            requiresConfiguration,
            description);
    }

    private ConnectorSummary MapLdap(LdapConfigurationModel configuration)
    {
        var description = string.IsNullOrWhiteSpace(configuration.Host)
            ? "LDAP server host is not configured"
            : $"{configuration.Host}:{configuration.Port}";

        return new ConnectorSummary(
            null,
            "connector:ldap",
            "LDAP",
            "LDAP / Active Directory",
            configuration.Enabled,
            null,
            null,
            configuration.HasPassword,
            supportsToggle: false,
            supportsTest: false,
            supportsRotateSecret: false,
            string.IsNullOrWhiteSpace(configuration.Host),
            description);
    }

    private ConnectorSummary MapOidc(OidcConfiguration? configuration)
    {
        if (configuration is null)
        {
            return new ConnectorSummary(
                null,
                "connector:oidc",
                "OIDC",
                "OpenID Connect",
                false,
                null,
                null,
                false,
                supportsToggle: false,
                supportsTest: false,
                supportsRotateSecret: false,
                true,
                "OIDC has not been configured");
        }

        var description = string.IsNullOrWhiteSpace(configuration.Authority)
            ? "OIDC authority missing"
            : configuration.Authority;

        return new ConnectorSummary(
            null,
            "connector:oidc",
            "OIDC",
            "OpenID Connect",
            configuration.Enabled,
            null,
            null,
            !string.IsNullOrWhiteSpace(configuration.ClientSecret),
            supportsToggle: false,
            supportsTest: false,
            supportsRotateSecret: false,
            string.IsNullOrWhiteSpace(configuration.Authority),
            description);
    }

    private async Task<ConnectorProfile?> LoadConnectorAsync(Guid connectorId, bool includeSecrets, CancellationToken cancellationToken)
    {
        var query = _dbContext.ConnectorProfiles.AsQueryable();
        if (includeSecrets)
        {
            query = query.Include(x => x.Secrets);
        }

        return await query.FirstOrDefaultAsync(x => x.Id == connectorId, cancellationToken);
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

    private static string? DescribeSmtp(IReadOnlyDictionary<string, string?> configuration)
    {
        configuration.TryGetValue("host", out var host);
        configuration.TryGetValue("port", out var port);
        if (string.IsNullOrWhiteSpace(host))
        {
            return "SMTP host not configured";
        }

        return string.IsNullOrWhiteSpace(port) ? host : $"{host}:{port}";
    }

    private static string? DescribeSms(IReadOnlyDictionary<string, string?> configuration)
    {
        configuration.TryGetValue("endpoint", out var endpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "SMS endpoint not configured";
        }

        configuration.TryGetValue("sender", out var sender);
        return string.IsNullOrWhiteSpace(sender) ? endpoint : $"{endpoint} (sender {sender})";
    }

    private static string? DescribeStorage(IReadOnlyDictionary<string, string?> configuration)
    {
        configuration.TryGetValue("type", out var type);
        var storageType = type?.ToUpperInvariant();

        return storageType switch
        {
            "LOCAL" => configuration.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path) ? path : "Local path not configured",
            "SMB" => configuration.TryGetValue("path", out var share) && !string.IsNullOrWhiteSpace(share) ? share : "UNC path not configured",
            "S3" => configuration.TryGetValue("bucket", out var bucket) && !string.IsNullOrWhiteSpace(bucket) ? $"S3 bucket {bucket}" : "S3 bucket not configured",
            _ => configuration.TryGetValue("path", out var generic) ? generic : null
        };
    }

    private static string? DescribeSftp(IReadOnlyDictionary<string, string?> configuration)
    {
        configuration.TryGetValue("host", out var host);
        configuration.TryGetValue("port", out var port);
        configuration.TryGetValue("protocol", out var protocol);

        if (string.IsNullOrWhiteSpace(host))
        {
            return "SFTP/FTPS host not configured";
        }

        var address = string.IsNullOrWhiteSpace(port) ? host : $"{host}:{port}";
        return string.IsNullOrWhiteSpace(protocol) ? address : $"{protocol} - {address}";
    }

    private static bool RequiresStorageConfiguration(IReadOnlyDictionary<string, string?> configuration)
    {
        if (!configuration.TryGetValue("type", out var type) || string.IsNullOrWhiteSpace(type))
        {
            return true;
        }

        switch (type.ToUpperInvariant())
        {
            case "LOCAL":
                return !configuration.TryGetValue("path", out var path) || string.IsNullOrWhiteSpace(path);
            case "SMB":
                return !configuration.TryGetValue("path", out var share) || string.IsNullOrWhiteSpace(share);
            case "SFTP":
            case "FTPS":
                return !configuration.TryGetValue("endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint);
            case "S3":
                var missingBucket = !configuration.TryGetValue("bucket", out var bucket) || string.IsNullOrWhiteSpace(bucket);
                var missingEndpoint = !configuration.TryGetValue("endpoint", out var s3Endpoint) || string.IsNullOrWhiteSpace(s3Endpoint);
                return missingBucket || missingEndpoint;
            default:
                return true;
        }
    }

    private static bool SupportsTest(string type)
    {
        return type.Equals("SMTP", StringComparison.OrdinalIgnoreCase)
            || type.Equals("SMS", StringComparison.OrdinalIgnoreCase)
            || type.Equals("WEBHOOK", StringComparison.OrdinalIgnoreCase)
            || type.Equals("STORAGE", StringComparison.OrdinalIgnoreCase)
            || type.Equals("SFTP", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SupportsSecretRotation(string type)
    {
        return type.Equals("SMTP", StringComparison.OrdinalIgnoreCase)
            || type.Equals("SMS", StringComparison.OrdinalIgnoreCase)
            || type.Equals("SFTP", StringComparison.OrdinalIgnoreCase)
            || type.Equals("FTPS", StringComparison.OrdinalIgnoreCase)
            || type.Equals("STORAGE", StringComparison.OrdinalIgnoreCase)
            || type.Equals("PRINTING", StringComparison.OrdinalIgnoreCase)
            || type.Equals("WEBHOOK", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
