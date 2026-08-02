using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Daryva.Api.Security;

/// <summary>
/// Lets controllers write [Authorize(Policy = Permissions.Properties.Manage)] directly against
/// permission strings, without registering a named policy per permission in Program.cs. Falls
/// back to the default provider for plain [Authorize] (no policy name) and for any policy name
/// that isn't a known permission -- e.g. a typo lands on the default provider's null policy,
/// which ASP.NET Core surfaces as a clear error rather than silently granting access.
/// </summary>
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (RolePermissions.AllPermissions.Contains(policyName))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackPolicyProvider.GetPolicyAsync(policyName);
    }
}
