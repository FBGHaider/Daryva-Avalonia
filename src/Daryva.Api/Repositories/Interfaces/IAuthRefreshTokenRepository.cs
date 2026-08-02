using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IAuthRefreshTokenRepository
{
    Task<AuthRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<AuthRefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<List<AuthRefreshToken>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(AuthRefreshToken session);
}
