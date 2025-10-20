using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class PasswordHistoryEntryConfiguration : IEntityTypeConfiguration<PasswordHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PasswordHistoryEntry> builder)
    {
        builder.ToTable("PasswordHistoryEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
