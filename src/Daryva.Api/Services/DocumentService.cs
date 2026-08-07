using Daryva.Api.Domain;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security.Interfaces;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DocumentService> _logger;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogger _auditLogger;
    private readonly IFileStorageService _fileStorageService;

    public DocumentService(
        IDocumentRepository documentRepository,
        IUnitOfWork unitOfWork,
        ILogger<DocumentService> logger,
        ITenantContext tenantContext,
        IAuditLogger auditLogger,
        IFileStorageService fileStorageService)
    {
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _tenantContext = tenantContext;
        _auditLogger = auditLogger;
        _fileStorageService = fileStorageService;
    }

    public async Task<IEnumerable<Document>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
        => await _documentRepository.GetAllAsync(cancellationToken);

    public async Task<IEnumerable<Document>> GetDocumentsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await _documentRepository.GetByTenantIdAsync(tenantId, cancellationToken);

    public async Task<IEnumerable<Document>> GetDocumentsByTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default)
        => await _documentRepository.GetByTenancyIdAsync(tenancyId, cancellationToken);

    public async Task<IEnumerable<Document>> GetDocumentsByHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
        => await _documentRepository.GetByHouseIdAsync(houseId, cancellationToken);

    public async Task<Document?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
        => await _documentRepository.GetByIdAsync(documentId, cancellationToken);

    public async Task<Document> CreateDocumentAsync(Document document, byte[]? fileContent, CancellationToken cancellationToken = default)
    {
        if (fileContent != null && fileContent.Length > 0)
        {
            document.StoragePath = await _fileStorageService.SaveAsync(
                document.OrganizationId, document.Id, document.FileName, fileContent, cancellationToken);
        }

        _documentRepository.Add(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task UpdateDocumentAsync(Document document, CancellationToken cancellationToken = default)
    {
        _documentRepository.Update(document);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document != null)
        {
            _documentRepository.Remove(document);
            LogAudit(AuditEventTypes.DocumentDeleted, document.OrganizationId, nameof(Document), document.Id.ToString());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _fileStorageService.DeleteAsync(document.StoragePath, cancellationToken);
        }
    }

    public Task<byte[]?> GetFileContentAsync(Document document, CancellationToken cancellationToken = default)
        => _fileStorageService.ReadAsync(document.StoragePath, cancellationToken);

    private void LogAudit(string eventType, Guid organizationId, string targetType, string targetId)
    {
        if (!Guid.TryParse(_tenantContext.UserId, out var actorId))
            return;

        _auditLogger.Log(actorId, _tenantContext.CurrentRole ?? "Unknown", eventType,
            organizationId: organizationId, targetType: targetType, targetId: targetId,
            supportSessionId: _tenantContext.ActiveSupportSessionId);
    }
}
