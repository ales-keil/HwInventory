using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("Users");
        builder.Property(x => x.UserName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.AppUser)
            .HasForeignKey(x => x.AppUserId);
        builder.HasMany(x => x.DataScopes)
            .WithOne(x => x.AppUser)
            .HasForeignKey(x => x.AppUserId);
    }
}
