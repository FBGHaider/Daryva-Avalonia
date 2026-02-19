using System.Security.Claims;

namespace Daryva.Api.Security;

/// <summary>
/// Interface for obtaining the current tenant (organization) context.
/// This is injected into the DbContext to enforce multi-tenancy isolation.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The authenticated user's ID (from JWT claims).
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// The currently selected organization ID for this request.
    /// Must be verified against the user's memberships.
    /// Null if user has no org context (not yet joined an org).
    /// </summary>
    Guid? CurrentOrgId { get; }

    /// <summary>
    /// Set the current organization context (used by middleware after validation).
    /// </summary>
    void SetCurrentOrgId(Guid? orgId);

    /// <summary>
    /// Check if user belongs to a specific org (for authorization).
    /// </summary>
    Task<bool> IsMemberOfOrgAsync(Guid orgId);
}

/// <summary>
/// Implementation of ITenantContext scoped to a single HTTP request.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _currentOrgId;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public string UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            
            // Try "NameIdentifier" claim first (common in many auth schemes)
            var nameIdClaim = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
            if (nameIdClaim != null)
            {
                return nameIdClaim.Value;
            }

            // Fallback: try "sub" claim (common in JWT)
            var subClaim = httpContext?.User?.FindFirst("sub");
            if (subClaim != null)
            {
                return subClaim.Value;
            }

            return "unknown-user";
        }
    }

    public Guid? CurrentOrgId => _currentOrgId;

    public void SetCurrentOrgId(Guid? orgId)
    {
        _currentOrgId = orgId;
    }

    public Task<bool> IsMemberOfOrgAsync(Guid orgId)
    {
        // Phase 3: This will query the database via a service to verify membership.
        // For now, just return true if CurrentOrgId matches.
        return Task.FromResult(CurrentOrgId == orgId);
    }
}
