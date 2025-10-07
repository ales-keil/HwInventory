using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HWInventory.Api.Middleware;

public class SessionTrackingMiddleware
{
    private const string SessionCookieName = "HWInventory.SessionId";
    private readonly RequestDelegate _next;

    public SessionTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionTracker sessionTracker)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                var actor = context.User.Identity?.Name ?? context.User.FindFirstValue(ClaimTypes.Email) ?? "system";

                if (!context.Request.Cookies.TryGetValue(SessionCookieName, out var sessionIdentifier) || string.IsNullOrWhiteSpace(sessionIdentifier))
                {
                    sessionIdentifier = Guid.NewGuid().ToString("N");
                    context.Response.Cookies.Append(SessionCookieName, sessionIdentifier, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });

                    await sessionTracker.RegisterAsync(new RegisterSessionRequest(
                        userId,
                        sessionIdentifier,
                        actor,
                        context.Connection.RemoteIpAddress?.ToString(),
                        context.Request.Headers.UserAgent.ToString()),
                        context.RequestAborted);
                }
                else
                {
                    var active = await sessionTracker.TouchAsync(sessionIdentifier, context.RequestAborted);
                    if (!active)
                    {
                        context.Response.Cookies.Delete(SessionCookieName);
                        await context.SignOutAsync();
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}

public static class SessionTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionTracking(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionTrackingMiddleware>();
    }
}
