using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("ReportDefinitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.FilterJson).HasMaxLength(4000);
        builder.Property(x => x.Recipients).HasMaxLength(500);
        builder.Property(x => x.StoragePath).HasMaxLength(500);
        builder.HasMany(x => x.Runs)
            .WithOne(x => x.ReportDefinition)
            .HasForeignKey(x => x.ReportDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
