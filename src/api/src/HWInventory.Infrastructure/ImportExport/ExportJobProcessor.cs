using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.ImportExport;

[DisallowConcurrentExecution]
public class ExportJobProcessor : IJob
{
    private readonly IExportService _exportService;
    private readonly ILogger<ExportJobProcessor> _logger;

    public ExportJobProcessor(IExportService exportService, ILogger<ExportJobProcessor> logger)
    {
        _exportService = exportService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Spouštím úlohu exportu");
        await _exportService.ProcessPendingJobsAsync(context.CancellationToken);
    }
}
