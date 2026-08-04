using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class SupportAccessCodeRepository : ISupportAccessCodeRepository
{
    private readonly AppDbContext _dbContext;

    public SupportAccessCodeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<SupportAccessCode?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return _dbContext.SupportAccessCodes.FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);
    }

    public Task<string?> GetOrganizationNameAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Organizations
            .Where(o => o.Id == organizationId)
            .Select(o => o.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(SupportAccessCode accessCode)
    {
        _dbContext.SupportAccessCodes.Add(accessCode);
    }
}
