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

    void Add(Tenant tenant);

    void Remove(Tenant tenant);
}
