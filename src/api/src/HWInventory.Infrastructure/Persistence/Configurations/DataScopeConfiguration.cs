using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class DataScopeConfiguration : IEntityTypeConfiguration<DataScope>
{
    public void Configure(EntityTypeBuilder<DataScope> builder)
    {
        builder.ToTable("DataScopes");
        builder.Property(x => x.ScopeType).HasMaxLength(100).IsRequired();
    }
}
