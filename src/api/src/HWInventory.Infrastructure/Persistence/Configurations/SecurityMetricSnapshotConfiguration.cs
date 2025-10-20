using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class SecurityMetricSnapshotConfiguration : IEntityTypeConfiguration<SecurityMetricSnapshot>
{
    public void Configure(EntityTypeBuilder<SecurityMetricSnapshot> builder)
    {
        builder.ToTable("SecurityMetricSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CapturedAtUtc).IsRequired();
        builder.Property(x => x.AlertsJson).HasMaxLength(2000);
    }
}
