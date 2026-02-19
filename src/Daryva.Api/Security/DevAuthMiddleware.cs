using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Daryva.Api.Security;

/// <summary>
/// Development-only authentication middleware.
/// 
/// When enabled via appsettings.DevAuth.Enabled, injects a fake user identity
/// without requiring an external auth provider. Useful for local development and testing.
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
            _logger.LogWarning("⚠️  DevAuth is ENABLED. This must NEVER be used in production. Requests will be authenticated as '{Email}'.", _userEmail);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_enabled)
        {
            // Create a claims identity for the dev user
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, _userId),
                new("sub", _userId),
                new(ClaimTypes.Email, _userEmail),
                new(ClaimTypes.Name, _userName),
            };

            var claimsIdentity = new ClaimsIdentity(claims, "DevAuth");
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Set the user on the HttpContext
            context.User = claimsPrincipal;

            _logger.LogDebug("DevAuth: Injected user '{Email}' (UserId: {UserId})", _userEmail, _userId);
        }

        await _next(context);
    }
}
