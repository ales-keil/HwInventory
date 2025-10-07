using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class WorkstationConfiguration : IEntityTypeConfiguration<Workstation>
{
    public void Configure(EntityTypeBuilder<Workstation> builder)
    {
        builder.ToTable("Workstations");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InventoryNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Cpu).HasMaxLength(200);
        builder.Property(x => x.Ram).HasMaxLength(100);
        builder.Property(x => x.Storage).HasMaxLength(200);
        builder.Property(x => x.MacAddress).HasMaxLength(100).IsRequired();
        builder.OwnsMany(x => x.NetworkAssignments, nb =>
        {
            nb.ToTable("WorkstationNetworkAssignments");
            nb.WithOwner().HasForeignKey("WorkstationId");
            nb.Property<Guid>("Id");
            nb.HasKey("Id");
            nb.Property(x => x.Label).HasMaxLength(50);
            nb.Property(x => x.IpAddress).HasMaxLength(100);
        });
    }
}
