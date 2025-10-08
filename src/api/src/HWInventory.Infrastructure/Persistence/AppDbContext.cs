using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Generic;
using System.Text.Json;

namespace HWInventory.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid, IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>, IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Server> Servers => Set<Server>();
    public DbSet<NetworkDevice> NetworkDevices => Set<NetworkDevice>();
    public DbSet<Workstation> Workstations => Set<Workstation>();
    public DbSet<DictionaryEntry> DictionaryEntries => Set<DictionaryEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();
    public DbSet<LabelPrintJob> LabelPrintJobs => Set<LabelPrintJob>();
    public new DbSet<AppUser> Users => Set<AppUser>();
    public new DbSet<AppRole> Roles => Set<AppRole>();
    public DbSet<FeatureModule> FeatureModules => Set<FeatureModule>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<ConnectorProfile> ConnectorProfiles => Set<ConnectorProfile>();
    public DbSet<LdapRoleMapping> LdapRoleMappings => Set<LdapRoleMapping>();
    public DbSet<DataScope> DataScopes => Set<DataScope>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<PasswordHistoryEntry> PasswordHistoryEntries => Set<PasswordHistoryEntry>();
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<BackupSchedule> BackupSchedules => Set<BackupSchedule>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var auditLogs = BuildAuditEntries();
        var result = await base.SaveChangesAsync(cancellationToken);

        if (auditLogs.Count > 0)
        {
            AuditLogs.AddRange(auditLogs);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private List<AuditLog> BuildAuditEntries()
    {
        var auditEntries = new List<AuditLog>();
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
            {
                continue;
            }

            var auditLog = new AuditLog
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id,
                PerformedAtUtc = now,
                PerformedBy = entry.Entity.ModifiedBy ?? entry.Entity.CreatedBy,
                Roles = null
            };

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    auditLog.Action = "Create";
                    auditLog.ChangeSummary = "Entity created";
                    auditLog.ChangedFieldsJson = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                    break;
                case EntityState.Modified:
                    entry.Entity.ModifiedAtUtc = now;
                    auditLog.Action = "Update";
                    auditLog.ChangeSummary = "Entity updated";
                    auditLog.ChangedFieldsJson = SerializeChanges(entry);
                    break;
                case EntityState.Deleted:
                    auditLog.Action = "Delete";
                    auditLog.ChangeSummary = "Entity deleted";
                    auditLog.ChangedFieldsJson = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                    break;
            }

            auditEntries.Add(auditLog);
        }

        return auditEntries;
    }

    private static string SerializeChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (!property.IsModified)
            {
                continue;
            }

            changes[property.Metadata.Name] = new
            {
                Original = property.OriginalValue,
                Current = property.CurrentValue
            };
        }

        return JsonSerializer.Serialize(changes);
    }
}
