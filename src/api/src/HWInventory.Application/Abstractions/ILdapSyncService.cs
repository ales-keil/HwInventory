namespace HWInventory.Application.Abstractions;

public interface ILdapSyncService
{
    Task<LdapConnectionTestResult> TestConnectionAsync(LdapConnectionOptions options, CancellationToken cancellationToken = default);
    Task<LdapDryRunResult> DryRunAsync(LdapDryRunRequest request, CancellationToken cancellationToken = default);
}

public record LdapConnectionOptions(
    string Host,
    int Port,
    bool UseSsl,
    string BindDn,
    string? Password,
    bool IgnoreCertificateErrors);

public record LdapDryRunRequest(
    LdapConnectionOptions Connection,
    string UsersBaseDn,
    string? UsersFilter,
    IReadOnlyCollection<string> AttributeMap,
    int ResultLimit);

public record LdapConnectionTestResult(bool Success, string Message, IReadOnlyDictionary<string, string>? Diagnostics);

public record LdapDryRunResult(
    IReadOnlyCollection<LdapDryRunUser> Users,
    int ResultCount,
    bool Truncated,
    string? Notes);

public record LdapDryRunUser(string DistinguishedName, IReadOnlyDictionary<string, string?> Attributes);
