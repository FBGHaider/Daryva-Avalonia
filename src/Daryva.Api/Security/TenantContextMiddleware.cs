using Daryva.Api.Data;
using Daryva.Api.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Security;

/// <summary>
/// Middleware to determine the current organization context for multi-tenant isolation.
/// Runs after authentication to:
/// 1. Read X-Org-Id header (if provided)
/// 2. Validate user membership in requested org
/// 3. Auto-select if user belongs to exactly one org
/// 4. Return 403 if user tries invalid org or has no org context
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContext tenantContext,
        AppDbContext dbContext)
    {
        // Extract X-Org-Id header if provided
        var orgIdHeader = httpContext.Request.Headers["X-Org-Id"].FirstOrDefault();
        Guid? requestedOrgId = null;

        if (!string.IsNullOrEmpty(orgIdHeader) && Guid.TryParse(orgIdHeader, out var parsedOrgId))
        {
            requestedOrgId = parsedOrgId;
        }

        var userId = tenantContext.UserId;

        // Skip tenant context for public endpoints (health check, etc.)
        if (IsPublicRoute(httpContext))
        {
            await _next(httpContext);
            return;
        }

        // If user is unauthenticated, set null and let controller handle it
        if (!httpContext.User.Identity?.IsAuthenticated ?? false)
        {
            tenantContext.SetCurrentOrgId(null);
            await _next(httpContext);
            return;
        }

        // Get user's organization memberships
        var userOrgs = await dbContext.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.OrganizationId)
            .ToListAsync();

        if (userOrgs.Count == 0)
        {
            if (requestedOrgId.HasValue)
            {
                // No real org memberships, but an org was explicitly requested -- e.g. a platform
                // admin acting via an active Support Session on that org. Set it tentatively;
                // ResolveCurrentRoleAsync (below) determines whether this caller actually gets any
                // access. If not, CurrentRole stays null and downstream permission/resource checks
                // deny, same as any other unauthorized request -- no privilege escalation risk.
                _logger.LogInformation(
                    "User {UserId} has no organization memberships; tentatively resolving requested org {OrgId}.",
                    userId, requestedOrgId);
                tenantContext.SetCurrentOrgId(requestedOrgId.Value);
            }
            else
            {
                // User has no orgs yet; they'll need to create one first
                _logger.LogInformation("User {UserId} has no organization memberships. CurrentOrgId = null.", userId);
                tenantContext.SetCurrentOrgId(null);
            }

            // Resolve even with no/unvalidated org: IsPlatformAdmin must be available for
            // purely platform-level endpoints (org list, user management) that need no org context.
            await tenantContext.ResolveCurrentRoleAsync();
            await _next(httpContext);
            return;
        }

        if (requestedOrgId.HasValue)
        {
            // User explicitly requested an org via header
            if (!userOrgs.Contains(requestedOrgId.Value))
            {
                // Unauthorized: user not a member of requested org
                _logger.LogWarning(
                    "User {UserId} attempted to access org {OrgId} but is not a member.",
                    userId, requestedOrgId);
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Forbidden",
                    message = "You are not a member of the requested organization."
                });
                return;
            }

            tenantContext.SetCurrentOrgId(requestedOrgId.Value);
            _logger.LogInformation("User {UserId} selected org {OrgId}.", userId, requestedOrgId);
        }
        else
        {
            // No X-Org-Id header; auto-select if user belongs to exactly one org
            if (userOrgs.Count == 1)
            {
                tenantContext.SetCurrentOrgId(userOrgs[0]);
                _logger.LogInformation("User {UserId} auto-selected single org {OrgId}.", userId, userOrgs[0]);
            }
            else
            {
                // User belongs to multiple orgs but didn't specify which one
                // This is a client error: they must provide X-Org-Id header
                _logger.LogInformation(
                    "User {UserId} belongs to {Count} orgs but did not specify X-Org-Id header.",
                    userId, userOrgs.Count);
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Bad Request",
                    message = "You belong to multiple organizations. Specify X-Org-Id header.",
                    organizations = userOrgs
                });
                return;
            }
        }

        // Resolve CurrentRole/IsPrimaryOwnerOfCurrentOrg once here, so the rest of the pipeline
        // (controllers, services) can read ITenantContext.CurrentRole as a plain sync property.
        await tenantContext.ResolveCurrentRoleAsync();

        await _next(httpContext);
    }

    /// <summary>
    /// Routes that don't require org context (auth, health, etc).
    /// </summary>
    private static bool IsPublicRoute(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? "";
        return path == "/health" ||
               path == "/api/me" ||    // Current user profile + onboarding state (no org context required)
               path == "/api/orgs" ||  // Allow listing user's organizations without X-Org-Id
               path.StartsWith("/api/orgs/join/") || // Accept invite / join by code (no org context required)
               path.StartsWith("/api/auth/") ||
               path == "/swagger" ||
               path.StartsWith("/swagger/") ||
               path == "/metrics"; // Prometheus metrics, if any
    }
}
