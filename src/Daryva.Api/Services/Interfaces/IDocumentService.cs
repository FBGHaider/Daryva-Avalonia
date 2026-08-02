using Daryva.Api.Domain;

namespace Daryva.Api.Services.Interfaces;

public interface IDocumentService
{
    Task<IEnumerable<Document>> GetAllDocumentsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetDocumentsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetDocumentsByTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetDocumentsByHouseAsync(Guid houseId, CancellationToken cancellationToken = default);
    Task<Document?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<Document> CreateDocumentAsync(Document document, CancellationToken cancellationToken = default);
    Task UpdateDocumentAsync(Document document, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
