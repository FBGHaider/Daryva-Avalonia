using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface ITenancyRepository
{
    Task<List<Tenancy>> GetActiveByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default);

    /// <summary>Most recent active tenancy for a tenant, with House included -- used to resolve "where do they currently live".</summary>
    Task<Tenancy?> GetLatestActiveWithHouseByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Tenancy?> GetByIdAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    /// <summary>Tenancies (with Tenant/House included) active during [periodStart, periodEnd], for non-archived tenants,
    /// optionally restricted to a house and/or a tenant-name/address search term. Backs the rent ledger.</summary>
    Task<List<Tenancy>> GetForRentLedgerAsync(
        DateTime periodStart,
        DateTime periodEnd,
        Guid? houseId,
        string? searchTerm,
        CancellationToken cancellationToken = default);

    /// <summary>All tenancy ids sharing the same (tenant, house) pair -- ledger/payment totals are summed across this group.</summary>
    Task<List<Guid>> GetIdsInSameGroupAsync(Guid tenantId, Guid houseId, CancellationToken cancellationToken = default);

    void Add(Tenancy tenancy);

    void Update(Tenancy tenancy);

    void Remove(Tenancy tenancy);
}
