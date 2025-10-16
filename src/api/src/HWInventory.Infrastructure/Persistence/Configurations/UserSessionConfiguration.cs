using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");
        builder.Property(x => x.SessionIdentifier).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(200);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.HasIndex(x => new { x.AppUserId, x.IsRevoked });
    }
}
