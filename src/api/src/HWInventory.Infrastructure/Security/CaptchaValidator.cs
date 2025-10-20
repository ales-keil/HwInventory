using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using HWInventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Security;

public class CaptchaValidator : ICaptchaValidator
{
    private readonly IAppDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CaptchaValidator> _logger;

    public CaptchaValidator(IAppDbContext dbContext, IHttpClientFactory httpClientFactory, ILogger<CaptchaValidator> logger)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var settings = await _dbContext.Settings
            .Where(x => x.Section == "Security.Captcha")
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        var isEnabled = settings.TryGetValue("Enabled", out var enabledValue) && bool.TryParse(enabledValue, out var enabled) && enabled;
        if (!isEnabled)
        {
            return true;
        }

        if (settings.TryGetValue("BypassToken", out var bypassToken) && !string.IsNullOrWhiteSpace(bypassToken) &&
            string.Equals(token, bypassToken, StringComparison.Ordinal))
        {
            return true;
        }

        if (!settings.TryGetValue("VerificationEndpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint))
        {
            _logger.LogWarning("Captcha verification endpoint not configured. Rejecting tokens.");
            return false;
        }

        settings.TryGetValue("Secret", out var secret);
        settings.TryGetValue("SiteKey", out var siteKey);

        try
        {
            var client = _httpClientFactory.CreateClient("captcha");
            var response = await client.PostAsJsonAsync(endpoint, new
            {
                token,
                secret,
                siteKey
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Captcha verification failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<CaptchaResponse>(cancellationToken: cancellationToken);
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Captcha verification error");
            return false;
        }
    }

    private sealed record CaptchaResponse(bool Success, IEnumerable<string>? Errors);
}
