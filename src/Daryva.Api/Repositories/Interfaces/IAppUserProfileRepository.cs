using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IAppUserProfileRepository
{
    Task<AppUserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<Dictionary<string, AppUserProfile>> GetByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);

    void Add(AppUserProfile profile);
}
