using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IDepositReturnRepository
{
    Task<List<Guid>> GetTenancyIdsWithReturnAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyForTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    void Add(DepositReturn depositReturn);
}
