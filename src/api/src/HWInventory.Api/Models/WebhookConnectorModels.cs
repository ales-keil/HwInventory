using System;
using System.Collections.Generic;

namespace HWInventory.Api.Models;

public record WebhookConnectorResponse(
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

public class WebhookConnectorUpdateRequest
{
    public string? Alias { get; set; }
    public bool Enabled { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public string? ContentType { get; set; } = "application/json";
    public Dictionary<string, string>? Headers { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
    public bool UseSignature { get; set; }
    public string? SigningSecret { get; set; }
    public bool RotateSecret { get; set; }
}

public record WebhookTestRequestDto(string? EventType, string? PayloadJson);

public record WebhookTestResponse(bool Success, string Message, string? ResponseSnippet);
