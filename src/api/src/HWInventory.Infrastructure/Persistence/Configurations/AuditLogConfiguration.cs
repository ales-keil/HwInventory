using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(x => x.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PerformedBy).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Roles).HasMaxLength(200);
        builder.Property(x => x.ChangeSummary).HasMaxLength(400);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(400);
    }
}
