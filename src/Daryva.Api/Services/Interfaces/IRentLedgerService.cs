using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Single source of truth for rent ledger entries (who has rent due in a period and how much).
/// Used by the Rent & Payments tab and by house stats so house monthly rent matches the ledger.
/// Rent totals and status use the same (tenant, house) grouping so they stay in sync with the ledger.
/// </summary>
public interface IRentLedgerService
{
    /// <summary>
    /// Returns the same entries that appear in the rent ledger for the given period.
    /// Dedupes by tenant+house, respects rent start period and move-out, excludes archived tenants.
    /// </summary>
    Task<IReadOnlyList<RentLedgerItemResponse>> GetRentLedgerEntriesAsync(
        int year,
        int month,
        Guid? houseId = null,
        string? statusFilter = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all tenancy IDs that share the same (tenant, house) as the given tenancy.
    /// Used so rent total/status for a period sum payments across the same group as the ledger.
    /// </summary>
    Task<IReadOnlyList<Guid>?> GetTenancyIdsInSameGroupAsync(Guid tenancyId, CancellationToken cancellationToken = default);
}
