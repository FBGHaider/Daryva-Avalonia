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
}
