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
        builder.Property(x => x.DepartmentKey).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.ModifiedBy).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(50);
    }
}
