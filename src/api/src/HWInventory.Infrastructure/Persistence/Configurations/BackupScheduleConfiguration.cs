using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class BackupScheduleConfiguration : IEntityTypeConfiguration<BackupSchedule>
{
    public void Configure(EntityTypeBuilder<BackupSchedule> builder)
    {
        builder.ToTable("BackupSchedules");
        builder.Property(x => x.Frequency).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(32).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.EmailRecipients).HasMaxLength(512);
    }
}
