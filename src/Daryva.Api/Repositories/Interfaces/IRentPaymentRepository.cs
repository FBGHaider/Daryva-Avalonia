using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IRentPaymentRepository
{
    /// <summary>Non-voided rent payments for the given tenancies with DatePaid in [periodStartUtc, periodEndExclusiveUtc).</summary>
    Task<List<RentPayment>> GetForPeriodAsync(
        IReadOnlyCollection<Guid> tenancyIds,
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancellationToken = default);

    void Add(RentPayment payment);

    void Update(RentPayment payment);

    void Remove(RentPayment payment);
}
