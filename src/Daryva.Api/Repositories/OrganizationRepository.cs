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
}
