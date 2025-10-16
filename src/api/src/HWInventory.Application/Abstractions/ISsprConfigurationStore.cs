using System.Threading;

namespace HWInventory.Application.Abstractions;

public interface ISsprConfigurationStore
{
    Task<SsprConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    Task<SsprConfigurationModel> SaveAsync(SsprConfigurationUpdate update, CancellationToken cancellationToken = default);
}

public record SsprConfigurationModel(
    bool Enabled,
    bool RequireTwoFactor,
    bool RequireCaptcha,
    bool RequireSmsOtp,
    int TokenExpiryMinutes,
    int ThrottleWindowMinutes,
    int MaxRequestsPerWindow,
    string? SmsConnectorKey);

public record SsprConfigurationUpdate(
    bool Enabled,
    bool RequireTwoFactor,
    bool RequireCaptcha,
    bool RequireSmsOtp,
    int TokenExpiryMinutes,
    int ThrottleWindowMinutes,
    int MaxRequestsPerWindow,
    string? SmsConnectorKey);
