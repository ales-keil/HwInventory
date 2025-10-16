using System;
using System.ComponentModel.DataAnnotations;
using HWInventory.Application.Abstractions;

namespace HWInventory.Api.Models;

public record PrintingConnectorResponse(
    Guid Id,
    string Alias,
    bool Enabled,
    string Host,
    int Port,
    string QueueType,
    int? TimeoutSeconds,
    int? RetryCount,
    bool HasSecret,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public class PrintingConnectorRequest
{
    public string Alias { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    [Required]
    [MaxLength(200)]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 9100;

    [Required]
    [MaxLength(50)]
    public string QueueType { get; set; } = "RAW";

    [Range(1, 600)]
    public int? TimeoutSeconds { get; set; }

    [Range(0, 10)]
    public int? RetryCount { get; set; }

    public bool RotateSecret { get; set; }

    [MaxLength(256)]
    public string? SharedSecret { get; set; }
}

public record PrintingConnectorTestResponse(bool Success, string Message);

public static class PrintingConnectorMapping
{
    public static PrintingConnectorResponse ToResponse(this PrintingConnectorModel model)
        => new(
            model.Id,
            model.Alias,
            model.Enabled,
            model.Host,
            model.Port,
            model.QueueType,
            model.TimeoutSeconds,
            model.RetryCount,
            model.HasSecret,
            model.HealthStatus,
            model.LastTestedAtUtc);
}
