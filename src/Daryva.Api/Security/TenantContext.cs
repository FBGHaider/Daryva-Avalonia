using System.Security.Claims;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.Api.Security;

/// <summary>
/// Implementation of ITenantContext scoped to a single HTTP request.
/// </summary>
public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private Guid? _currentOrgId;
    private string? _currentRole;
    private bool _isPrimaryOwnerOfCurrentOrg;
    private bool _isPlatformAdmin;
    private Guid? _activeSupportSessionId;
    private bool _roleResolved;

    // Repositories are resolved lazily (not constructor-injected) because they depend on
    // AppDbContext, and AppDbContext's global query filters depend on ITenantContext -- a
    // direct constructor dependency here would be a circular DI graph
    // (AppDbContext -> ITenantContext -> I...Repository -> AppDbContext).
    // Resolving via IServiceProvider at point of use, after AppDbContext already exists in the
    // scope, breaks the cycle.
    public TenantContext(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

    public string? CurrentRole => _currentRole;

    public bool IsPrimaryOwnerOfCurrentOrg => _isPrimaryOwnerOfCurrentOrg;

    public bool IsPlatformAdmin => _isPlatformAdmin;

    public Guid? ActiveSupportSessionId => _activeSupportSessionId;

    public void SetCurrentOrgId(Guid? orgId)
    {
        _currentOrgId = orgId;
        _currentRole = null;
        _isPrimaryOwnerOfCurrentOrg = false;
        _isPlatformAdmin = false;
        _activeSupportSessionId = null;
        _roleResolved = false;
    }

    public async Task ResolveCurrentRoleAsync(CancellationToken cancellationToken = default)
    {
        if (_roleResolved)
            return;

        _roleResolved = true;

        if (string.IsNullOrEmpty(UserId))
            return;

        var isValidUserGuid = Guid.TryParse(UserId, out var userGuid);

        // Platform admin status is independent of CurrentOrgId -- platform-level endpoints
        // (org list, user management) may be called with no org context at all.
        if (isValidUserGuid)
        {
            var appUserRepository = _serviceProvider.GetRequiredService<IAppUserRepository>();
            var appUser = await appUserRepository.GetByIdAsync(userGuid, cancellationToken).ConfigureAwait(false);
            _isPlatformAdmin = appUser?.IsPlatformAdmin ?? false;
        }

        if (!_currentOrgId.HasValue)
            return;

        var orgMemberRepository = _serviceProvider.GetRequiredService<IOrganizationMemberRepository>();
        var membership = await orgMemberRepository
            .GetMembershipAsync(UserId, _currentOrgId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (membership != null)
        {
            _currentRole = membership.Role;
            _isPrimaryOwnerOfCurrentOrg = membership.IsPrimaryOwner;
            return;
        }

        // Not a real member of this org. If the caller is a platform admin with an active
        // Support Session on this specific org, elevate to Landlord-equivalent for this
        // request only -- never granted by default, see Security.RolePermissions.
        if (_isPlatformAdmin && isValidUserGuid)
        {
            var supportSessionRepository = _serviceProvider.GetRequiredService<ISupportSessionRepository>();
            var activeSession = await supportSessionRepository
                .GetActiveSessionAsync(userGuid, _currentOrgId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (activeSession != null)
            {
                _currentRole = Roles.Landlord;
                _activeSupportSessionId = activeSession.Id;
            }
        }
    }

    public async Task<bool> IsMemberOfOrgAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(UserId))
            return false;

        if (_roleResolved && _currentOrgId == orgId)
            return _currentRole != null;

        var repository = _serviceProvider.GetRequiredService<IOrganizationMemberRepository>();
        var membership = await repository
            .GetMembershipAsync(UserId, orgId, cancellationToken)
            .ConfigureAwait(false);

        return membership != null;
    }
}
