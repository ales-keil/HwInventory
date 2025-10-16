using System;

namespace HWInventory.Application.Abstractions;

public interface IRequestMetricsCollector
{
    void IncrementActive();
    void Record(string method, int statusCode, TimeSpan duration);
    string ExportSnapshot();
}
