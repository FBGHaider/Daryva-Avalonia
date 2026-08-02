using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IAppUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByEmailVerificationTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<AppUser?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    void Add(AppUser user);
}
