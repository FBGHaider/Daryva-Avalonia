using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Daryva.Api.Security;

/// <summary>
/// Development-only authentication middleware.
///
/// When enabled, injects a dev user only when the request has no Authorization header at all.
/// Must run after UseAuthentication() so real JWT auth takes precedence. Enables local testing
/// without a token while still using real identity when the client sends one.
///
/// Deliberately does NOT fall back when a Bearer token IS present but failed to validate --
/// doing so previously masked a real JWT config bug (see prod incident where Jwt:Authority
/// pointed at a decommissioned OIDC provider): every rejected token silently became a valid
/// dev@local session instead of surfacing as 401, so broken auth passed local testing.
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
        if (_enabled)
        {
            var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
            var hasAuthorizationHeader = !string.IsNullOrEmpty(context.Request.Headers.Authorization);

            if (!isAuthenticated && hasAuthorizationHeader)
            {
                // A Bearer token WAS sent but failed real validation -- a genuine auth failure
                // (bad signature, wrong issuer, expired token, misconfigured Jwt:Authority, etc.).
                // Do not paper over it with a fake identity; let it surface as a real 401 so
                // JWT config bugs can't hide behind DevAuth during local testing.
                _logger.LogWarning("DevAuth: Authorization header present but token failed validation; not injecting dev user, letting request fail as unauthenticated.");
            }
            else if (!isAuthenticated)
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
