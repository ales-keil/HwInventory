using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface ISecurityAlertConfigurationStore
{
    Task<SecurityAlertConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    Task<SecurityAlertConfigurationModel> SaveAsync(SecurityAlertConfigurationUpdate update, CancellationToken cancellationToken = default);
}

public record SecurityAlertConfigurationModel(
    bool Enabled,
    double MinimumTwoFactorAdoptionPercentage,
    int LockedAccountThreshold,
    int PendingResetThreshold,
    IReadOnlyCollection<string> NotificationEmails);

public record SecurityAlertConfigurationUpdate(
    bool Enabled,
    double MinimumTwoFactorAdoptionPercentage,
    int LockedAccountThreshold,
    int PendingResetThreshold,
    IReadOnlyCollection<string> NotificationEmails);
