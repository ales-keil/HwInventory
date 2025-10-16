using System.ComponentModel.DataAnnotations;

namespace HWInventory.Api.Models;

public sealed class BatchActionRequest
{
    [Required]
    public IReadOnlyCollection<Guid> Ids { get; init; } = Array.Empty<Guid>();
}
