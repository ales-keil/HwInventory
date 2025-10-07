using Microsoft.AspNetCore.Identity;

namespace HWInventory.Domain.Entities;

public class AppRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
