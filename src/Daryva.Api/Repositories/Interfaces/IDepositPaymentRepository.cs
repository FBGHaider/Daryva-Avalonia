using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IDepositPaymentRepository
{
    /// <summary>Sum of non-voided deposit payments per tenancy, for the given tenancies.</summary>
    Task<Dictionary<Guid, decimal>> GetTotalsByTenancyIdAsync(IReadOnlyCollection<Guid> tenancyIds, CancellationToken cancellationToken = default);

    void Add(DepositPayment payment);

    void Update(DepositPayment payment);

    void Remove(DepositPayment payment);
}
