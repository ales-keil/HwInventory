using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class LabelPrintJobConfiguration : IEntityTypeConfiguration<LabelPrintJob>
{
    public void Configure(EntityTypeBuilder<LabelPrintJob> builder)
    {
        builder.ToTable("LabelPrintJobs");
        builder.Property(x => x.Target).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.LabelPrintJobId);
    }
}
