using Daryva.MVVM.Models;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service interface for Document operations.
    /// </summary>
    public interface IDocumentService
    {
        /// <summary>
        /// Gets all documents, optionally filtered by owner type and document type.
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentsAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null, string? type = null);

        /// <summary>
        /// Gets a document by ID.
        /// </summary>
        Task<Document?> GetDocumentByIdAsync(int documentId);

        /// <summary>
        /// Gets documents for a tenant.
        /// </summary>
        Task<IEnumerable<Document>> GetTenantDocumentsAsync(int tenantId);

        /// <summary>
        /// Gets documents for a tenancy.
        /// </summary>
        Task<IEnumerable<Document>> GetTenancyDocumentsAsync(int tenancyId);

        /// <summary>
        /// Gets documents for a house.
        /// </summary>
        Task<IEnumerable<Document>> GetHouseDocumentsAsync(int houseId);

        /// <summary>
        /// Gets documents that are expiring soon.
        /// </summary>
        Task<IEnumerable<Document>> GetExpiringDocumentsAsync(int daysAhead = 30);

        /// <summary>
        /// Gets the document status checklist for an owner.
        /// </summary>
        Task<IEnumerable<DocumentStatusItem>> GetDocumentStatusChecklistAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null);

        /// <summary>
        /// Uploads a document with file handling.
        /// </summary>
        Task<Document> UploadDocumentAsync(Document document, byte[] fileBytes, string? storageRootPath = null);

        /// <summary>
        /// Updates a document.
        /// </summary>
        Task UpdateDocumentAsync(Document document);

        /// <summary>
        /// Deletes a document (including file if stored on disk).
        /// </summary>
        Task<bool> DeleteDocumentAsync(int documentId, string? storageRootPath = null);

        /// <summary>
        /// Gets document file bytes (from storage path or database).
        /// </summary>
        Task<byte[]?> GetDocumentFileBytesAsync(int documentId, string? storageRootPath = null);

        /// <summary>
        /// Gets document history for a specific document type and owner.
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentHistoryAsync(int? tenantId, int? tenancyId, int? houseId, string type);
    }

    /// <summary>
    /// Represents a document status item for checklist views.
    /// </summary>
    public class DocumentStatusItem
    {
        public string Type { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Missing, Active, Expired, ExpiringSoon
        public DateTime? UploadedDate { get; set; }
        public DateTime? ValidTo { get; set; }
        public int? DocumentId { get; set; }
        public int Version { get; set; }
    }
}
