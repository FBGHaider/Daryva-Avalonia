namespace Daryva.Api.Security.Interfaces;

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
    /// The caller's effective org role for CurrentOrgId (e.g. Security.Roles.Landlord), or null
    /// if not resolved / no access. This is the caller's OrganizationMember.Role when they're a
    /// real member -- OR, if they're a platform admin (IsPlatformAdmin) with an active Support
    /// Session on CurrentOrgId, an elevated Roles.Landlord for the duration of that session.
    /// Populated by ResolveCurrentRoleAsync -- reading this before that runs returns null even
    /// for an actual member, same as CurrentOrgId before SetCurrentOrgId runs.
    /// </summary>
    string? CurrentRole { get; }

    /// <summary>
    /// The caller's OrganizationMember.IsPrimaryOwner for CurrentOrgId. False when CurrentRole
    /// came from Support Session elevation rather than real membership. Same population caveat
    /// as CurrentRole.
    /// </summary>
    bool IsPrimaryOwnerOfCurrentOrg { get; }

    /// <summary>
    /// Whether AppUser.IsPlatformAdmin is set for the caller. Independent of CurrentOrgId --
    /// platform-level actions (org list, user management) don't require an org context. Same
    /// population caveat as CurrentRole.
    /// </summary>
    bool IsPlatformAdmin { get; }

    /// <summary>
    /// Set when CurrentRole came from Support Session elevation (not real org membership).
    /// Used to tag AuditLog rows so the elevated action is visible in the landlord's own org
    /// audit trail. Null outside an active elevation.
    /// </summary>
    Guid? ActiveSupportSessionId { get; }

    /// <summary>
    /// Set when CurrentRole is Roles.Tenant -- the Tenant.Id (not AppUser.Id) the caller is
    /// linked to for CurrentOrgId. Null for every other role. Controllers that expose data to
    /// the Tenant role must use this to scope their own queries (documents, tenancy, payments)
    /// -- the global org-wide query filter alone is not enough to isolate one tenant from
    /// another within the same org. Same population caveat as CurrentRole.
    /// </summary>
    Guid? CurrentTenantId { get; }

    /// <summary>
    /// Set the current organization context (used by middleware after validation).
    /// Resets any previously resolved CurrentRole/IsPrimaryOwnerOfCurrentOrg/IsPlatformAdmin/
    /// ActiveSupportSessionId/CurrentTenantId.
    /// </summary>
    void SetCurrentOrgId(Guid? orgId);

    /// <summary>
    /// Resolves IsPlatformAdmin, then CurrentRole/IsPrimaryOwnerOfCurrentOrg/
    /// ActiveSupportSessionId/CurrentTenantId, from the database for the current (UserId, CurrentOrgId) pair.
    /// IsPlatformAdmin resolves even if CurrentOrgId is null. Called once per request by
    /// TenantContextMiddleware, after SetCurrentOrgId, so the rest of the pipeline can read
    /// these as plain sync properties -- mirroring how CurrentOrgId itself is resolved once and
    /// read many times. Cached: calling this again without an intervening SetCurrentOrgId is a
    /// no-op.
    /// </summary>
    Task ResolveCurrentRoleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the user belongs to a given org (not necessarily CurrentOrgId). Real DB lookup.
    /// </summary>
    Task<bool> IsMemberOfOrgAsync(Guid orgId, CancellationToken cancellationToken = default);
}
