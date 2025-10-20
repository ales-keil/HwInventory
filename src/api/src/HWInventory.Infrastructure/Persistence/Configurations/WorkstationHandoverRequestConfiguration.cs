using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class WorkstationHandoverRequestConfiguration : IEntityTypeConfiguration<WorkstationHandoverRequest>
{
    public void Configure(EntityTypeBuilder<WorkstationHandoverRequest> builder)
    {
        builder.ToTable("WorkstationHandovers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(WorkstationHandoverStatus.Pending);

        builder.Property(x => x.AcceptTokenHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.DeclineTokenHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.RequestedBy)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ToRecipientsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CcRecipientsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.BccRecipientsJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Subject)
            .HasMaxLength(512);

        builder.Property(x => x.MessageBody)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Comment)
            .HasColumnType("nvarchar(max)");

        builder.HasOne(x => x.Workstation)
            .WithMany()
            .HasForeignKey(x => x.WorkstationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WorkstationId, x.Status });
        builder.HasIndex(x => x.TokenExpiresAtUtc);
    }
}
