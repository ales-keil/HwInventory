using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Route("api/settings/sftp")]
[Authorize(Policy = AuthorizationPolicies.SettingsManage)]
public class SftpController : ApiControllerBase
{
    private readonly ISftpConnectorStore _store;

    public SftpController(IAppDbContext dbContext, ISftpConnectorStore store)
        : base(dbContext)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<SftpConnectorModel?>> GetAsync(CancellationToken cancellationToken)
    {
        var model = await _store.GetAsync(cancellationToken);
        return Ok(model);
    }

    [HttpPut]
    public async Task<ActionResult<SftpConnectorModel>> SaveAsync([FromBody] SftpConnectorUpdate update, CancellationToken cancellationToken)
    {
        var before = await _store.GetAsync(cancellationToken);
        var result = await _store.SaveAsync(update, cancellationToken);

        AddAuditLog(
            entityType: "Settings.Sftp",
            entityId: result.Id,
            action: "Update",
            summary: "SFTP/FTPS connector configuration updated",
            changedFields: new
            {
                result.Alias,
                result.Enabled,
                result.Protocol,
                result.Host,
                result.Port,
                result.RemotePath,
                result.Username,
                result.UseKeyAuthentication,
                result.PassiveMode,
                result.UseImplicitFtps,
                result.AllowUnknownHosts,
                PreviousAlias = before?.Alias,
                PreviousEnabled = before?.Enabled,
                PreviousProtocol = before?.Protocol,
                PreviousHost = before?.Host,
                PreviousPort = before?.Port,
                PreviousRemotePath = before?.RemotePath,
                PreviousUsername = before?.Username,
                PreviousUseKeyAuthentication = before?.UseKeyAuthentication,
                PreviousPassiveMode = before?.PassiveMode,
                PreviousUseImplicitFtps = before?.UseImplicitFtps,
                PreviousAllowUnknownHosts = before?.AllowUnknownHosts
            });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("test")]
    public async Task<ActionResult<SftpConnectorTestResult>> TestAsync(CancellationToken cancellationToken)
    {
        var result = await _store.TestAsync(cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        AddAuditLog(
            entityType: "Settings.Sftp",
            entityId: Guid.Empty,
            action: "Test",
            summary: "SFTP/FTPS connector test executed",
            changedFields: new { result.Message });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }
}
