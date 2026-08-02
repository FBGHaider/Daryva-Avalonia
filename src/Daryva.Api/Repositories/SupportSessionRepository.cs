using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class SupportSessionRepository : ISupportSessionRepository
{
    private readonly AppDbContext _dbContext;

    public SupportSessionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SupportSession?> GetActiveSessionAsync(Guid adminUserId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return _dbContext.SupportSessions
            .Where(s => s.AdminUserId == adminUserId
                && s.OrganizationId == organizationId
                && s.EndedAt == null
                && s.ExpiresAt > now)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
