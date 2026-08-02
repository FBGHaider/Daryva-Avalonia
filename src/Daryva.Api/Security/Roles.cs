namespace Daryva.Api.Security;

/// <summary>
/// Logical roles used for authorization -- distinct from OrganizationMember.Roles, which
/// only lists valid values for the OrganizationMember.Role database column. Admin is never
/// an OrganizationMember.Role value: it comes from AppUser.IsPlatformAdmin, resolved
/// independently of org membership. Tenant is reserved -- no tenant portal/account exists
/// yet, so nothing currently resolves a caller to this role, but RolePermissions is ready
/// for when it does.
///
/// Landlord must stay equal to OrganizationMember.Roles.Landlord (Domain/OrganizationMember.cs)
/// -- kept as a separate literal rather than a cross-reference so this Security-layer type
/// doesn't depend on Domain, but the two must not drift apart.
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Landlord = "Landlord";
    public const string Tenant = "Tenant";
}
