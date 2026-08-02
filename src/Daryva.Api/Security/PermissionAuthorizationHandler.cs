using Daryva.Api.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Daryva.Api.Security;

/// <summary>
/// Grants a PermissionRequirement if the caller's effective permission set contains it. That
/// set is the union of: Admin's platform baseline (RolePermissions.ForRole(Roles.Admin)) when
/// ITenantContext.IsPlatformAdmin is true -- always available to admins, independent of org
/// context -- and CurrentRole's permissions when CurrentRole is set, which per phase 12 is
/// either real OrganizationMember.Role or an elevated Roles.Landlord from an active Support
/// Session on this specific org. Does not check resource ownership (which house/tenancy
/// belongs to which org) -- that's a separate concern, phase 14.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ITenantContext _tenantContext;

    public PermissionAuthorizationHandler(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (HasPermission(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private bool HasPermission(string permission)
    {
        if (_tenantContext.IsPlatformAdmin && RolePermissions.RoleHasPermission(Roles.Admin, permission))
            return true;

        var currentRole = _tenantContext.CurrentRole;
        return currentRole != null && RolePermissions.RoleHasPermission(currentRole, permission);
    }
}
