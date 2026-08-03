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

    public Task<List<OrganizationMember>> GetByUserIdWithOrganizationAsync(string userId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Organization)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.AnyAsync(m => m.UserId == userId, cancellationToken);

    public Task<OrganizationMember?> GetByEmailAsync(Guid organizationId, string email, CancellationToken cancellationToken = default)
        => _dbContext.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.Email == email, cancellationToken);

    public void Add(OrganizationMember member) => _dbContext.OrganizationMembers.Add(member);
}
