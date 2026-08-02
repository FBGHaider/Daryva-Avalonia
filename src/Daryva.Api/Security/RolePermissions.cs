namespace Daryva.Api.Security;

/// <summary>
/// Single source of truth mapping each logical role (<see cref="Roles"/>) to its permission
/// set (<see cref="Permissions"/>).
///
/// Admin's set here is the PLATFORM-LEVEL baseline only -- it deliberately does NOT include
/// any org-data permissions (Properties, Tenants, Payments, Documents, etc). An admin gets
/// those only for the duration of an active Support Session, scoped to one org at a time;
/// that elevation is computed at request time (phase 12), not granted globally here. This is
/// the static half of the design -- "what can this role do" -- not "what org can it act on",
/// which is a separate, per-request concern.
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Roles.Admin] = new HashSet<string>
            {
                Permissions.Platform.Admin,
                Permissions.Platform.ManageOrganizations,
                Permissions.Platform.ManageUsers,
                Permissions.Audit.View,
            },
            [Roles.Landlord] = new HashSet<string>
            {
                Permissions.Properties.View,
                Permissions.Properties.Manage,
                Permissions.Tenants.View,
                Permissions.Tenants.Manage,
                Permissions.Tenancies.View,
                Permissions.Tenancies.Manage,
                Permissions.Payments.View,
                Permissions.Payments.Record,
                Permissions.Payments.Void,
                Permissions.Expenses.View,
                Permissions.Expenses.Manage,
                Permissions.Documents.View,
                Permissions.Documents.Manage,
                Permissions.Maintenance.View,
                Permissions.Maintenance.Create,
                Permissions.Maintenance.Manage,
                Permissions.Messaging.Send,
                Permissions.Messaging.View,
                Permissions.Reports.View,
                Permissions.Organization.View,
                Permissions.Organization.ManageMembers,
                Permissions.Organization.ManageSettings,
                Permissions.Organization.Delete,
                Permissions.Audit.View,
            },
            [Roles.Tenant] = new HashSet<string>
            {
                Permissions.Properties.View,
                Permissions.Tenancies.View,
                Permissions.Payments.View,
                Permissions.Documents.View,
                Permissions.Maintenance.View,
                Permissions.Maintenance.Create,
                Permissions.Messaging.Send,
                Permissions.Messaging.View,
            },
        };

    /// <summary>Every permission that appears in at least one role's set -- used by PermissionPolicyProvider to recognize a policy name as a permission.</summary>
    public static readonly IReadOnlySet<string> AllPermissions =
        Map.Values.SelectMany(p => p).ToHashSet();

    public static IReadOnlySet<string> ForRole(string role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : new HashSet<string>();

    public static bool RoleHasPermission(string role, string permission) =>
        Map.TryGetValue(role, out var permissions) && permissions.Contains(permission);
}
