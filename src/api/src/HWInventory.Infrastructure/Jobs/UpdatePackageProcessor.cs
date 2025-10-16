using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.Jobs;

public class UpdatePackageProcessor : IJob
{
    private readonly IUpdateService _updateService;
    private readonly ILogger<UpdatePackageProcessor> _logger;

    public UpdatePackageProcessor(IUpdateService updateService, ILogger<UpdatePackageProcessor> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Spouštím kontrolu fronty aktualizačních balíčků");
        await _updateService.ProcessPendingPackagesAsync(context.CancellationToken);
    }
}
