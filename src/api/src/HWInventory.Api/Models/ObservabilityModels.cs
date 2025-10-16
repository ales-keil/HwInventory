using System.ComponentModel.DataAnnotations;

namespace HWInventory.Api.Models;

public class ObservabilityConfigurationResponse
{
    public string LogLevel { get; set; } = "Information";
    public bool HealthEndpointEnabled { get; set; }
    public bool MetricsEndpointEnabled { get; set; }
    public bool CorrelationIdsEnabled { get; set; }
    public bool IncludeTraceIdentifier { get; set; }
    public bool OtelExporterEnabled { get; set; }
    public string? OtelEndpoint { get; set; }
    public bool HasOtelAuthToken { get; set; }
    public string? ResourceAttributes { get; set; }
}

public class ObservabilityConfigurationRequest
{
    [Required]
    public string LogLevel { get; set; } = "Information";

    public bool HealthEndpointEnabled { get; set; }

    public bool MetricsEndpointEnabled { get; set; }

    public bool CorrelationIdsEnabled { get; set; }

    public bool IncludeTraceIdentifier { get; set; }

    public bool OtelExporterEnabled { get; set; }

    public string? OtelEndpoint { get; set; }

    public string? OtelAuthToken { get; set; }

    public bool RotateOtelAuthToken { get; set; }

    public string? ResourceAttributes { get; set; }
}
