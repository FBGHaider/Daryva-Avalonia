using System.IO;
using Daryva.MVVM.Models;
using Daryva.Services.Data;
using Daryva.Services;

namespace Daryva.Services.Business
{
    /// <summary>
    /// Service implementation for Document operations with file storage handling.
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly string _defaultStorageRootPath;

        // Document types organized by owner category
        private static readonly Dictionary<string, string[]> DocumentTypesByOwner = new()
        {
            ["Tenant"] = new[]
            {
                "StudentConfirmationLetter",
                "PhotoId",
                "RightToRent",
                "Other"
            },
            ["Tenancy"] = new[]
            {
                "TenancyAgreementSigned",
                "GuarantorAgreement",
                "InventoryCheckIn",
                "DepositProtectionCertificate",
                "NoticeToLeave",
                "Other"
            },
            ["House"] = new[]
            {
                "Other"
            }
        };

        // Display names for document types
        private static readonly Dictionary<string, string> DocumentTypeDisplayNames = new()
        {
            ["StudentConfirmationLetter"] = "Student Confirmation Letter",
            ["PhotoId"] = "Photo ID",
            ["RightToRent"] = "Right to Rent",
            ["TenancyAgreementSigned"] = "Tenancy Agreement (Signed)",
            ["GuarantorAgreement"] = "Guarantor Agreement",
            ["InventoryCheckIn"] = "Inventory & Check-in",
            ["DepositProtectionCertificate"] = "Deposit Protection Certificate",
            ["NoticeToLeave"] = "Notice to Leave",
            ["Other"] = "Other"
        };

        public DocumentService(IDocumentRepository documentRepository, IConfigurationService? configurationService = null)
        {
            _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
            
            // Get storage path from configuration or use default
            var configService = configurationService ?? new ConfigurationService();
            _defaultStorageRootPath = configService.GetValue("DocumentStoragePath") 
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LandLordDocs");
            
            // Ensure storage directory exists
            if (!Directory.Exists(_defaultStorageRootPath))
            {
                Directory.CreateDirectory(_defaultStorageRootPath);
            }
        }

        public async Task<IEnumerable<Document>> GetDocumentsAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null, string? type = null)
        {
            return await _documentRepository.GetAllDocumentsAsync(tenantId, tenancyId, houseId, type);
        }

        public async Task<Document?> GetDocumentByIdAsync(int documentId)
        {
            return await _documentRepository.GetDocumentByIdAsync(documentId);
        }

        public async Task<IEnumerable<Document>> GetTenantDocumentsAsync(int tenantId)
        {
            return await _documentRepository.GetDocumentsByTenantIdAsync(tenantId);
        }

        public async Task<IEnumerable<Document>> GetTenancyDocumentsAsync(int tenancyId)
        {
            return await _documentRepository.GetDocumentsByTenancyIdAsync(tenancyId);
        }

        public async Task<IEnumerable<Document>> GetHouseDocumentsAsync(int houseId)
        {
            return await _documentRepository.GetDocumentsByHouseIdAsync(houseId);
        }

        public async Task<IEnumerable<Document>> GetExpiringDocumentsAsync(int daysAhead = 30)
        {
            return await _documentRepository.GetExpiringDocumentsAsync(daysAhead);
        }

        public async Task<IEnumerable<DocumentStatusItem>> GetDocumentStatusChecklistAsync(int? tenantId = null, int? tenancyId = null, int? houseId = null)
        {
            // Determine owner type
            string ownerType = tenantId.HasValue ? "Tenant" : tenancyId.HasValue ? "Tenancy" : "House";
            
            // Define important documents that should always appear in the checklist
            var importantTypes = ownerType == "Tenant" 
                ? new[] { "PhotoId", "StudentConfirmationLetter", "RightToRent" }
                : ownerType == "Tenancy"
                ? new[] { "TenancyAgreementSigned" }
                : Array.Empty<string>();

            var checklist = new List<DocumentStatusItem>();
            var addedTypes = new HashSet<string>(); // Track types we've already added

            // First, add important document types (always shown, even if missing)
            foreach (var docType in importantTypes)
            {
                var existingDoc = await _documentRepository.GetActiveDocumentAsync(tenantId, tenancyId, houseId, docType);
                
                var statusItem = new DocumentStatusItem
                {
                    Type = docType,
                    DisplayName = DocumentTypeDisplayNames.GetValueOrDefault(docType, docType),
                    DocumentId = existingDoc?.DocumentId,
                    Version = existingDoc?.Version ?? 0,
                    UploadedDate = existingDoc?.UploadedAt,
                    ValidTo = existingDoc?.ValidTo
                };

                // Calculate status
                if (existingDoc == null)
                {
                    statusItem.Status = "Missing";
                }
                else if (existingDoc.ValidTo.HasValue)
                {
                    var daysUntilExpiry = (existingDoc.ValidTo.Value - DateTime.Now).Days;
                    if (daysUntilExpiry < 0)
                    {
                        statusItem.Status = "Expired";
                    }
                    else if (daysUntilExpiry <= 30)
                    {
                        statusItem.Status = "ExpiringSoon";
                    }
                    else
                    {
                        statusItem.Status = "Active";
                    }
                }
                else
                {
                    statusItem.Status = "Active";
                }

                checklist.Add(statusItem);
                addedTypes.Add(docType);
            }

            // Then, get all uploaded documents (active only) and add any non-important types that have been uploaded
            var allUploadedDocs = await _documentRepository.GetAllDocumentsAsync(tenantId, tenancyId, houseId, null);
            var activeUploadedDocs = allUploadedDocs.Where(d => d.IsActive);
            
            // Group by type to get unique types
            var uploadedTypes = activeUploadedDocs
                .Where(d => !addedTypes.Contains(d.Type)) // Exclude important types already added
                .GroupBy(d => d.Type)
                .Select(g => g.First()); // Get one document per type
            
            foreach (var uploadedDoc in uploadedTypes)
            {
                var statusItem = new DocumentStatusItem
                {
                    Type = uploadedDoc.Type,
                    DisplayName = string.IsNullOrWhiteSpace(uploadedDoc.DisplayName) 
                        ? DocumentTypeDisplayNames.GetValueOrDefault(uploadedDoc.Type, uploadedDoc.Type)
                        : uploadedDoc.DisplayName,
                    DocumentId = uploadedDoc.DocumentId,
                    Version = uploadedDoc.Version,
                    UploadedDate = uploadedDoc.UploadedAt,
                    ValidTo = uploadedDoc.ValidTo
                };

                // Calculate status for uploaded documents
                if (uploadedDoc.ValidTo.HasValue)
                {
                    var daysUntilExpiry = (uploadedDoc.ValidTo.Value - DateTime.Now).Days;
                    if (daysUntilExpiry < 0)
                    {
                        statusItem.Status = "Expired";
                    }
                    else if (daysUntilExpiry <= 30)
                    {
                        statusItem.Status = "ExpiringSoon";
                    }
                    else
                    {
                        statusItem.Status = "Active";
                    }
                }
                else
                {
                    statusItem.Status = "Active";
                }

                checklist.Add(statusItem);
                addedTypes.Add(uploadedDoc.Type);
            }

            return checklist;
        }

        public async Task<Document> UploadDocumentAsync(Document document, byte[] fileBytes, string? storageRootPath = null)
        {
            if (fileBytes == null || fileBytes.Length == 0)
            {
                throw new ArgumentException("File bytes cannot be null or empty", nameof(fileBytes));
            }

            // Validate that exactly one owner is set
            var ownerCount = (document.TenantId.HasValue ? 1 : 0) +
                            (document.TenancyId.HasValue ? 1 : 0) +
                            (document.HouseId.HasValue ? 1 : 0);

            if (ownerCount != 1)
            {
                throw new ArgumentException("Document must have exactly one owner (TenantId, TenancyId, or HouseId)");
            }

            storageRootPath ??= _defaultStorageRootPath;

            // Check if there's an existing active document of the same type
            var existingDoc = await _documentRepository.GetActiveDocumentAsync(
                document.TenantId, document.TenancyId, document.HouseId, document.Type);

            // If replacing an existing document, deactivate old versions and increment version
            int newVersion = 1;
            if (existingDoc != null)
            {
                // Deactivate all existing documents of this type
                var allVersions = await _documentRepository.GetDocumentHistoryAsync(
                    document.TenantId, document.TenancyId, document.HouseId, document.Type);
                
                foreach (var oldDoc in allVersions.Where(d => d.IsActive))
                {
                    oldDoc.IsActive = false;
                    await _documentRepository.UpdateDocumentAsync(oldDoc);
                }

                newVersion = allVersions.Any() ? allVersions.Max(d => d.Version) + 1 : 1;
            }

            // Generate display name if not provided
            if (string.IsNullOrWhiteSpace(document.DisplayName))
            {
                var displayNameBase = DocumentTypeDisplayNames.GetValueOrDefault(document.Type, document.Type);
                var yearLabel = DateTime.Now.Year;
                if (document.ValidTo.HasValue)
                {
                    var nextYear = document.ValidTo.Value.Year;
                    if (nextYear > yearLabel)
                    {
                        displayNameBase += $" {yearLabel}/{nextYear % 100}";
                    }
                    else
                    {
                        displayNameBase += $" {yearLabel}";
                    }
                }
                else
                {
                    displayNameBase += $" {yearLabel}";
                }
                document.DisplayName = displayNameBase;
            }

            // Set source if not provided
            document.Source ??= "Uploaded";

            // Generate storage path
            string ownerFolder = document.TenantId.HasValue ? $"Tenants/{document.TenantId}" :
                                document.TenancyId.HasValue ? $"Tenancies/{document.TenancyId}" :
                                $"Houses/{document.HouseId}";
            
            string typeFolder = document.Type;
            string storageFolder = Path.Combine(storageRootPath, ownerFolder, typeFolder);
            
            // Ensure directory exists
            if (!Directory.Exists(storageFolder))
            {
                Directory.CreateDirectory(storageFolder);
            }

            // Generate filename: v{Version}_{yyyyMMdd}_{originalFileName}
            var safeFileName = Path.GetFileName(document.FileName) ?? "document";
            var fileExtension = Path.GetExtension(safeFileName);
            var fileNameBase = Path.GetFileNameWithoutExtension(safeFileName);
            var versionPrefix = $"v{newVersion}";
            var datePrefix = DateTime.Now.ToString("yyyyMMdd");
            var storageFileName = $"{versionPrefix}_{datePrefix}_{fileNameBase}{fileExtension}";
            
            string fullStoragePath = Path.Combine(storageFolder, storageFileName);
            
            // Save file to disk
            await File.WriteAllBytesAsync(fullStoragePath, fileBytes);

            // Set document properties
            document.StoragePath = fullStoragePath;
            document.Version = newVersion;
            document.IsActive = true;
            document.UploadedAt = DateTime.Now;

            // Determine MIME type if not provided
            if (string.IsNullOrWhiteSpace(document.FileMimeType))
            {
                document.FileMimeType = GetMimeTypeFromExtension(fileExtension);
            }

            // Create document in database
            var documentId = await _documentRepository.CreateDocumentAsync(document);
            document.DocumentId = documentId;

            return document;
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            await _documentRepository.UpdateDocumentAsync(document);
        }

        public async Task<bool> DeleteDocumentAsync(int documentId, string? storageRootPath = null)
        {
            var document = await _documentRepository.GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                return false;
            }

            storageRootPath ??= _defaultStorageRootPath;

            // Delete file from disk if storage path exists
            if (!string.IsNullOrWhiteSpace(document.StoragePath))
            {
                var fullPath = Path.IsPathRooted(document.StoragePath) 
                    ? document.StoragePath 
                    : Path.Combine(storageRootPath, document.StoragePath);

                if (File.Exists(fullPath))
                {
                    try
                    {
                        File.Delete(fullPath);
                    }
                    catch
                    {
                        // Log error but continue with database deletion
                    }
                }
            }

            // Delete from database
            return await _documentRepository.DeleteDocumentAsync(documentId);
        }

        public async Task<byte[]?> GetDocumentFileBytesAsync(int documentId, string? storageRootPath = null)
        {
            var document = await _documentRepository.GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                return null;
            }

            storageRootPath ??= _defaultStorageRootPath;

            // Try to read from storage path first
            if (!string.IsNullOrWhiteSpace(document.StoragePath))
            {
                var fullPath = Path.IsPathRooted(document.StoragePath) 
                    ? document.StoragePath 
                    : Path.Combine(storageRootPath, document.StoragePath);

                if (File.Exists(fullPath))
                {
                    try
                    {
                        return await File.ReadAllBytesAsync(fullPath);
                    }
                    catch
                    {
                        // File read failed, return null
                        return null;
                    }
                }
            }

            return null;
        }

        public async Task<IEnumerable<Document>> GetDocumentHistoryAsync(int? tenantId, int? tenancyId, int? houseId, string type)
        {
            return await _documentRepository.GetDocumentHistoryAsync(tenantId, tenancyId, houseId, type);
        }

        /// <summary>
        /// Gets MIME type from file extension.
        /// </summary>
        private static string GetMimeTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}
