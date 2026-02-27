using Daryva.MVVM.Models;
using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// Adapter that implements IDocumentService using the backend API.
/// Maps between UI Document model and API DocumentDto.
/// Replaces the SQLite-based DocumentService when using API backend.
/// </summary>
public class DocumentApiServiceAdapter : IDocumentService
{
    private readonly IDocumentApiService _documentApiService;
    private readonly IApiEntityIdMapper _idMapper;

    public DocumentApiServiceAdapter(IDocumentApiService documentApiService, IApiEntityIdMapper idMapper)
    {
        _documentApiService = documentApiService ?? throw new ArgumentNullException(nameof(documentApiService));
        _idMapper = idMapper ?? throw new ArgumentNullException(nameof(idMapper));
    }

    public async Task<IEnumerable<Document>> GetDocumentsAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null, string? type = null)
    {
        var documentDtos = await _documentApiService.GetDocumentsAsync();
        var documents = documentDtos.Select(MapToDocument).ToList();

        // Filter client-side
        if (tenantId.HasValue)
            documents = documents.Where(d => d.TenantId == tenantId.Value).ToList();

        if (tenancyId.HasValue)
            documents = documents.Where(d => d.TenancyId == tenancyId.Value).ToList();

        if (houseId.HasValue)
            documents = documents.Where(d => d.HouseId == houseId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(type))
            documents = documents.Where(d => d.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

        return documents;
    }

    public async Task<Document?> GetDocumentByIdAsync(int documentId)
    {
        var documents = await GetDocumentsAsync();
        return documents.FirstOrDefault(d => d.DocumentId == documentId);
    }

    public async Task<IEnumerable<Document>> GetTenantDocumentsAsync(int tenantId)
    {
        return await GetDocumentsAsync(tenantId: tenantId);
    }

    public async Task<IEnumerable<Document>> GetTenancyDocumentsAsync(int tenancyId)
    {
        return await GetDocumentsAsync(tenancyId: tenancyId);
    }

    public async Task<IEnumerable<Document>> GetHouseDocumentsAsync(int houseId)
    {
        return await GetDocumentsAsync(houseId: houseId);
    }

    public async Task<IEnumerable<Document>> GetExpiringDocumentsAsync(int daysAhead = 30)
    {
        var documents = await GetDocumentsAsync();
        var expiringDate = DateTime.Now.AddDays(daysAhead);
        return documents.Where(d => d.ValidTo.HasValue && d.ValidTo.Value <= expiringDate && d.IsActive);
    }

    public async Task<IEnumerable<DocumentStatusItem>> GetDocumentStatusChecklistAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null)
    {
        var documents = (await GetDocumentsAsync(tenantId, tenancyId, houseId)).ToList();
        
        var statusItems = new List<DocumentStatusItem>();
        var groupedByType = documents.GroupBy(d => d.Type);

        foreach (var typeGroup in groupedByType)
        {
            var latest = typeGroup.OrderByDescending(d => d.UploadedAt).FirstOrDefault();
            if (latest != null)
            {
                statusItems.Add(new DocumentStatusItem
                {
                    Type = latest.Type,
                    DisplayName = latest.DisplayName,
                    UploadedDate = latest.UploadedAt,
                    ValidTo = latest.ValidTo,
                    DocumentId = latest.DocumentId,
                    Version = latest.Version,
                    Status = DeterminateDocumentStatus(latest)
                });
            }
        }

        return statusItems;
    }

    public IEnumerable<string> GetDocumentTypesForOwner(string owner)
    {
        // Return standard document types (this would normally come from backend)
        return new[]
        {
            "StudentConfirmationLetter",
            "PhotoId",
            "RightToRent",
            "TenancyAgreementSigned",
            "GuarantorAgreement",
            "InventoryCheckIn",
            "DepositProtectionCertificate",
            "NoticeToLeave",
            "Other"
        };
    }

    public async Task<Document> UploadDocumentAsync(Document document, byte[] fileBytes, string? storageRootPath = null)
    {
        var createDto = new CreateDocumentDto
        {
            TenantId = document.ApiTenantId,
            TenancyId = document.ApiTenancyId,
            HouseId = document.ApiHouseId,
            Type = document.Type,
            DisplayName = document.DisplayName,
            FileName = document.FileName,
            FileMimeType = document.FileMimeType,
            Source = document.Source ?? "Uploaded",
            UploadedAt = document.UploadedAt,
            ValidFrom = document.ValidFrom,
            ValidTo = document.ValidTo,
            FileContent = Convert.ToBase64String(fileBytes)
        };

        var createdDto = await _documentApiService.CreateDocumentAsync(createDto);
        return MapToDocument(createdDto);
    }

    public async Task UpdateDocumentAsync(Document document)
    {
        if (!document.ApiId.HasValue)
            throw new InvalidOperationException("Cannot update document without API ID.");

        var updateDto = new UpdateDocumentDto
        {
            DisplayName = document.DisplayName,
            ValidFrom = document.ValidFrom,
            ValidTo = document.ValidTo,
            IsActive = document.IsActive
        };

        var updatedDto = await _documentApiService.UpdateDocumentAsync(document.ApiId.Value, updateDto);
        
        // Update the document object with response data
        document.DisplayName = updatedDto.DisplayName;
        document.ValidFrom = updatedDto.ValidFrom;
        document.ValidTo = updatedDto.ValidTo;
        document.IsActive = updatedDto.IsActive;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId, string? storageRootPath = null)
    {
        var document = await GetDocumentByIdAsync(documentId);
        if (document == null || !document.ApiId.HasValue)
            return false;

        return await _documentApiService.DeleteDocumentAsync(document.ApiId.Value);
    }

    public async Task<byte[]?> GetDocumentFileBytesAsync(int documentId, string? storageRootPath = null)
    {
        var document = await GetDocumentByIdAsync(documentId);
        if (document == null || !document.ApiId.HasValue)
            return null;

        return await _documentApiService.DownloadDocumentAsync(document.ApiId.Value);
    }

    public async Task<IEnumerable<Document>> GetDocumentHistoryAsync(int? tenantId, int? tenancyId, int? houseId, string type)
    {
        var documents = (await GetDocumentsAsync(tenantId, tenancyId, houseId, type)).ToList();
        return documents.OrderByDescending(d => d.UploadedAt);
    }

    /// <summary>
    /// Map DocumentDto from API to UI Document model.
    /// Assigns a local int ID based on the hash of the Guid.
    /// </summary>
    private Document MapToDocument(DocumentDto dto)
    {
        return new Document
        {
            DocumentId = dto.Id.GetHashCode(),
            ApiId = dto.Id,
            TenantId = dto.TenantId.HasValue ? (int?)_idMapper.MapTenantId(dto.TenantId.Value) : null,
            ApiTenantId = dto.TenantId,
            TenancyId = dto.TenancyId.HasValue ? (int?)_idMapper.MapTenancyId(dto.TenancyId.Value) : null,
            ApiTenancyId = dto.TenancyId,
            HouseId = dto.HouseId.HasValue ? (int?)_idMapper.MapHouseId(dto.HouseId.Value) : null,
            ApiHouseId = dto.HouseId,
            Type = dto.Type,
            DisplayName = dto.DisplayName,
            FileName = dto.FileName,
            FileMimeType = dto.FileMimeType,
            StoragePath = dto.StoragePath,
            Source = dto.Source,
            UploadedAt = dto.UploadedAt,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            Version = dto.Version,
            IsActive = dto.IsActive
        };
    }

    /// <summary>
    /// Determine the status of a document (Missing, Active, Expired, ExpiringSoon).
    /// </summary>
    private string DeterminateDocumentStatus(Document document)
    {
        if (!document.IsActive)
            return "Missing";

        if (document.ValidTo.HasValue)
        {
            if (document.ValidTo.Value < DateTime.Now)
                return "Expired";

            if (document.ValidTo.Value <= DateTime.Now.AddDays(30))
                return "ExpiringSoon";
        }

        return "Active";
    }
}
