using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");
        builder.Property(x => x.StoragePath).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StoredFilePath).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.MappingJson).HasMaxLength(2048);
        builder.Property(x => x.ResultLog).HasMaxLength(2048);
        builder.Property(x => x.FailureReason).HasMaxLength(1024);
        builder.Property(x => x.EmailRecipients).HasMaxLength(512);
    }
}
