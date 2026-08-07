using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface ITenantRepository
{
    /// <summary>Includes Tenancies -> House -- callers list tenants alongside their tenancy history.</summary>
    Task<List<Tenant>> GetAllAsync(bool includeArchived, CancellationToken cancellationToken = default);

    Task<Tenant?> GetByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, no Include -- for simple in-place field updates.</summary>
    Task<Tenant?> GetTrackedByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tracked with Tenancies included -- for operations that also need to mutate the tenant's tenancies (e.g. archiving).</summary>
    Task<Tenant?> GetTrackedWithTenanciesByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Non-archived tenants, optionally restricted to a specific set of ids (null = all). Used to build notification recipient lists.</summary>
    Task<List<Tenant>> GetActiveAsync(IReadOnlyCollection<Guid>? tenantIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every Tenant row linked to this AppUser, across all organizations. Always bypasses the
    /// org-scoped query filter (IgnoreQueryFilters) -- callers use this precisely because no org
    /// context is established yet (role resolution, middleware org auto-selection) or shouldn't
    /// be assumed (a tenant could in principle be invited by more than one landlord org).
    /// </summary>
    Task<List<Tenant>> GetAllByAppUserIdAsync(Guid appUserId, CancellationToken cancellationToken = default);

    /// <summary>Bypasses the org-scoped query filter -- looked up from the public accept-invite endpoint, before any org context exists.</summary>
    Task<Tenant?> GetByInviteTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Add(Tenant tenant);

    void Remove(Tenant tenant);
}
