using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Observability;

public static class ObservabilityLogging
{
    private static LogLevel _minimumLevel = LogLevel.Information;

    public static LogLevel MinimumLevel => Volatile.Read(ref _minimumLevel);

    public static void ApplyMinimumLevel(string? level)
    {
        if (!Enum.TryParse<LogLevel>(level, true, out var parsed))
        {
            parsed = LogLevel.Information;
        }

        ApplyMinimumLevel(parsed);
    }

    public static void ApplyMinimumLevel(LogLevel level)
    {
        Volatile.Write(ref _minimumLevel, level);
    }
}
