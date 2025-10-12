using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using HWInventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Security;

public class SmsGateway : ISmsGateway
{
    private readonly IAppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsGateway> _logger;

    public SmsGateway(IAppDbContext dbContext, IHttpClientFactory httpClientFactory, ILogger<SmsGateway> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAsync(string destination, string message, CancellationToken cancellationToken = default)
    {
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == "SMS" && x.Enabled, cancellationToken);

        if (connector is null)
        {
            _logger.LogWarning("SMS connector not configured. Message to {Destination} dropped.", destination);
            return;
        }

        try
        {
            var config = connector.ConfigurationJson is null
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(connector.ConfigurationJson) ?? new Dictionary<string, string>();

            config.TryGetValue("endpoint", out var endpoint);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                _logger.LogWarning("SMS connector endpoint missing. Message to {Destination} dropped.", destination);
                return;
            }

            var client = _httpClientFactory.CreateClient("sms");
            var payload = new
            {
                to = destination,
                message,
                sender = config.TryGetValue("sender", out var sender) ? sender : null,
                token = connector.Secrets.FirstOrDefault()?.SecretReference
            };

            var response = await client.PostAsJsonAsync(endpoint, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SMS send failed for {Destination} with status {Status}", destination, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS send error for {Destination}", destination);
        }
    }
}
