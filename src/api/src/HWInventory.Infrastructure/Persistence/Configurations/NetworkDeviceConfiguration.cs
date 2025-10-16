using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HWInventory.Infrastructure.Persistence.Configurations;

public class NetworkDeviceConfiguration : IEntityTypeConfiguration<NetworkDevice>
{
    public void Configure(EntityTypeBuilder<NetworkDevice> builder)
    {
        builder.ToTable("NetworkDevices");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InventoryNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Manufacturer).HasMaxLength(200);
        builder.Property(x => x.Model).HasMaxLength(200);
        builder.OwnsMany(x => x.NetworkAssignments, nb =>
        {
            nb.ToTable("NetworkDeviceAssignments");
            nb.WithOwner().HasForeignKey("NetworkDeviceId");
            nb.Property<Guid>("Id");
            nb.HasKey("Id");
            nb.Property(x => x.Label).HasMaxLength(50);
            nb.Property(x => x.IpAddress).HasMaxLength(100);
        });
    }
}
