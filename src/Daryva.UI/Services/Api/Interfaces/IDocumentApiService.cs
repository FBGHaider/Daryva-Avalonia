namespace Daryva.Services.Api;

/// <summary>
/// DTO for Document API responses.
/// </summary>
public class DocumentDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public Guid? HouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileMimeType { get; set; }
    public string? StoragePath { get; set; }
    public string? Source { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for creating a new document.
/// </summary>
public class CreateDocumentDto
{
    public Guid? TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public Guid? HouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileMimeType { get; set; }
    public string? Source { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    /// <summary>Base64-encoded file content.</summary>
    public string? FileContent { get; set; }
}

/// <summary>
/// DTO for updating a document.
/// </summary>
public class UpdateDocumentDto
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Service for document-related API operations.
/// </summary>
public interface IDocumentApiService
{
    /// <summary>
    /// Get all documents for the current organization.
    /// </summary>
    Task<List<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get documents for a specific tenant.
    /// </summary>
    Task<List<DocumentDto>> GetDocumentsByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get documents for a specific tenancy.
    /// </summary>
    Task<List<DocumentDto>> GetDocumentsByTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get documents for a specific house.
    /// </summary>
    Task<List<DocumentDto>> GetDocumentsByHouseAsync(Guid houseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific document by ID.
    /// </summary>
    Task<DocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new document in the current organization.
    /// </summary>
    Task<DocumentDto> CreateDocumentAsync(CreateDocumentDto document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing document.
    /// </summary>
    Task<DocumentDto> UpdateDocumentAsync(Guid documentId, UpdateDocumentDto document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a document's file content.
    /// </summary>
    Task<byte[]?> DownloadDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a document.
    /// </summary>
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);
}
