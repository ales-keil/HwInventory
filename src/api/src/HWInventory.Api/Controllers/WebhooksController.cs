using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Route("api/settings/webhooks")]
[Authorize(Policy = AuthorizationPolicies.SettingsManage)]
public class WebhooksController : ApiControllerBase
{
    private readonly IWebhookConnectorStore _store;

    public WebhooksController(IAppDbContext dbContext, IWebhookConnectorStore store)
        : base(dbContext)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<WebhookConnectorResponse?>> GetAsync(CancellationToken cancellationToken)
    {
        var model = await _store.GetAsync(cancellationToken);
        if (model is null)
        {
            return Ok(null);
        }

        return Ok(Map(model));
    }

    [HttpPut]
    public async Task<ActionResult<WebhookConnectorResponse>> SaveAsync([FromBody] WebhookConnectorUpdateRequest request, CancellationToken cancellationToken)
    {
        var before = await _store.GetAsync(cancellationToken);
        var update = new WebhookConnectorUpdate(
            request.Alias,
            request.Enabled,
            request.Url,
            request.Method,
            request.ContentType,
            request.Headers ?? new Dictionary<string, string>(),
            request.UseSignature,
            request.SigningSecret,
            request.RotateSecret);

        var result = await _store.SaveAsync(update, cancellationToken);

        AddAuditLog(
            "Settings.Webhooks",
            result.Id,
            "Update",
            "Webhook connector updated",
            new
            {
                result.Alias,
                result.Enabled,
                result.Url,
                result.Method,
                result.ContentType,
                Headers = result.Headers,
                result.UseSignature,
                result.HasSecret,
                PreviousAlias = before?.Alias,
                PreviousEnabled = before?.Enabled,
                PreviousUrl = before?.Url,
                PreviousMethod = before?.Method,
                PreviousContentType = before?.ContentType,
                PreviousUseSignature = before?.UseSignature,
                HadSecret = before?.HasSecret
            });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(Map(result));
    }

    [HttpPost("test")]
    public async Task<ActionResult<WebhookTestResponse>> SendTestAsync([FromBody] WebhookTestRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _store.SendTestAsync(new WebhookTestRequest(request.EventType, request.PayloadJson), cancellationToken);
        AddAuditLog(
            "Settings.Webhooks",
            Guid.Empty,
            "Test",
            "Webhook test executed",
            new
            {
                result.Success,
                result.Message,
                request.EventType
            });

        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(new WebhookTestResponse(result.Success, result.Message, result.ResponseSnippet));
    }

    private static WebhookConnectorResponse Map(WebhookConnectorModel model)
    {
        return new WebhookConnectorResponse(
            model.Id,
            model.Alias,
            model.Enabled,
            model.Url,
            model.Method,
            model.ContentType,
            model.Headers,
            model.UseSignature,
            model.HasSecret,
            model.HealthStatus,
            model.LastTestedAtUtc);
    }
}
