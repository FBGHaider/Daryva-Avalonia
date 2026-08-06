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

    /// <summary>Tenant/House included -- the list view shape, optionally filtered.</summary>
    Task<List<Tenancy>> GetAllWithDetailsAsync(Guid? tenantId, Guid? houseId, bool? activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Tenant/House included -- tenancies active at any point during [periodStart, periodEndExclusive).</summary>
    Task<List<Tenancy>> GetActiveInPeriodWithDetailsAsync(DateTime periodStart, DateTime periodEndExclusive, CancellationToken cancellationToken = default);

    Task<Tenancy?> GetByIdWithDetailsAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    Task<List<Tenancy>> GetEndedWithDepositAsync(CancellationToken cancellationToken = default);

    /// <summary>Tenant/House included, ordered by tenant name then house address -- for the rent-repair export.</summary>
    Task<List<Tenancy>> GetAllWithDetailsForExportAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked, no includes -- for in-place mutation (end/reactivate/update/delete).</summary>
    Task<Tenancy?> GetTrackedByIdAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, no includes -- for bulk in-place mutation (e.g. rent repair).</summary>
    Task<List<Tenancy>> GetTrackedByIdsAsync(IEnumerable<Guid> tenancyIds, CancellationToken cancellationToken = default);

    /// <summary>Tracked -- for bulk removal.</summary>
    Task<List<Tenancy>> GetTrackedByHouseIdAsync(Guid houseId, bool endedOnly, CancellationToken cancellationToken = default);

    /// <summary>Tenant/House included, for non-archived tenants who moved in on or before periodEnd (no
    /// move-out lower bound, unlike the rent ledger -- deposit history should still show for tenancies
    /// that ended earlier), optionally restricted to a house and/or a tenant-name/address search term.</summary>
    Task<List<Tenancy>> GetForDepositLedgerAsync(DateTime periodEnd, Guid? houseId, string? searchTerm, CancellationToken cancellationToken = default);

    /// <summary>Tenant/House included -- ended tenancies (with a real move-out year) that still have a
    /// deposit amount set and aren't already in excludeTenancyIds (already has a recorded return).</summary>
    Task<List<Tenancy>> GetEndedWithDepositExcludingAsync(IReadOnlyCollection<Guid> excludeTenancyIds, int minValidLeaveYear, CancellationToken cancellationToken = default);

    void Add(Tenancy tenancy);

    void Update(Tenancy tenancy);

    void Remove(Tenancy tenancy);

    void RemoveRange(IEnumerable<Tenancy> tenancies);
}
