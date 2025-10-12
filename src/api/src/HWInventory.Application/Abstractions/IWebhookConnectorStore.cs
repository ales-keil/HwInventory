using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IWebhookConnectorStore
{
    Task<WebhookConnectorModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<WebhookConnectorModel> SaveAsync(WebhookConnectorUpdate update, CancellationToken cancellationToken = default);
    Task<WebhookTestResult> SendTestAsync(WebhookTestRequest request, CancellationToken cancellationToken = default);
}

public record WebhookConnectorModel(
    Guid Id,
    string Alias,
    bool Enabled,
    string Url,
    string Method,
    string? ContentType,
    IReadOnlyDictionary<string, string> Headers,
    bool UseSignature,
    bool HasSecret,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public record WebhookConnectorUpdate(
    string? Alias,
    bool Enabled,
    string Url,
    string Method,
    string? ContentType,
    IReadOnlyDictionary<string, string> Headers,
    bool UseSignature,
    string? SigningSecret,
    bool RotateSecret);

public record WebhookTestRequest(string? EventType, string? PayloadJson);

public record WebhookTestResult(bool Success, string Message, string? ResponseSnippet);
