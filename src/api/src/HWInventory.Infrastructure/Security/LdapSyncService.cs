using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.DirectoryServices.Protocols;
using HWInventory.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Security;

public class LdapSyncService : ILdapSyncService
{
    private readonly ILogger<LdapSyncService> _logger;

    public LdapSyncService(ILogger<LdapSyncService> logger)
    {
        _logger = logger;
    }

    public Task<LdapConnectionTestResult> TestConnectionAsync(LdapConnectionOptions options, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExecuteTest(options), cancellationToken);
    }

    public Task<LdapDryRunResult> DryRunAsync(LdapDryRunRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ExecuteDryRun(request), cancellationToken);
    }

    private LdapConnectionTestResult ExecuteTest(LdapConnectionOptions options)
    {
        try
        {
            using var connection = CreateConnection(options);
            connection.Bind();
            var diagnostics = new Dictionary<string, string>
            {
                ["Server"] = options.Host,
                ["Port"] = options.Port.ToString(),
                ["Secure"] = options.UseSsl.ToString()
            };

            return new LdapConnectionTestResult(true, "Bind succeeded", diagnostics);
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(ex, "LDAP bind failed");
            return new LdapConnectionTestResult(false, ex.Message, BuildDiagnosticsFromException(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected LDAP error");
            return new LdapConnectionTestResult(false, ex.Message, BuildDiagnosticsFromException(ex));
        }
    }

    private LdapDryRunResult ExecuteDryRun(LdapDryRunRequest request)
    {
        try
        {
            using var connection = CreateConnection(request.Connection);
            connection.Bind();

            var searchRequest = new SearchRequest(
                request.UsersBaseDn,
                string.IsNullOrWhiteSpace(request.UsersFilter) ? "(objectClass=user)" : request.UsersFilter,
                SearchScope.Subtree,
                request.AttributeMap.ToArray());

            var response = (SearchResponse)connection.SendRequest(searchRequest);

            var users = new List<LdapDryRunUser>();
            foreach (SearchResultEntry entry in response.Entries)
            {
                var attributes = new Dictionary<string, string?>();
                foreach (var attributeKey in request.AttributeMap)
                {
                    if (entry.Attributes.Contains(attributeKey))
                    {
                        var attribute = entry.Attributes[attributeKey];
                        attributes[attributeKey] = attribute?.GetValues(typeof(string)).Cast<string?>().FirstOrDefault();
                    }
                    else
                    {
                        attributes[attributeKey] = null;
                    }
                }

                users.Add(new LdapDryRunUser(entry.DistinguishedName, attributes));
                if (users.Count >= request.ResultLimit)
                {
                    break;
                }
            }

            var truncated = users.Count >= request.ResultLimit && response.Entries.Count > request.ResultLimit;
            var notesBuilder = new StringBuilder();
            if (truncated)
            {
                notesBuilder.Append("Result set truncated to limit");
            }

            return new LdapDryRunResult(users, response.Entries.Count, truncated, notesBuilder.Length == 0 ? null : notesBuilder.ToString());
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(ex, "LDAP dry run failed");
            throw;
        }
    }

    private static LdapConnection CreateConnection(LdapConnectionOptions options)
    {
        var identifier = new LdapDirectoryIdentifier(options.Host, options.Port);
        var connection = new LdapConnection(identifier)
        {
            Credential = new NetworkCredential(options.BindDn, options.Password ?? string.Empty),
            AuthType = AuthType.Basic
        };

        if (options.UseSsl)
        {
            connection.SessionOptions.SecureSocketLayer = true;
        }

        if (options.IgnoreCertificateErrors)
        {
            connection.SessionOptions.VerifyServerCertificate += (_, _) => true;
        }

        connection.SessionOptions.ProtocolVersion = 3;
        connection.Timeout = TimeSpan.FromSeconds(15);
        return connection;
    }

    private static Dictionary<string, string> BuildDiagnosticsFromException(Exception ex)
    {
        var diagnostics = new Dictionary<string, string>
        {
            ["ExceptionType"] = ex.GetType().FullName ?? ex.GetType().Name,
            ["Message"] = ex.Message
        };

        if (ex is LdapException ldapEx)
        {
            diagnostics["ErrorCode"] = ldapEx.ErrorCode.ToString();
            diagnostics["ServerErrorMessage"] = ldapEx.ServerErrorMessage ?? string.Empty;
        }

        return diagnostics;
    }
}
