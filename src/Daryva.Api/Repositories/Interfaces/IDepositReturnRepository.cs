using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IDepositReturnRepository
{
    Task<List<Guid>> GetTenancyIdsWithReturnAsync(CancellationToken cancellationToken = default);

    /// <summary>Every deposit return for the org -- backs the backup export.</summary>
    Task<List<DepositReturn>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyForTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    void Add(DepositReturn depositReturn);
}
