using Daryva.Api.Data;
using Daryva.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

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

public class DocumentService : IDocumentService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(AppDbContext dbContext, ILogger<DocumentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<Document>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetDocumentsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetDocumentsByTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.TenancyId == tenancyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetDocumentsByHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.HouseId == houseId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
    }

    public async Task<Document> CreateDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task UpdateDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        _dbContext.Documents.Update(document);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetDocumentByIdAsync(documentId, cancellationToken);
        if (document != null)
        {
            _dbContext.Documents.Remove(document);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
