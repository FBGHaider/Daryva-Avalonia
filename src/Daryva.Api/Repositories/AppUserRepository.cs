using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class AppUserRepository : IAppUserRepository
{
    private readonly AppDbContext _dbContext;

    public AppUserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<AppUser?> GetByEmailVerificationTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.FirstOrDefaultAsync(u => u.EmailVerificationTokenHash == tokenHash, cancellationToken);

    public Task<AppUser?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _dbContext.AppUsers.FirstOrDefaultAsync(u => u.PasswordResetTokenHash == tokenHash, cancellationToken);

    public void Add(AppUser user) => _dbContext.AppUsers.Add(user);
}
