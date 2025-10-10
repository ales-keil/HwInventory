using System;
using System.Linq;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HWInventory.Infrastructure.Persistence;

public static class SeedData
{
    private static readonly (string Dict, string Key, string Value)[] DictionarySeeds =
    {
        ("Environment", "PROD", "Production"),
        ("Environment", "TEST", "Testing"),
        ("Environment", "DEV", "Development"),
        ("WsusPriority", "HIGH", "Vysoká"),
        ("WsusPriority", "NORMAL", "Standard"),
        ("OperatingSystem", "WIN2022", "Windows Server 2022"),
        ("OperatingSystem", "WIN11", "Windows 11"),
        ("ServerRole", "APP", "Application Server"),
        ("ServerRole", "DB", "Database Server"),
        ("WorkstationType", "PC", "Desktop"),
        ("WorkstationType", "LAPTOP", "Notebook"),
        ("Location", "HQ-1F", "HQ 1st Floor"),
        ("Location", "HQ-2F", "HQ 2nd Floor"),
        ("VLAN", "VLAN10", "Production 10"),
        ("VLAN", "VLAN20", "DMZ 20"),
    };

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (!await roleManager.Roles.AnyAsync(cancellationToken))
        {
            var roles = new[]
            {
                new AppRole { Name = SystemRoleNames.SuperAdmin, NormalizedName = SystemRoleNames.SuperAdmin.ToUpperInvariant() },
                new AppRole { Name = SystemRoleNames.ServerAdmin, NormalizedName = SystemRoleNames.ServerAdmin.ToUpperInvariant() },
                new AppRole { Name = SystemRoleNames.NetworkAdmin, NormalizedName = SystemRoleNames.NetworkAdmin.ToUpperInvariant() },
                new AppRole { Name = SystemRoleNames.AppAdmin, NormalizedName = SystemRoleNames.AppAdmin.ToUpperInvariant() },
                new AppRole { Name = SystemRoleNames.Helpdesk, NormalizedName = SystemRoleNames.Helpdesk.ToUpperInvariant() },
                new AppRole { Name = SystemRoleNames.HelpdeskRead, NormalizedName = SystemRoleNames.HelpdeskRead.ToUpperInvariant() }
            };

            foreach (var role in roles)
            {
                await roleManager.CreateAsync(role);
            }
        }

        foreach (var seed in DictionarySeeds)
        {
            if (!await context.DictionaryEntries.AnyAsync(x => x.DictType == seed.Dict && x.Key == seed.Key, cancellationToken))
            {
                context.DictionaryEntries.Add(new DictionaryEntry
                {
                    DictType = seed.Dict,
                    Key = seed.Key,
                    Value = seed.Value,
                    CreatedAtUtc = DateTime.UtcNow,
                    CreatedBy = "seed"
                });
            }
        }

        if (!await context.FeatureModules.AnyAsync(cancellationToken))
        {
            context.FeatureModules.AddRange(new[]
            {
                new FeatureModule { Key = "labels", Name = "Labels & Printing", Enabled = true, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "seed" },
                new FeatureModule { Key = "reports", Name = "Reports & Schedules", Enabled = false, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "seed" },
                new FeatureModule { Key = "zabbix", Name = "Zabbix Metrics", Enabled = false, CreatedAtUtc = DateTime.UtcNow, CreatedBy = "seed" }
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        if (!await userManager.Users.AnyAsync(cancellationToken))
        {
            var adminEmail = configuration["SeedAdmin:Email"] ?? "admin@localhost";
            var adminPassword = configuration["SeedAdmin:Password"] ?? "ChangeMe!123!";

            var adminUser = new AppUser
            {
                UserName = adminEmail,
                NormalizedUserName = adminEmail.ToUpperInvariant(),
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                DisplayName = "Administrator",
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "seed",
                Status = EntityStatus.Active
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create initial admin account: {errors}");
            }

            await userManager.AddToRoleAsync(adminUser, SystemRoleNames.SuperAdmin);
        }
    }
}
