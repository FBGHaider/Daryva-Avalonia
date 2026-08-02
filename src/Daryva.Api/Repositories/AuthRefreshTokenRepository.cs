using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class AuthRefreshTokenRepository : IAuthRefreshTokenRepository
{
    private readonly AppDbContext _dbContext;

    public AuthRefreshTokenRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AuthRefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _dbContext.AuthRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<AuthRefreshToken?> GetByTokenHashWithUserAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _dbContext.AuthRefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public Task<List<AuthRefreshToken>> GetActiveSessionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _dbContext.AuthRefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(AuthRefreshToken session) => _dbContext.AuthRefreshTokens.Add(session);
}
