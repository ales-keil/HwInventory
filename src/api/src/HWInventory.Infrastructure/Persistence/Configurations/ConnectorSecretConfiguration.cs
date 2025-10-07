using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class ConnectorSecretConfiguration : IEntityTypeConfiguration<ConnectorSecret>
{
    public void Configure(EntityTypeBuilder<ConnectorSecret> builder)
    {
        builder.ToTable("ConnectorSecrets");
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SecretReference).HasMaxLength(200).IsRequired();
    }
}
