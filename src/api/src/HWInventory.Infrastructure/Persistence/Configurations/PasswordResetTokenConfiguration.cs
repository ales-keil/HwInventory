using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");
        builder.HasIndex(x => x.Token).IsUnique();
        builder.Property(x => x.DeliveryMethod).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DeliveryAddress).HasMaxLength(300);
        builder.Property(x => x.Status).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SmsCodeHash).HasMaxLength(256);
        builder.Property(x => x.IdentityToken).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);
    }
}
