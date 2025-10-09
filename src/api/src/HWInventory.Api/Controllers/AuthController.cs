using System;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ITotpService _totpService;

    public AuthController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ITotpService totpService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _totpService = totpService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatné přihlašovací údaje",
                Detail = "Uživatelské jméno/e-mail i heslo jsou povinné."
            });
        }

        AppUser? user = request.UserNameOrEmail.Contains('@')
            ? await _userManager.FindByEmailAsync(request.UserNameOrEmail)
            : await _userManager.FindByNameAsync(request.UserNameOrEmail);

        if (user is null || user.Status != EntityStatus.Active)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            return Unauthorized(new ProblemDetails
            {
                Title = "Přihlášení selhalo",
                Detail = "Zadané údaje nejsou platné."
            });
        }

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, true);

        if (result.RequiresTwoFactor)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Vyžadováno vícefaktorové ověření",
                Detail = "Tento účet vyžaduje zadání TOTP kódu."
            });
        }

        if (result.IsLockedOut)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Účet je dočasně zablokován",
                Detail = "Překročili jste maximální počet pokusů. Zkuste to prosím později."
            });
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Přihlášení selhalo",
                Detail = "Zadané údaje nejsou platné."
            });
        }

        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<object>> GetProfileAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Email,
            Roles = roles,
            TwoFactorEnabled = user.TwoFactorEnabled,
            TwoFactorRequired = user.TwoFactorRequired
        });
    }

    [HttpPost("totp/setup")]
    [Authorize]
    public async Task<ActionResult<object>> BeginTotpSetupAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(user.AuthenticatorKey))
        {
            user.AuthenticatorKey = _totpService.GenerateSecretKey();
            await _userManager.UpdateAsync(user);
        }

        var code = _totpService.GenerateCode(user.AuthenticatorKey);
        return Ok(new
        {
            SecretKey = user.AuthenticatorKey,
            VerificationCode = code
        });
    }

    [HttpPost("totp/verify")]
    [Authorize]
    public async Task<ActionResult> CompleteTotpSetupAsync([FromBody] TotpVerificationRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(user.AuthenticatorKey))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "TOTP secret not initialized",
                Detail = "Call /api/auth/totp/setup before attempting verification."
            });
        }

        if (!_totpService.ValidateCode(user.AuthenticatorKey, request.Code))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid verification code",
                Detail = "The provided TOTP code could not be validated."
            });
        }

        user.TwoFactorEnabled = true;
        await _userManager.UpdateAsync(user);
        await _userManager.SetTwoFactorEnabledAsync(user, true);
        await _signInManager.RefreshSignInAsync(user);

        return NoContent();
    }

    public record LoginRequest(string UserNameOrEmail, string Password, bool RememberMe);

    public record TotpVerificationRequest(string Code);
}
