using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class ReportRunConfiguration : IEntityTypeConfiguration<ReportRun>
{
    public void Configure(EntityTypeBuilder<ReportRun> builder)
    {
        builder.ToTable("ReportRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ArtifactPath).HasMaxLength(500);
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.Property(x => x.DownloadToken).HasMaxLength(200);
    }
}
