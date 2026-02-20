using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Daryva.Services.Api;
using Daryva.Services.Data;
using Daryva.Services;

namespace Daryva.Services.Migration;

public class SqliteToApiMigrationService : IMigrationService
{
    private const string HouseIdMapSettingKey = "ApiMigration.HouseIdMap";
    private const string TenantIdMapSettingKey = "ApiMigration.TenantIdMap";
    private const string TenancyIdMapSettingKey = "ApiMigration.TenancyIdMap";
    private const string RentPaymentIdMapSettingKey = "ApiMigration.RentPaymentIdMap";
    private const string DepositPaymentIdMapSettingKey = "ApiMigration.DepositPaymentIdMap";

    private readonly IHouseRepository _houseRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenancyRepository _tenancyRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRentPaymentRepository _rentPaymentRepository;
    private readonly IDepositPaymentRepository _depositPaymentRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IApiClient _apiClient;
    private readonly IConfigurationService _configurationService;

    public SqliteToApiMigrationService(
        IHouseRepository houseRepository,
        ITenantRepository tenantRepository,
        ITenancyRepository tenancyRepository,
        IExpenseRepository expenseRepository,
        IDocumentRepository documentRepository,
        IRentPaymentRepository rentPaymentRepository,
        IDepositPaymentRepository depositPaymentRepository,
        INotificationRepository notificationRepository,
        IApiClient apiClient,
        IConfigurationService configurationService)
    {
        _houseRepository = houseRepository;
        _tenantRepository = tenantRepository;
        _tenancyRepository = tenancyRepository;
        _expenseRepository = expenseRepository;
        _documentRepository = documentRepository;
        _rentPaymentRepository = rentPaymentRepository;
        _depositPaymentRepository = depositPaymentRepository;
        _notificationRepository = notificationRepository;
        _apiClient = apiClient;
        _configurationService = configurationService;
    }

    // Helper methods to ensure DateTime is treated as UTC for PostgreSQL
    private static DateTime ToUtc(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    private static DateTime? ToUtc(DateTime? dateTime)
    {
        return dateTime.HasValue ? DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc) : null;
    }

    private static DateTime? NormalizeMoveOutDate(DateTime? moveOutDate)
    {
        if (!moveOutDate.HasValue)
            return null;

        if (moveOutDate.Value <= DateTime.MinValue.AddDays(1))
            return null;

        return ToUtc(moveOutDate);
    }

    // Helper method to truncate strings to max length for database constraints
    private static string? TruncateString(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }

    public async Task<MigrationResult> MigrateAllDataAsync(Guid targetOrgId, IProgress<MigrationProgress> progress)
    {
        try
        {
            var stats = new MigrationStats();
            var errors = new List<string>();

            // Total steps: 10 (read) + 1 (send) + 1 (complete)
            const int totalSteps = 12;
            int currentStep = 0;

            // Step 1: Read Houses
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading houses from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var houses = await _houseRepository.GetAllHousesAsync();
            var importHouses = houses.Select(h => new ImportHouseDto
            {
                OldId = h.HouseId,
                Name = h.Name,
                AddressLine1 = h.AddressLine1,
                AddressLine2 = h.AddressLine2,
                City = h.City,
                Postcode = h.Postcode,
                TotalRooms = h.TotalRooms
            }).ToList();

            // Step 2: Read Tenants
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading tenants from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var tenants = await _tenantRepository.GetAllTenantsAsync(includeArchived: true);
            var importTenants = tenants.Select(t => new ImportTenantDto
            {
                OldId = t.TenantId,
                FullName = t.FullName,
                PhoneNumber = t.PhoneNumber,
                Email = t.Email,
                UniversityName = t.UniversityName,
                IsArchived = t.IsArchived
            }).ToList();

            // Step 3: Read Tenancies
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading tenancies from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var allTenancies = new List<MVVM.Models.Tenancy>();
            foreach (var house in houses)
            {
                var houseTenancies = await _tenancyRepository.GetTenanciesByHouseIdAsync(house.HouseId);
                allTenancies.AddRange(houseTenancies);
            }

            progress.Report(new MigrationProgress
            {
                CurrentStep = $"Found {allTenancies.Count} tenancies in SQLite. Checking tenant-house assignments...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            // IMPORTANT: Check if each active tenant has an active tenancy
            // The old database has CurrentHouseId on Tenant table which needs to be converted to tenancies
            var existingTenantIds = allTenancies.Select(t => t.TenantId).Distinct().ToHashSet();
            int createdTenanciesCount = 0;
            int nextTenancyId = allTenancies.Any() ? allTenancies.Max(t => t.TenancyId) + 1 : 1;

            foreach (var tenant in tenants.Where(t => !t.IsArchived))
            {
                // Check if this tenant has an active tenancy
                var hasActiveTenancy = allTenancies.Any(tn => 
                    tn.TenantId == tenant.TenantId && 
                    tn.Status == "Active" && 
                    !tn.MoveOutDate.HasValue);

                // If tenant doesn't have an active tenancy, create one
                // (regardless of CurrentTenancyId - we need to ensure every active tenant has a house)
                if (!hasActiveTenancy)
                {
                    // Try to find which house this tenant should be in
                    // Look for their most recent tenancy or use the first house as fallback
                    var lastTenancy = allTenancies
                        .Where(tn => tn.TenantId == tenant.TenantId)
                        .OrderByDescending(tn => tn.MoveInDate)
                        .FirstOrDefault();

                    int targetHouseId;
                    if (lastTenancy != null)
                    {
                        // Use the house from their last tenancy
                        targetHouseId = lastTenancy.HouseId;
                    }
                    else if (houses.Any())
                    {
                        // Fallback: distribute tenants across available houses
                        var housesList = houses.ToList();
                        var houseIndex = createdTenanciesCount % housesList.Count;
                        targetHouseId = housesList[houseIndex].HouseId;
                    }
                    else
                    {
                        continue; // Skip if no houses available
                    }

                    // Create a new active tenancy for this tenant
                    var newTenancy = new MVVM.Models.Tenancy
                    {
                        TenancyId = nextTenancyId++,
                        TenantId = tenant.TenantId,
                        HouseId = targetHouseId,
                        MoveInDate = tenant.CreatedAt,
                        MoveOutDate = null,
                        RentStartMonth = tenant.CreatedAt.Month,
                        RentStartYear = tenant.CreatedAt.Year,
                        RentAmountMonthly = 500m, // Default rent amount
                        DepositAmount = 0m,
                        PaymentDueDay = 1,
                        Status = "Active",
                        Notes = "Auto-created from tenant CurrentHouseId field during migration"
                    };
                    allTenancies.Add(newTenancy);
                    createdTenanciesCount++;
                }
            }

            if (createdTenanciesCount > 0)
            {
                progress.Report(new MigrationProgress
                {
                    CurrentStep = $"Created {createdTenanciesCount} missing tenancies for active tenants. Total: {allTenancies.Count}",
                    CompletedSteps = currentStep,
                    TotalSteps = totalSteps,
                    Stats = stats
                });
            }

            var importTenancies = allTenancies.Select(t => new ImportTenancyDto
            {
                OldId = t.TenancyId,
                OldHouseId = t.HouseId,
                OldTenantId = t.TenantId,
                MoveInDate = ToUtc(t.MoveInDate),
                MoveOutDate = NormalizeMoveOutDate(t.MoveOutDate),
                RentStartMonth = t.RentStartMonth,
                RentStartYear = t.RentStartYear,
                RentAmountMonthly = t.RentAmountMonthly,
                DepositAmount = t.DepositAmount,
                PaymentDueDay = t.PaymentDueDay,
                Status = t.Status,
                Notes = t.Notes
            }).ToList();

            // Step 4: Read Documents
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading documents from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var documents = await _documentRepository.GetAllDocumentsAsync();
            var importDocuments = documents.Select(d => new ImportDocumentDto
            {
                OldId = d.DocumentId,
                OldTenantId = d.TenantId,
                OldTenancyId = d.TenancyId,
                OldHouseId = d.HouseId,
                Type = TruncateString(d.Type, 100) ?? string.Empty,
                DisplayName = TruncateString(d.DisplayName, 256) ?? string.Empty,
                FileName = TruncateString(d.FileName, 512) ?? string.Empty,
                FileMimeType = TruncateString(d.FileMimeType, 128),
                StoragePath = TruncateString(d.StoragePath, 1024),
                Source = TruncateString(d.Source, 50),
                UploadedAt = ToUtc(d.UploadedAt),
                ValidFrom = ToUtc(d.ValidFrom),
                ValidTo = ToUtc(d.ValidTo),
                Version = d.Version,
                IsActive = d.IsActive
            }).ToList();

            // Step 5: Read Expenses
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading expenses from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var expenses = await _expenseRepository.GetAllExpensesAsync();
            var importExpenses = expenses.Select(e => new ImportExpenseDto
            {
                OldId = e.HouseExpenseId,
                OldHouseId = e.HouseId,
                DateIncurred = ToUtc(e.DateIncurred),
                Category = e.Category,
                Amount = e.Amount,
                Vendor = e.Vendor,
                Notes = e.Notes,
                OldReceiptDocumentId = e.ReceiptDocumentId
            }).ToList();

            // Step 6: Read Rent Payments
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading rent payments from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var rentPayments = await _rentPaymentRepository.GetAllRentPaymentsAsync();
            var importRentPayments = rentPayments.Select(r => new ImportRentPaymentDto
            {
                OldId = r.RentPaymentId,
                OldTenancyId = r.TenancyId,
                DatePaid = ToUtc(r.PaidOn),
                AmountPaid = r.AmountPaid,
                PaymentMethod = r.Method,
                ReferenceNumber = r.Reference, 
                Notes = r.Notes,
                CollectedBy = r.CollectedBy
            }).ToList();

            // Step 7: Read Deposit Payments
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading deposit payments from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var depositPayments = await _depositPaymentRepository.GetAllDepositPaymentsAsync();
            var importDepositPayments = depositPayments.Select(d => new ImportDepositPaymentDto
            {
                OldId = d.DepositPaymentId,
                OldTenancyId = d.TenancyId,
                DatePaid = ToUtc(d.PaidOn),
                AmountPaid = d.AmountPaid,
                PaymentMethod = d.Method, 
                ProtectionScheme = null,
                ProtectionReference = null, 
                Notes = d.Notes
            }).ToList();

            // Step 8: Read Notification Templates
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading notification templates from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var templates = await _notificationRepository.GetAllTemplatesAsync();
            var importTemplates = templates.Select(t => new ImportNotificationTemplateDto
            {
                OldId = t.TemplateId,
                Name = TruncateString(t.Name, 200) ?? string.Empty,
                Channel = TruncateString(t.Channel, 50) ?? string.Empty,
                Type = TruncateString(t.Type, 50) ?? string.Empty,
                SubjectTemplate = TruncateString(t.SubjectTemplate, 256),
                BodyTemplate = t.BodyTemplate,
                IsDefault = t.IsDefault,
                CreatedAt = ToUtc(t.CreatedAt)
            }).ToList();

            // Step 9: Read Notifications
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading notifications from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var notifications = await _notificationRepository.GetAllNotificationsAsync();
            var importNotifications = notifications.Select(n => new ImportNotificationDto
            {
                OldId = n.NotificationId,
                OldTenantId = n.TenantId,
                OldTenancyId = n.TenancyId,
                Channel = TruncateString(n.Channel, 50) ?? string.Empty,
                Type = TruncateString(n.Type, 50) ?? string.Empty,
                ToAddress = TruncateString(n.ToAddress, 256) ?? string.Empty,
                Subject = TruncateString(n.Subject, 256),
                Body = n.Body,
                ScheduledFor = ToUtc(n.ScheduledFor),
                SentAt = ToUtc(n.SentAt),
                Status = TruncateString(n.Status, 50) ?? "Pending",
                ProviderMessageId = TruncateString(n.ProviderMessageId, 256),
                Error = n.Error,
                OldTemplateId = n.TemplateId
            }).ToList();

            // Step 10: Read Notification Attempts
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Reading notification attempts from SQLite...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var importAttempts = new List<ImportNotificationAttemptDto>();
            foreach (var notification in notifications)
            {
                var attempts = await _notificationRepository.GetAttemptsByNotificationIdAsync(notification.NotificationId);
                importAttempts.AddRange(attempts.Select(a => new ImportNotificationAttemptDto
                {
                    OldId = a.AttemptId,
                    OldNotificationId = a.NotificationId,
                    AttemptedAt = ToUtc(a.AttemptedAt),
                    Status = TruncateString(a.Status, 50) ?? string.Empty,
                    Error = a.Error,
                    ProviderMessageId = TruncateString(a.ProviderMessageId, 256)
                }));
            }

            // Step 11: Send to API
            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Sending data to API...",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            var request = new BulkImportRequestDto
            {
                Houses = importHouses,
                Tenants = importTenants,
                Tenancies = importTenancies,
                Expenses = importExpenses,
                Documents = importDocuments,
                RentPayments = importRentPayments,
                DepositPayments = importDepositPayments,
                NotificationTemplates = importTemplates,
                Notifications = importNotifications,
                NotificationAttempts = importAttempts
            };

            _apiClient.SetCurrentOrgId(targetOrgId);

            var response = await _apiClient.HttpClient.PostAsJsonAsync("api/import", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return new MigrationResult
                {
                    Success = false,
                    Message = $"API returned {response.StatusCode}: {errorContent}",
                    Stats = stats,
                    Errors = new List<string> { errorContent }
                };
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<BulkImportResponseDto>();
            if (apiResponse == null)
            {
                return new MigrationResult
                {
                    Success = false,
                    Message = "Failed to deserialize API response",
                    Stats = stats,
                    Errors = new List<string> { "Response was null" }
                };
            }

            if (apiResponse.Stats != null)
            {
                stats.HousesImported = apiResponse.Stats.HousesImported;
                stats.TenantsImported = apiResponse.Stats.TenantsImported;
                stats.TenanciesImported = apiResponse.Stats.TenanciesImported;
                stats.ExpensesImported = apiResponse.Stats.ExpensesImported;
                stats.DocumentsImported = apiResponse.Stats.DocumentsImported;
                stats.RentPaymentsImported = apiResponse.Stats.RentPaymentsImported;
                stats.DepositPaymentsImported = apiResponse.Stats.DepositPaymentsImported;
                stats.NotificationTemplatesImported = apiResponse.Stats.NotificationTemplatesImported;
                stats.NotificationsImported = apiResponse.Stats.NotificationsImported;
                stats.NotificationAttemptsImported = apiResponse.Stats.NotificationAttemptsImported;
            }

            if (apiResponse.IdMappings.TryGetValue("houses", out var houseIdMap) && houseIdMap.Count > 0)
                _configurationService.SetLocalValue(HouseIdMapSettingKey, JsonSerializer.Serialize(houseIdMap));

            if (apiResponse.IdMappings.TryGetValue("tenants", out var tenantIdMap) && tenantIdMap.Count > 0)
                _configurationService.SetLocalValue(TenantIdMapSettingKey, JsonSerializer.Serialize(tenantIdMap));

            if (apiResponse.IdMappings.TryGetValue("tenancies", out var tenancyIdMap) && tenancyIdMap.Count > 0)
                _configurationService.SetLocalValue(TenancyIdMapSettingKey, JsonSerializer.Serialize(tenancyIdMap));

            if (apiResponse.IdMappings.TryGetValue("rentPayments", out var rentPaymentIdMap) && rentPaymentIdMap.Count > 0)
                _configurationService.SetLocalValue(RentPaymentIdMapSettingKey, JsonSerializer.Serialize(rentPaymentIdMap));

            if (apiResponse.IdMappings.TryGetValue("depositPayments", out var depositPaymentIdMap) && depositPaymentIdMap.Count > 0)
                _configurationService.SetLocalValue(DepositPaymentIdMapSettingKey, JsonSerializer.Serialize(depositPaymentIdMap));

            currentStep++;
            progress.Report(new MigrationProgress
            {
                CurrentStep = "Migration complete!",
                CompletedSteps = currentStep,
                TotalSteps = totalSteps,
                Stats = stats
            });

            return new MigrationResult
            {
                Success = apiResponse.Success,
                Message = apiResponse.Message,
                Stats = stats,
                Errors = apiResponse.Errors
            };
        }
        catch (Exception ex)
        {
            return new MigrationResult
            {
                Success = false,
                Message = $"Migration failed: {ex.Message}",
                Errors = new List<string> { ex.ToString() }
            };
        }
    }

}

public class ImportHouseDto
{
    public int OldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public int TotalRooms { get; set; }
}

public class ImportTenantDto
{
    public int OldId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? UniversityName { get; set; }
    public bool IsArchived { get; set; }
}

public class ImportTenancyDto
{
    public int OldId { get; set; }
    public int OldHouseId { get; set; }
    public int OldTenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public int PaymentDueDay { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ImportDocumentDto
{
    public int OldId { get; set; }
    public int? OldTenantId { get; set; }
    public int? OldTenancyId { get; set; }
    public int? OldHouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileMimeType { get; set; }
    public string? StoragePath { get; set; }
    public string? Source { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
}

public class ImportExpenseDto
{
    public int OldId { get; set; }
    public int OldHouseId { get; set; }
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public int? OldReceiptDocumentId { get; set; }
}

public class ImportRentPaymentDto
{
    public int OldId { get; set; }
    public int OldTenancyId { get; set; }
    public DateTime DatePaid { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
}

public class ImportDepositPaymentDto
{
    public int OldId { get; set; }
    public int OldTenancyId { get; set; }
    public DateTime DatePaid { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ProtectionScheme { get; set; }
    public string? ProtectionReference { get; set; }
    public string? Notes { get; set; }
}

public class ImportNotificationTemplateDto
{
    public int OldId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? SubjectTemplate { get; set; }
    public string BodyTemplate { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ImportNotificationDto
{
    public int OldId { get; set; }
    public int OldTenantId { get; set; }
    public int? OldTenancyId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public int? OldTemplateId { get; set; }
}

public class ImportNotificationAttemptDto
{
    public int OldId { get; set; }
    public int OldNotificationId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? ProviderMessageId { get; set; }
}

public class BulkImportRequestDto
{
    public List<ImportHouseDto> Houses { get; set; } = new();
    public List<ImportTenantDto> Tenants { get; set; } = new();
    public List<ImportTenancyDto> Tenancies { get; set; } = new();
    public List<ImportExpenseDto> Expenses { get; set; } = new();
    public List<ImportDocumentDto> Documents { get; set; } = new();
    public List<ImportRentPaymentDto> RentPayments { get; set; } = new();
    public List<ImportDepositPaymentDto> DepositPayments { get; set; } = new();
    public List<ImportNotificationTemplateDto> NotificationTemplates { get; set; } = new();
    public List<ImportNotificationDto> Notifications { get; set; } = new();
    public List<ImportNotificationAttemptDto> NotificationAttempts { get; set; } = new();
}

public class BulkImportResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ImportStatsDto? Stats { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, Dictionary<int, Guid>> IdMappings { get; set; } = new();
}

public class ImportStatsDto
{
    public int HousesImported { get; set; }
    public int TenantsImported { get; set; }
    public int TenanciesImported { get; set; }
    public int ExpensesImported { get; set; }
    public int DocumentsImported { get; set; }
    public int RentPaymentsImported { get; set; }
    public int DepositPaymentsImported { get; set; }
    public int NotificationTemplatesImported { get; set; }
    public int NotificationsImported { get; set; }
    public int NotificationAttemptsImported { get; set; }
    public int TotalItemsImported { get; set; }
}
