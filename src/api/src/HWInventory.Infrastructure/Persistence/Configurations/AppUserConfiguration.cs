using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AspNetUsers");
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Department).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);
        builder.Property(x => x.AuthenticatorKey).HasMaxLength(256);
        builder.HasMany(x => x.DataScopes)
            .WithOne(x => x.AppUser)
            .HasForeignKey(x => x.AppUserId);
        builder.HasMany(x => x.Sessions)
            .WithOne(x => x.AppUser)
            .HasForeignKey(x => x.AppUserId);
        builder.HasMany(x => x.PasswordResetTokens)
            .WithOne(x => x.AppUser)
            .HasForeignKey(x => x.AppUserId);
    }
}
