using Microsoft.AspNetCore.Authorization;

namespace Daryva.Api.Security;

/// <summary>
/// Requires the caller's effective permission set to include this permission. See
/// PermissionAuthorizationHandler for how that set is computed, and Permissions/RolePermissions
/// for the vocabulary and role-to-permission map this checks against.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
