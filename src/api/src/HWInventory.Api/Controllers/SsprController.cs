using System.Threading;
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
    public async Task<ActionResult<PasswordResetRequestResult>> RequestAsync([FromBody] PasswordResetRequest request, CancellationToken cancellationToken)
    {
        var result = await _ssprService.RequestResetAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("complete")]
    public async Task<ActionResult<PasswordResetCompletionResult>> CompleteAsync([FromBody] PasswordResetCompletionRequest request, CancellationToken cancellationToken)
    {
        var result = await _ssprService.CompleteResetAsync(request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
