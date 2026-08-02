namespace Daryva.Api.Security;

/// <summary>
/// Flat permission vocabulary consumed by the role-to-permission map (phase 9) and the
/// permission-based authorization policies (phase 13). A permission says "this role can do
/// this category of action" -- it does NOT encode scope (which org, which house, which
/// tenancy). Scope is enforced separately by the resource-ownership handler (phase 14) and,
/// for org data, by AppDbContext's global query filters. The same permission (e.g.
/// Properties.View) is held by both Landlord (scoped to their org's houses) and, later,
/// Tenant (scoped to their own house) -- the resource layer narrows which rows, not this.
///
/// Maintenance and Messaging permissions are reserved ahead of their entities/workflows
/// existing (see design discussion) so the role-to-permission map is ready to plug them in.
/// </summary>
public static class Permissions
{
    public static class Properties
    {
        public const string View = "properties.view";
        public const string Manage = "properties.manage";
    }

    public static class Tenants
    {
        public const string View = "tenants.view";
        public const string Manage = "tenants.manage";
    }

    public static class Tenancies
    {
        public const string View = "tenancies.view";
        public const string Manage = "tenancies.manage";
    }

    public static class Payments
    {
        public const string View = "payments.view";
        public const string Record = "payments.record";

        /// <summary>Void/reversal only -- payments are never hard-deleted, see design discussion.</summary>
        public const string Void = "payments.void";
    }

    public static class Expenses
    {
        public const string View = "expenses.view";
        public const string Manage = "expenses.manage";
    }

    public static class Documents
    {
        public const string View = "documents.view";
        public const string Manage = "documents.manage";
    }

    /// <summary>Reserved -- no MaintenanceRequest entity/workflow yet.</summary>
    public static class Maintenance
    {
        public const string View = "maintenance.view";
        public const string Create = "maintenance.create";
        public const string Manage = "maintenance.manage";
    }

    /// <summary>Reserved -- no messaging entity/workflow yet.</summary>
    public static class Messaging
    {
        public const string Send = "messaging.send";
        public const string View = "messaging.view";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class Organization
    {
        public const string View = "org.view";
        public const string ManageMembers = "org.manage_members";
        public const string ManageSettings = "org.manage_settings";

        /// <summary>Also gated by IsPrimaryOwner at the resource layer even where a role holds this permission.</summary>
        public const string Delete = "org.delete";
    }

    public static class Audit
    {
        /// <summary>Scope (own org vs all orgs) is resolved by the query layer per phase 27, not this permission.</summary>
        public const string View = "audit.view";
    }

    public static class Platform
    {
        /// <summary>Baseline platform-level access (org list, user management, system settings).</summary>
        public const string Admin = "platform.admin";
        public const string ManageOrganizations = "platform.manage_organizations";
        public const string ManageUsers = "platform.manage_users";
    }
}
