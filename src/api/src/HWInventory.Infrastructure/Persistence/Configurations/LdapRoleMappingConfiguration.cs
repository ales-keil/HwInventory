using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class LdapRoleMappingConfiguration : IEntityTypeConfiguration<LdapRoleMapping>
{
    public void Configure(EntityTypeBuilder<LdapRoleMapping> builder)
    {
        builder.ToTable("LdapRoleMappings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GroupName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.RoleName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);
        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.HasIndex(x => x.GroupName);
    }
}
