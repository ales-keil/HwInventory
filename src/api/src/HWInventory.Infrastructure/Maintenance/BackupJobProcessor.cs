using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.Maintenance;

[DisallowConcurrentExecution]
public class BackupJobProcessor : IJob
{
    private readonly IBackupService _backupService;
    private readonly ILogger<BackupJobProcessor> _logger;

    public BackupJobProcessor(IBackupService backupService, ILogger<BackupJobProcessor> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Spouštím úlohu zpracování záloh");
        await _backupService.QueueScheduledBackupIfDueAsync(context.CancellationToken);
        await _backupService.ProcessPendingJobsAsync(context.CancellationToken);
    }
}
