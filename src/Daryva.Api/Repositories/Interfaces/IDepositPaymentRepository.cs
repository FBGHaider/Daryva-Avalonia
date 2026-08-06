using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IDepositPaymentRepository
{
    /// <summary>Every deposit payment for the org, including voided ones -- backs the backup export
    /// (which exports IsVoided as data rather than dropping voided rows, unlike every other method here).</summary>
    Task<List<DepositPayment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Sum of non-voided deposit payments per tenancy, for the given tenancies.</summary>
    Task<Dictionary<Guid, decimal>> GetTotalsByTenancyIdAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken = default);

    /// <summary>Non-voided deposit payments for the given tenancies, ordered by DatePaid descending -- for the deposit ledger's per-payment detail.</summary>
    Task<List<DepositPayment>> GetByTenancyIdsAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken = default);

    /// <summary>Tracked, explicit org check -- write-path lookup for voiding. Explicit orgId is kept even
    /// though the global query filter already scopes this, matching the rest of the payment-voiding
    /// code: this is money, and staying one .IgnoreQueryFilters() away from a cross-tenant bug isn't
    /// good enough here.</summary>
    Task<DepositPayment?> GetTrackedByIdAsync(Guid orgId, Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-voids every non-voided deposit payment for the org (ExecuteUpdateAsync -- bypasses
    /// the change tracker, commits independently of any other SaveChangesAsync). Caller must wrap this
    /// in an explicit transaction alongside the corresponding audit log save.</summary>
    Task<int> VoidAllForOrgAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>Filtered, Tenancy.Tenant/Tenancy.House included -- backs the transactions list.</summary>
    Task<List<DepositPayment>> QueryTransactionsAsync(
        DateTime? startDateUtc,
        DateTime? endDateExclusiveUtc,
        bool endDateIsDateOnly,
        Guid? houseId,
        Guid? tenantId,
        string? normalizedMethod,
        CancellationToken cancellationToken = default);

    void Add(DepositPayment payment);

    void Update(DepositPayment payment);

    void Remove(DepositPayment payment);
}
