using System.Diagnostics;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HWInventory.Api.Middleware;

public class RequestMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public RequestMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IObservabilityRuntime runtime, IRequestMetricsCollector collector)
    {
        var configuration = await runtime.GetAsync(context.RequestAborted);
        if (!configuration.MetricsEndpointEnabled)
        {
            await _next(context);
            return;
        }

        collector.IncrementActive();
        var stopwatch = Stopwatch.StartNew();
        var statusCode = 200;

        try
        {
            await _next(context);
            statusCode = context.Response.StatusCode == 0 ? 200 : context.Response.StatusCode;
        }
        catch
        {
            statusCode = StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            collector.Record(context.Request.Method ?? "UNKNOWN", statusCode, stopwatch.Elapsed);
        }
    }
}

public static class RequestMetricsMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMetrics(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestMetricsMiddleware>();
    }
}
