using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.Reports;

[DisallowConcurrentExecution]
public class ReportJobProcessor : IJob
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportJobProcessor> _logger;

    public ReportJobProcessor(IReportService reportService, ILogger<ReportJobProcessor> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Spouštím plánované reporty");
        await _reportService.ProcessDueReportsAsync(context.CancellationToken);
    }
}
