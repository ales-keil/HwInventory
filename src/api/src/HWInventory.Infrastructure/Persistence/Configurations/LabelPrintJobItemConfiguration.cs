using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class LabelPrintJobItemConfiguration : IEntityTypeConfiguration<LabelPrintJobItem>
{
    public void Configure(EntityTypeBuilder<LabelPrintJobItem> builder)
    {
        builder.ToTable("LabelPrintJobItems");
        builder.Property(x => x.AssetType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Copies).HasDefaultValue(1);
    }
}
