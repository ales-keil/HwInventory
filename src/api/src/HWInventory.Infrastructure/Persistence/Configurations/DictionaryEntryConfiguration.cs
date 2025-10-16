using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class DictionaryEntryConfiguration : IEntityTypeConfiguration<DictionaryEntry>
{
    public void Configure(EntityTypeBuilder<DictionaryEntry> builder)
    {
        builder.ToTable("DictionaryEntries");
        builder.Property(x => x.DictType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(200).IsRequired();
    }
}
