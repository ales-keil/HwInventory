using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.Property(x => x.StoragePath).HasMaxLength(1024);
        builder.Property(x => x.FileName).HasMaxLength(256);
        builder.Property(x => x.FilterJson).HasMaxLength(2048);
        builder.Property(x => x.EmailRecipients).HasMaxLength(512);
        builder.Property(x => x.ArtifactPath).HasMaxLength(1024);
        builder.Property(x => x.FailureReason).HasMaxLength(1024);
    }
}
