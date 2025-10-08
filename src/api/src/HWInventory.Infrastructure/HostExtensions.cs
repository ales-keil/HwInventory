using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Observability;
using HWInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HWInventory.Infrastructure;

public static class HostExtensions
{
    public static async Task<IHost> InitializeDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        var context = serviceProvider.GetRequiredService<AppDbContext>();

        if (context.Database.IsRelational())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        await SeedData.InitializeAsync(serviceProvider, cancellationToken);

        var observabilityStore = serviceProvider.GetRequiredService<IObservabilityConfigurationStore>();
        var configuration = await observabilityStore.GetAsync(cancellationToken);
        ObservabilityLogging.ApplyMinimumLevel(configuration.LogLevel);
        return host;
    }
}
