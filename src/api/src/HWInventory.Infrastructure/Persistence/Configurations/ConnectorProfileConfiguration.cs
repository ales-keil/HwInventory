using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class ConnectorProfileConfiguration : IEntityTypeConfiguration<ConnectorProfile>
{
    public void Configure(EntityTypeBuilder<ConnectorProfile> builder)
    {
        builder.ToTable("ConnectorProfiles");
        builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Alias).HasMaxLength(100).IsRequired();
        builder.Property(x => x.HealthStatus).HasMaxLength(50);
        builder.HasMany(x => x.Secrets)
            .WithOne(x => x.ConnectorProfile)
            .HasForeignKey(x => x.ConnectorProfileId);
    }
}
