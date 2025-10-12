using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface ICaptchaConfigurationStore
{
    Task<CaptchaConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    Task<CaptchaConfigurationModel> SaveAsync(CaptchaConfigurationUpdate update, CancellationToken cancellationToken = default);
}

public record CaptchaConfigurationModel(
    bool Enabled,
    string SiteKey,
    bool HasSecret,
    string VerificationEndpoint,
    string? BypassToken);

public record CaptchaConfigurationUpdate(
    bool Enabled,
    string SiteKey,
    string? Secret,
    bool RotateSecret,
    string VerificationEndpoint,
    string? BypassToken);
