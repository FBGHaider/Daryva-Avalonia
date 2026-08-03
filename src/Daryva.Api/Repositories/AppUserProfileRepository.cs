using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class AppUserProfileRepository : IAppUserProfileRepository
{
    private readonly AppDbContext _dbContext;

    public AppUserProfileRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<AppUserProfile?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
        => _dbContext.AppUserProfiles.FindAsync(new object[] { userId }, cancellationToken).AsTask();

    public async Task<Dictionary<string, AppUserProfile>> GetByIdsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToList();
        return await _dbContext.AppUserProfiles
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);
    }
}
