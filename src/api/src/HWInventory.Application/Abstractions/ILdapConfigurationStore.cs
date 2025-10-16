using System.Collections.Generic;
using System.Threading;

namespace HWInventory.Application.Abstractions;

public interface ILdapConfigurationStore
{
    Task<LdapConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    Task<LdapConfigurationModel> SaveAsync(LdapConfigurationUpdate update, CancellationToken cancellationToken = default);
}

public record LdapConfigurationModel(
    bool Enabled,
    string Host,
    int Port,
    bool UseSsl,
    string BindDn,
    bool HasPassword,
    bool IgnoreCertificateErrors,
    string UsersBaseDn,
    string? UsersFilter,
    IReadOnlyCollection<string> AttributeMap);

public record LdapConfigurationUpdate(
    bool Enabled,
    string Host,
    int Port,
    bool UseSsl,
    string BindDn,
    string? Password,
    bool ResetPassword,
    bool IgnoreCertificateErrors,
    string UsersBaseDn,
    string? UsersFilter,
    IReadOnlyCollection<string> AttributeMap);
