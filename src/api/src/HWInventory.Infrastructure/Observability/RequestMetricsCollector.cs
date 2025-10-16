using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using HWInventory.Application.Abstractions;

namespace HWInventory.Infrastructure.Observability;

public class RequestMetricsCollector : IRequestMetricsCollector
{
    private readonly ConcurrentDictionary<string, long> _methodTotals = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, long> _statusTotals = new();
    private readonly ConcurrentDictionary<string, long> _durationBuckets = new();

    private long _totalRequests;
    private long _activeRequests;
    private long _failedRequests;
    private readonly DateTime _processStartUtc = DateTime.UtcNow;

    public void IncrementActive()
    {
        Interlocked.Increment(ref _activeRequests);
    }

    public void Record(string method, int statusCode, TimeSpan duration)
    {
        Interlocked.Decrement(ref _activeRequests);
        Interlocked.Increment(ref _totalRequests);

        if (statusCode >= 500)
        {
            Interlocked.Increment(ref _failedRequests);
        }

        _methodTotals.AddOrUpdate(method.ToUpperInvariant(), 1, static (_, current) => current + 1);
        _statusTotals.AddOrUpdate(statusCode, 1, static (_, current) => current + 1);
        _durationBuckets.AddOrUpdate(GetDurationBucket(duration), 1, static (_, current) => current + 1);
    }

    public string ExportSnapshot()
    {
        var sb = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;

        sb.AppendLine("# HELP hwinventory_http_requests_total Total HTTP requests processed by the API.");
        sb.AppendLine("# TYPE hwinventory_http_requests_total counter");
        sb.AppendLine($"hwinventory_http_requests_total {Interlocked.Read(ref _totalRequests).ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_http_requests_active Active HTTP requests being processed.");
        sb.AppendLine("# TYPE hwinventory_http_requests_active gauge");
        sb.AppendLine($"hwinventory_http_requests_active {Interlocked.Read(ref _activeRequests).ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_http_requests_failed_total Total failed HTTP requests (5xx).");
        sb.AppendLine("# TYPE hwinventory_http_requests_failed_total counter");
        sb.AppendLine($"hwinventory_http_requests_failed_total {Interlocked.Read(ref _failedRequests).ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_http_requests_by_method_total Total HTTP requests by verb.");
        sb.AppendLine("# TYPE hwinventory_http_requests_by_method_total counter");
        foreach (var method in _methodTotals.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"hwinventory_http_requests_by_method_total{{method=\"{method.Key.ToUpperInvariant()}\"}} {method.Value.ToString(culture)}");
        }

        sb.AppendLine("# HELP hwinventory_http_requests_by_status_total Total HTTP requests by status code.");
        sb.AppendLine("# TYPE hwinventory_http_requests_by_status_total counter");
        foreach (var status in _statusTotals.OrderBy(kvp => kvp.Key))
        {
            sb.AppendLine($"hwinventory_http_requests_by_status_total{{status=\"{status.Key}\"}} {status.Value.ToString(culture)}");
        }

        sb.AppendLine("# HELP hwinventory_http_request_duration_bucket Request duration histogram buckets.");
        sb.AppendLine("# TYPE hwinventory_http_request_duration_bucket counter");
        foreach (var bucket in GetOrderedBuckets())
        {
            if (_durationBuckets.TryGetValue(bucket, out var value))
            {
                sb.AppendLine($"hwinventory_http_request_duration_bucket{{bucket=\"{bucket}\"}} {value.ToString(culture)}");
            }
        }

        var uptimeSeconds = (DateTime.UtcNow - _processStartUtc).TotalSeconds;
        sb.AppendLine("# HELP hwinventory_process_uptime_seconds Application uptime in seconds.");
        sb.AppendLine("# TYPE hwinventory_process_uptime_seconds gauge");
        sb.AppendLine($"hwinventory_process_uptime_seconds {uptimeSeconds.ToString(culture)}");

        return sb.ToString();
    }

    private static string GetDurationBucket(TimeSpan duration)
    {
        var milliseconds = duration.TotalMilliseconds;
        if (milliseconds < 100)
        {
            return "lt_100ms";
        }

        if (milliseconds < 500)
        {
            return "lt_500ms";
        }

        if (milliseconds < 1000)
        {
            return "lt_1000ms";
        }

        if (milliseconds < 5000)
        {
            return "lt_5000ms";
        }

        return "gte_5000ms";
    }

    private static IEnumerable<string> GetOrderedBuckets()
    {
        yield return "lt_100ms";
        yield return "lt_500ms";
        yield return "lt_1000ms";
        yield return "lt_5000ms";
        yield return "gte_5000ms";
    }
}
