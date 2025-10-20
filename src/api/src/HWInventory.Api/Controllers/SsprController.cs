using System.Threading;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[AllowAnonymous]
[Route("api/security/sspr")]
[ApiController]
public class SsprController : ControllerBase
{
    private readonly ISsprService _ssprService;

    public SsprController(ISsprService ssprService)
    {
        _ssprService = ssprService;
    }

    [HttpPost("request")]
    public async Task<ActionResult<PasswordResetRequestResult>> RequestAsync([FromBody] PasswordResetRequestDto request, CancellationToken cancellationToken)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _ssprService.RequestResetAsync(request.ToModel(clientIp, userAgent), cancellationToken);
        if (!result.Success)
        {
            if (result.RetryAfterSeconds.HasValue)
            {
                Response.Headers.RetryAfter = result.RetryAfterSeconds.Value.ToString();
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("complete")]
    public async Task<ActionResult<PasswordResetCompletionResult>> CompleteAsync([FromBody] PasswordResetCompletionRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _ssprService.CompleteResetAsync(request.ToModel(), cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
