using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface ITenancyRepository
{
    Task<List<Tenancy>> GetActiveByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default);

    /// <summary>Most recent active tenancy for a tenant, with House included -- used to resolve "where do they currently live".</summary>
    Task<Tenancy?> GetLatestActiveWithHouseByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    void Add(Tenancy tenancy);

    void Update(Tenancy tenancy);

    void Remove(Tenancy tenancy);
}
