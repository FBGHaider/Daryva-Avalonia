using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Daryva.Api.Security;

/// <summary>
/// Development-only authentication middleware.
///
/// When enabled, injects a dev user only when the request is not already authenticated
/// (no valid Bearer token). Must run after UseAuthentication() so Clerk/JWT takes precedence.
/// Enables local testing without a token while still using real identity when the client sends one.
///
/// WARNING: This middleware must NEVER be enabled in production.
/// </summary>
public class DevAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DevAuthMiddleware> _logger;
    private readonly bool _enabled;
    private readonly string _userId;
    private readonly string _userEmail;
    private readonly string _userName;

    public DevAuthMiddleware(
        RequestDelegate next,
        ILogger<DevAuthMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        var devAuthConfig = configuration.GetSection("DevAuth");
        _enabled = devAuthConfig.GetValue<bool>("Enabled");
        _userId = devAuthConfig.GetValue<string>("UserId") ?? "dev-user-1";
        _userEmail = devAuthConfig.GetValue<string>("Email") ?? "dev@local";
        _userName = devAuthConfig.GetValue<string>("Name") ?? "Dev User";

        if (_enabled)
        {
            _logger.LogWarning("⚠️  DevAuth is ENABLED (fallback when no Bearer token). Must NEVER be used in production. Unauthenticated requests will be treated as '{Email}'.", _userEmail);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only inject dev user when no valid auth is present (e.g. no Bearer token or JWT failed).
        // This must run after UseAuthentication() so Clerk/JWT can take precedence.
        if (_enabled)
        {
            var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
            if (!isAuthenticated)
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, _userId),
                    new("sub", _userId),
                    new(ClaimTypes.Email, _userEmail),
                    new(ClaimTypes.Name, _userName),
                };

                var claimsIdentity = new ClaimsIdentity(claims, "DevAuth");
                context.User = new ClaimsPrincipal(claimsIdentity);

                _logger.LogDebug("DevAuth: No auth present; injected user '{Email}' (UserId: {UserId})", _userEmail, _userId);
            }
        }

        await _next(context);
    }
}
