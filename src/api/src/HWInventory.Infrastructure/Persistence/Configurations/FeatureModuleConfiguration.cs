using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class FeatureModuleConfiguration : IEntityTypeConfiguration<FeatureModule>
{
    public void Configure(EntityTypeBuilder<FeatureModule> builder)
    {
        builder.ToTable("FeatureModules");
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}
