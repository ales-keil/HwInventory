using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.SettingsManage)]
[Route("api/settings/printing")]
public class PrintingController : ApiControllerBase
{
    private readonly IPrintingConnectorStore _store;

    public PrintingController(IAppDbContext dbContext, IPrintingConnectorStore store)
        : base(dbContext)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<PrintingConnectorResponse>> GetAsync(CancellationToken cancellationToken)
    {
        var model = await _store.GetAsync(cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return Ok(model.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<PrintingConnectorResponse>> SaveAsync([FromBody] PrintingConnectorRequest request, CancellationToken cancellationToken)
    {
        var model = await _store.SaveAsync(new PrintingConnectorUpdate(
            request.Alias,
            request.Enabled,
            request.Host,
            request.Port,
            request.QueueType,
            request.TimeoutSeconds,
            request.RetryCount,
            request.RotateSecret,
            request.SharedSecret),
            cancellationToken);

        AddAuditLog(
            entityType: "ConnectorProfile",
            entityId: model.Id,
            action: "Update",
            summary: "Printing connector updated",
            changedFields: new
            {
                request.Host,
                request.Port,
                request.QueueType,
                request.Enabled,
                request.TimeoutSeconds,
                request.RetryCount,
                request.RotateSecret,
                ProvidedSecret = !string.IsNullOrWhiteSpace(request.SharedSecret)
            });

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(model.ToResponse());
    }

    [HttpPost("test")]
    public async Task<ActionResult<PrintingConnectorTestResponse>> TestAsync(CancellationToken cancellationToken)
    {
        var result = await _store.TestAsync(cancellationToken);
        var model = await _store.GetAsync(cancellationToken);
        AddAuditLog(
            entityType: "ConnectorProfile",
            entityId: model?.Id,
            action: "Test",
            summary: "Printing connector test executed",
            changedFields: new
            {
                result.Success,
                result.Message
            });
        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PrintingConnectorTestResponse(result.Success, result.Message));
    }
}
