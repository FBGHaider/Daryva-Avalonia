using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IRentPaymentRepository
{
    /// <summary>Every rent payment for the org, including voided ones -- backs the backup export
    /// (which exports IsVoided as data rather than dropping voided rows, unlike every other method here).</summary>
    Task<List<RentPayment>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Non-voided rent payments for the given tenancies with DatePaid in [periodStartUtc, periodEndExclusiveUtc).</summary>
    Task<List<RentPayment>> GetForPeriodAsync(
        IReadOnlyCollection<Guid> tenancyIds,
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Sum of non-voided rent payments drawn from deposit (PaidFromDeposit), per tenancy, for the
    /// given tenancies. Every "deposit remaining" figure must subtract this from the raw deposit-paid
    /// total -- see RentPayment.PaidFromDeposit's doc comment.</summary>
    Task<Dictionary<Guid, decimal>> GetTotalsPaidFromDepositByTenancyIdAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken = default);

    /// <summary>Tracked, explicit org check -- write-path lookup for voiding. Explicit orgId is kept even
    /// though the global query filter already scopes this, matching the rest of the payment-voiding
    /// code: this is money, and staying one .IgnoreQueryFilters() away from a cross-tenant bug isn't
    /// good enough here.</summary>
    Task<RentPayment?> GetTrackedByIdAsync(Guid orgId, Guid paymentId, CancellationToken cancellationToken = default);

    /// <summary>Bulk-voids every non-voided rent payment for the org (ExecuteUpdateAsync -- bypasses the
    /// change tracker, commits independently of any other SaveChangesAsync). Caller must wrap this in an
    /// explicit transaction alongside the corresponding audit log save.</summary>
    Task<int> VoidAllForOrgAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>Filtered, Tenancy.Tenant/Tenancy.House included -- backs the transactions list.</summary>
    Task<List<RentPayment>> QueryTransactionsAsync(
        DateTime? startDateUtc,
        DateTime? endDateExclusiveUtc,
        bool endDateIsDateOnly,
        Guid? houseId,
        Guid? tenantId,
        string? normalizedMethod,
        CancellationToken cancellationToken = default);

    void Add(RentPayment payment);

    void Update(RentPayment payment);

    void Remove(RentPayment payment);
}
