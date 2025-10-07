using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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

    public static async Task InitializeAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);

        if (!await context.Roles.AnyAsync(cancellationToken))
        {
            context.Roles.AddRange(new[]
            {
                new AppRole { Name = SystemRoleNames.SuperAdmin },
                new AppRole { Name = SystemRoleNames.ServerAdmin },
                new AppRole { Name = SystemRoleNames.NetworkAdmin },
                new AppRole { Name = SystemRoleNames.AppAdmin },
                new AppRole { Name = SystemRoleNames.Helpdesk },
                new AppRole { Name = SystemRoleNames.HelpdeskRead }
            });
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
    }
}
