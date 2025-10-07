using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HWInventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase("hwinventory"));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));
        }

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        return services;
    }
}
