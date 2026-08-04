using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly AppDbContext _dbContext;

    public OrganizationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Organization organization) => _dbContext.Organizations.Add(organization);

    public void Remove(Organization organization) => _dbContext.Organizations.Remove(organization);

    public Task<OrganizationInvite?> GetInviteByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationInvites
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public void AddInvite(OrganizationInvite invite) => _dbContext.OrganizationInvites.Add(invite);

    public Task<List<OrganizationJoinCode>> GetActiveJoinCodesAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationJoinCodes
            .Where(c => c.OrganizationId == organizationId && c.RevokedAt == null)
            .ToListAsync(cancellationToken);

    public Task<OrganizationJoinCode?> GetJoinCodeByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationJoinCodes
            .Include(c => c.Organization)
            .FirstOrDefaultAsync(c => c.CodeHash == codeHash, cancellationToken);

    public void AddJoinCode(OrganizationJoinCode joinCode) => _dbContext.OrganizationJoinCodes.Add(joinCode);

    public async Task<(List<Organization> Items, int TotalCount)> SearchAllAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Support Mode's org browse is a targeted lookup ("find the org whose owner emailed us a
        // support request"), not a directory -- an admin should never see every organization on the
        // platform just by opening the page. No search term means no results, not "everything".
        if (string.IsNullOrWhiteSpace(search))
            return (new List<Organization>(), 0);

        var term = search.Trim().ToLower();
        var query = _dbContext.Organizations
            .AsNoTracking()
            .Where(o => _dbContext.OrganizationMembers
                .Any(m => m.OrganizationId == o.Id && m.Email != null && m.Email.ToLower().Contains(term)));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
