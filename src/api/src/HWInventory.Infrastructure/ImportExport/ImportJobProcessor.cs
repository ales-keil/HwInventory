using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.ImportExport;

[DisallowConcurrentExecution]
public class ImportJobProcessor : IJob
{
    private readonly IImportService _importService;
    private readonly ILogger<ImportJobProcessor> _logger;

    public ImportJobProcessor(IImportService importService, ILogger<ImportJobProcessor> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Spouštím úlohu importů");
        await _importService.ProcessPendingJobsAsync(context.CancellationToken);
    }
}
