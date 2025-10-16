using HWInventory.Domain.Entities;

namespace HWInventory.Application.Abstractions;

public interface IPasswordHistoryService
{
    Task RecordAsync(AppUser user, CancellationToken cancellationToken = default);

    Task<bool> IsReusedAsync(AppUser user, string password, CancellationToken cancellationToken = default);
}
