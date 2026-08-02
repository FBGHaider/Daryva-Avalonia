using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Daryva.Api.Services;

public class DocumentService : IDocumentService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;

    public DocumentService(AppDbContext dbContext, ILogger<DocumentService> logger, ITenantContext tenantContext, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
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
            LogAudit(AuditEventTypes.DocumentDeleted, document.OrganizationId, nameof(Document), document.Id.ToString());
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }
}
