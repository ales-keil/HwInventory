using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Server> Servers { get; }
    DbSet<NetworkDevice> NetworkDevices { get; }
    DbSet<Workstation> Workstations { get; }
    DbSet<DictionaryEntry> DictionaryEntries { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<LabelTemplate> LabelTemplates { get; }
    DbSet<LabelPrintJob> LabelPrintJobs { get; }
    DbSet<AppUser> Users { get; }
    DbSet<AppRole> Roles { get; }
    DbSet<FeatureModule> FeatureModules { get; }
    DbSet<AppSetting> Settings { get; }
    DbSet<ConnectorProfile> ConnectorProfiles { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
