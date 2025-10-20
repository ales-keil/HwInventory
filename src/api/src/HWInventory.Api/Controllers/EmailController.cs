using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Route("api/settings/email")]
public class EmailController : ApiControllerBase
{
    private readonly IEmailConnectorStore _store;

    public EmailController(IAppDbContext dbContext, IEmailConnectorStore store) : base(dbContext)
    {
        _store = store;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.SecurityManage)]
    public async Task<ActionResult<EmailConnectorModel?>> GetAsync(CancellationToken cancellationToken)
    {
        var model = await _store.GetAsync(cancellationToken);
        return Ok(model);
    }

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.SecurityManage)]
    public async Task<ActionResult<EmailConnectorModel>> SaveAsync([FromBody] EmailConnectorUpdate update, CancellationToken cancellationToken)
    {
        var before = await _store.GetAsync(cancellationToken);
        var result = await _store.SaveAsync(update, cancellationToken);

        AddAuditLog(
            "Settings.Email",
            result.Id,
            "Update",
            "SMTP configuration updated",
            new
            {
                Alias = result.Alias,
                Enabled = result.Enabled,
                Host = result.Host,
                Port = result.Port,
                UseTls = result.UseTls,
                Username = result.Username,
                FromAddress = result.FromAddress,
                ReplyToAddress = result.ReplyToAddress,
                PreviousEnabled = before?.Enabled,
                PreviousHost = before?.Host,
                PreviousPort = before?.Port,
                PreviousUseTls = before?.UseTls,
                PreviousUsername = before?.Username,
                PreviousFromAddress = before?.FromAddress,
                PreviousReplyToAddress = before?.ReplyToAddress
            });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("test")]
    [Authorize(Policy = AuthorizationPolicies.SecurityManage)]
    public async Task<ActionResult<EmailTestResult>> SendTestAsync([FromBody] EmailTestRequest request, CancellationToken cancellationToken)
    {
        var result = await _store.SendTestAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        AddAuditLog(
            "Settings.Email",
            Guid.Empty,
            "Test",
            $"SMTP test e-mail sent to {request.Recipient}");

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }
}
