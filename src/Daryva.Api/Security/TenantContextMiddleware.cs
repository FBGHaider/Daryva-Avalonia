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

        if (requestedOrgId.HasValue && !userOrgs.Contains(requestedOrgId.Value))
        {
            // Requested org isn't a real membership -- could still be a platform admin acting via
            // an active Support Session on that org (this applies whether or not the caller has
            // *other* real memberships elsewhere, so it must run regardless of userOrgs.Count).
            // Set it tentatively; ResolveCurrentRoleAsync (below) is what actually decides: it
            // checks IsPlatformAdmin + an active SupportSession row for this exact (user, org) pair.
            // If that comes back empty, CurrentRole stays null and we 403 below -- no privilege
            // escalation risk, this is not itself a grant of access.
            _logger.LogInformation(
                "User {UserId} requested org {OrgId} which is not a real membership; tentatively resolving (Support Session?).",
                userId, requestedOrgId);
            tenantContext.SetCurrentOrgId(requestedOrgId.Value);
            await tenantContext.ResolveCurrentRoleAsync();

            if (tenantContext.CurrentRole == null)
            {
                _logger.LogWarning(
                    "User {UserId} attempted to access org {OrgId} but is not a member and has no active Support Session on it.",
                    userId, requestedOrgId);
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Forbidden",
                    message = "You are not a member of the requested organization."
                });
                return;
            }

            await _next(httpContext);
            return;
        }

        if (userOrgs.Count == 0)
        {
            // User has no orgs yet and didn't request one (the requestedOrgId.HasValue case above
            // already handled a Support-Session org request); they'll need to create one first.
            _logger.LogInformation("User {UserId} has no organization memberships. CurrentOrgId = null.", userId);
            tenantContext.SetCurrentOrgId(null);

            // Resolve even with no org: IsPlatformAdmin must be available for purely platform-level
            // endpoints (org list, user management) that need no org context.
            await tenantContext.ResolveCurrentRoleAsync();
            await _next(httpContext);
            return;
        }

        if (requestedOrgId.HasValue)
        {
            // User explicitly requested an org via header, and it's a real membership (the
            // not-a-member branch above already returned for the opposite case).
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
