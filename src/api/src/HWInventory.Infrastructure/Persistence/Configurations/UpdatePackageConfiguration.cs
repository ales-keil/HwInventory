using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class UpdatePackageConfiguration : IEntityTypeConfiguration<UpdatePackage>
{
    public void Configure(EntityTypeBuilder<UpdatePackage> builder)
    {
        builder.ToTable("UpdatePackages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Version)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.StoredPath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.StagingPath)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(x => x.Sha256)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ManifestJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Notes)
            .HasMaxLength(1024);

        builder.Property(x => x.BackupStoragePath)
            .HasMaxLength(512);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(1024);

        builder.Property(x => x.LogPath)
            .HasMaxLength(1024);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ModifiedBy)
            .HasMaxLength(256);

        builder.Property(x => x.UpdateStatus)
            .HasConversion<int>();

        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.UpdateStatus);
    }
}
