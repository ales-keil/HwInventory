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
    DbSet<DataScope> DataScopes { get; }
    DbSet<FeatureModule> FeatureModules { get; }
    DbSet<AppSetting> Settings { get; }
    DbSet<ConnectorProfile> ConnectorProfiles { get; }
    DbSet<LdapRoleMapping> LdapRoleMappings { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<PasswordHistoryEntry> PasswordHistoryEntries { get; }
    DbSet<BackupJob> BackupJobs { get; }
    DbSet<BackupSchedule> BackupSchedules { get; }
    DbSet<ExportJob> ExportJobs { get; }
    DbSet<ImportJob> ImportJobs { get; }
    DbSet<UpdatePackage> UpdatePackages { get; }
    DbSet<ReportDefinition> ReportDefinitions { get; }
    DbSet<ReportRun> ReportRuns { get; }
    DbSet<WorkstationHandoverRequest> WorkstationHandovers { get; }
    DbSet<SecurityMetricSnapshot> SecurityMetricSnapshots { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
