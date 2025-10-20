using System;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.Observability;

[DisallowConcurrentExecution]
public class SecurityMetricsSnapshotJob : IJob
{
    private readonly ISecurityMetricsSnapshotService _snapshotService;
    private readonly ILogger<SecurityMetricsSnapshotJob> _logger;

    public SecurityMetricsSnapshotJob(ISecurityMetricsSnapshotService snapshotService, ILogger<SecurityMetricsSnapshotJob> logger)
    {
        _snapshotService = snapshotService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await _snapshotService.CaptureAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Security metrics snapshot failed");
        }
    }
}
