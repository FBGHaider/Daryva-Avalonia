using FBGRentora.MVVM.Models;

namespace FBGRentora.Services.Data
{
    /// <summary>
    /// Repository interface for Document operations.
    /// </summary>
    public interface IDocumentRepository
    {
        /// <summary>
        /// Gets all documents for a tenant.
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentsByTenantIdAsync(int tenantId);

        /// <summary>
        /// Gets all documents for a tenancy.
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentsByTenancyIdAsync(int tenancyId);

        /// <summary>
        /// Gets all documents for a house.
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentsByHouseIdAsync(int houseId);

        /// <summary>
        /// Gets all documents, optionally filtered by owner type.
        /// </summary>
        Task<IEnumerable<Document>> GetAllDocumentsAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null, string? type = null);

        /// <summary>
        /// Gets a document by ID.
        /// </summary>
        Task<Document?> GetDocumentByIdAsync(int documentId);

        /// <summary>
        /// Gets the active document of a specific type for an owner.
        /// </summary>
        Task<Document?> GetActiveDocumentAsync(int? tenantId, int? tenancyId, int? houseId, string type);

        /// <summary>
        /// Creates a new document.
        /// </summary>
        Task<int> CreateDocumentAsync(Document document);

        /// <summary>
        /// Updates a document.
        /// </summary>
        Task<bool> UpdateDocumentAsync(Document document);

        /// <summary>
        /// Deletes a document.
        /// </summary>
        Task<bool> DeleteDocumentAsync(int documentId);

        /// <summary>
        /// Gets documents that are expiring soon (within specified days).
        /// </summary>
        Task<IEnumerable<Document>> GetExpiringDocumentsAsync(int daysAhead = 30);

        /// <summary>
        /// Gets all versions of a document (by owner and type).
        /// </summary>
        Task<IEnumerable<Document>> GetDocumentHistoryAsync(int? tenantId, int? tenancyId, int? houseId, string type);
    }
}
