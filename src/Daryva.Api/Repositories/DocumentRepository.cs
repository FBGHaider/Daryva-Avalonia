using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Repositories;

public class DocumentRepository : OrgScopedRepository<Document>, IDocumentRepository
{
    public DocumentRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Task<List<Document>> GetAllAsync(CancellationToken cancellationToken = default)
        => Set.AsNoTracking().ToListAsync(cancellationToken);

    public Task<List<Document>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().Where(d => d.TenantId == tenantId).ToListAsync(cancellationToken);

    public Task<List<Document>> GetByTenancyIdAsync(Guid tenancyId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().Where(d => d.TenancyId == tenancyId).ToListAsync(cancellationToken);

    public Task<List<Document>> GetByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().Where(d => d.HouseId == houseId).ToListAsync(cancellationToken);

    public Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
}
