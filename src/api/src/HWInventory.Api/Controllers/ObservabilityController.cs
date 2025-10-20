using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using HWInventory.Infrastructure.Observability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ObservabilityManage)]
[Route("api/observability")]
public class ObservabilityController : ApiControllerBase
{
    private readonly IObservabilityConfigurationStore _store;
    private readonly IObservabilityRuntime _runtime;
    private readonly IRequestMetricsCollector _metricsCollector;
    private readonly ISecurityMetricsProvider _securityMetricsProvider;

    public ObservabilityController(
        IAppDbContext dbContext,
        IObservabilityConfigurationStore store,
        IObservabilityRuntime runtime,
        IRequestMetricsCollector metricsCollector,
        ISecurityMetricsProvider securityMetricsProvider)
        : base(dbContext)
    {
        _store = store;
        _runtime = runtime;
        _metricsCollector = metricsCollector;
        _securityMetricsProvider = securityMetricsProvider;
    }

    [HttpGet("config")]
    public async Task<ActionResult<ObservabilityConfigurationResponse>> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _store.GetAsync(cancellationToken);
        return Ok(Map(configuration));
    }

    [HttpPut("config")]
    public async Task<ActionResult<ObservabilityConfigurationResponse>> UpdateConfigurationAsync(
        [FromBody] ObservabilityConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LogLevel>(request.LogLevel, true, out var parsedLevel))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatná hodnota",
                Detail = "Zadaná hodnota log levelu není podporována.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var previous = await _store.GetAsync(cancellationToken);
        var update = new ObservabilityConfigurationUpdate(
            parsedLevel.ToString(),
            request.HealthEndpointEnabled,
            request.MetricsEndpointEnabled,
            request.CorrelationIdsEnabled,
            request.IncludeTraceIdentifier,
            request.OtelExporterEnabled,
            string.IsNullOrWhiteSpace(request.OtelEndpoint) ? null : request.OtelEndpoint,
            string.IsNullOrWhiteSpace(request.OtelAuthToken) ? null : request.OtelAuthToken,
            request.RotateOtelAuthToken || !string.IsNullOrWhiteSpace(request.OtelAuthToken),
            string.IsNullOrWhiteSpace(request.ResourceAttributes) ? null : request.ResourceAttributes);

        var updated = await _store.SaveAsync(update, cancellationToken);
        _runtime.Invalidate();
        ObservabilityLogging.ApplyMinimumLevel(parsedLevel);

        AddAuditLog(
            "Settings.Observability",
            Guid.Empty,
            "Update",
            "Aktualizace observability konfigurace",
            new
            {
                Before = previous,
                After = updated
            });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(updated));
    }

    [HttpGet("metrics/preview")]
    public async Task<ActionResult<string>> GetMetricsPreview(CancellationToken cancellationToken)
    {
        var requestMetrics = _metricsCollector.ExportSnapshot();
        var securitySnapshot = await _securityMetricsProvider.GetSnapshotAsync(cancellationToken);
        var securityMetrics = _securityMetricsProvider.FormatPrometheus(securitySnapshot);
        var combined = string.IsNullOrWhiteSpace(securityMetrics)
            ? requestMetrics
            : string.Concat(requestMetrics, requestMetrics.EndsWith("\n", StringComparison.Ordinal) ? string.Empty : "\n", securityMetrics);

        return Content(combined, "text/plain");
    }

    private static ObservabilityConfigurationResponse Map(ObservabilityConfigurationModel model)
    {
        return new ObservabilityConfigurationResponse
        {
            LogLevel = model.LogLevel,
            HealthEndpointEnabled = model.HealthEndpointEnabled,
            MetricsEndpointEnabled = model.MetricsEndpointEnabled,
            CorrelationIdsEnabled = model.CorrelationIdsEnabled,
            IncludeTraceIdentifier = model.IncludeTraceIdentifier,
            OtelExporterEnabled = model.OtelExporterEnabled,
            OtelEndpoint = model.OtelEndpoint,
            HasOtelAuthToken = model.HasOtelAuthToken,
            ResourceAttributes = model.ResourceAttributes
        };
    }
}
