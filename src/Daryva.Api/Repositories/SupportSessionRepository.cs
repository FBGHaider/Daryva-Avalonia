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

    public Task<SupportSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.SupportSessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<List<SupportSession>> ListAsync(Guid? organizationId, bool includeEnded, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SupportSessions.AsQueryable();

        if (organizationId.HasValue)
            query = query.Where(s => s.OrganizationId == organizationId.Value);

        if (!includeEnded)
            query = query.Where(s => s.EndedAt == null);

        return await query.OrderByDescending(s => s.StartedAt).ToListAsync(cancellationToken);
    }

    public Task<string?> GetOrganizationNameAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Organizations
            .Where(o => o.Id == organizationId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(SupportSession session)
    {
        _dbContext.SupportSessions.Add(session);
    }
}
