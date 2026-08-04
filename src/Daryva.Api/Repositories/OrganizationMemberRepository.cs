using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class OrganizationMemberRepository : IOrganizationMemberRepository
{
    private readonly AppDbContext _dbContext;

    public OrganizationMemberRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrganizationMember?> GetMembershipAsync(string userId, Guid organizationId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, cancellationToken);

    public Task<List<OrganizationMember>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.Where(m => m.OrganizationId == organizationId).ToListAsync(cancellationToken);

    public Task<List<OrganizationMember>> GetByOrganizationIdsAsync(IEnumerable<Guid> organizationIds, CancellationToken cancellationToken = default)
    {
        var ids = organizationIds.Distinct().ToList();
        return _dbContext.OrganizationMembers.Where(m => ids.Contains(m.OrganizationId)).ToListAsync(cancellationToken);
    }

    public Task<List<OrganizationMember>> GetByUserIdWithOrganizationAsync(string userId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Organization)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.AnyAsync(m => m.UserId == userId, cancellationToken);

    public Task<OrganizationMember?> GetByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.Email == email, cancellationToken);

    public Task<List<OrganizationMember>> SearchByEmailAsync(string emailTerm, int maxResults, CancellationToken cancellationToken = default)
    {
        var term = emailTerm.Trim().ToLower();
        return _dbContext.OrganizationMembers
            .Where(m => m.Email != null && m.Email.ToLower().Contains(term))
            .OrderBy(m => m.Email)
            .Take(maxResults)
            .ToListAsync(cancellationToken);
    }

    public void Add(OrganizationMember member) => _dbContext.OrganizationMembers.Add(member);
}
