using Daryva.Api.Domain;

namespace Daryva.Api.Repositories.Interfaces;

public interface IDocumentRepository
{
    Task<List<Document>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<Document>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<List<Document>> GetByTenancyIdAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    Task<List<Document>> GetByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    void Add(Document document);

    void Update(Document document);

    void Remove(Document document);
}
