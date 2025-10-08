namespace HWInventory.Application.Abstractions;

public interface IExternalIdentityService
{
    Task ConfigureOidcAsync(OidcConfiguration configuration, CancellationToken cancellationToken = default);
    Task<OidcConfiguration?> GetConfigurationAsync(CancellationToken cancellationToken = default);
}

public record OidcConfiguration(
    bool Enabled,
    string Authority,
    string ClientId,
    string? ClientSecret,
    string? ResponseType,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyDictionary<string, string> ClaimMappings,
    bool UsePkce);
