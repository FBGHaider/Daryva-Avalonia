using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Daryva.Services.Api;
using Daryva.Services.Data;

namespace Daryva.Services.Migration;

public class SqliteToApiMigrationService : IMigrationService
{
    private readonly IHouseRepository _houseRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenancyRepository _tenancyRepository;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRentPaymentRepository _rentPaymentRepository;
    private readonly IDepositPaymentRepository _depositPaymentRepository;
    private readonly IApiClient _apiClient;

    public SqliteToApiMigrationService(
        IHouseRepository houseRepository,
        ITenantRepository tenantRepository,
        ITenancyRepository tenancyRepository,
        IExpenseRepository expenseRepository,
        IDocumentRepository documentRepository,
        IRentPaymentRepository rentPaymentRepository,
        IDepositPaymentRepository depositPaymentRepository,
        IApiClient apiClient)
    {
        _houseRepository = houseRepository;
        _tenantRepository = tenantRepository;
        _tenancyRepository = tenancyRepository;
        _expenseRepository = expenseRepository;
        _documentRepository = documentRepository;
        _rentPaymentRepository = rentPaymentRepository;
        _depositPaymentRepository = depositPaymentRepository;
        _apiClient = apiClient;
    }

    public async Task<MigrationResult> MigrateAllDataAsync(Guid targetOrgId, IProgress<MigrationProgress> progress)
    {
        try
        {
            var stats = new MigrationStats();
            var errors = new List<string>();

            // Total steps: 7 (read) + 1 (send) + 1 (complete)
            const int totalSteps = 9;
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

            var importTenancies = allTenancies.Select(t => new ImportTenancyDto
            {
                OldId = t.TenancyId,
                HouseOldId = t.HouseId,
                TenantOldId = t.TenantId,
                MoveInDate = t.MoveInDate,
                MoveOutDate = t.MoveOutDate,
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
                TenantOldId = d.TenantId,
                TenancyOldId = d.TenancyId,
                HouseOldId = d.HouseId,
                Type = d.Type,
                DisplayName = d.DisplayName,
                FileName = d.FileName,
                FileMimeType = d.FileMimeType,
                StoragePath = d.StoragePath,
                Source = d.Source,
                UploadedAt = d.UploadedAt,
                ValidFrom = d.ValidFrom,
                ValidTo = d.ValidTo,
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
                HouseOldId = e.HouseId,
                DateIncurred = e.DateIncurred,
                Category = e.Category,
                Amount = e.Amount,
                Vendor = e.Vendor,
                Notes = e.Notes,
                ReceiptDocumentOldId = e.ReceiptDocumentId
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
                TenancyOldId = r.TenancyId,
                DatePaid = r.PaidOn,
                AmountPaid = r.AmountPaid,
                PaymentMethod = r.Method, // SQLite model uses "Method"
                ReferenceNumber = r.Reference, // SQLite model uses "Reference"
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
                TenancyOldId = d.TenancyId,
                DatePaid = d.PaidOn,
                AmountPaid = d.AmountPaid,
                PaymentMethod = d.Method, // SQLite model uses "Method"
                ProtectionScheme = null, // SQLite model doesn't have this field
                ProtectionReference = null, // SQLite model doesn't have this field
                Notes = d.Notes
            }).ToList();

            // Step 8: Send to API
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
                DepositPayments = importDepositPayments
            };

            // Ensure X-Org-Id is set
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

            // Update stats from API response
            if (apiResponse.Stats != null)
            {
                stats.HousesImported = apiResponse.Stats.HousesImported;
                stats.TenantsImported = apiResponse.Stats.TenantsImported;
                stats.TenanciesImported = apiResponse.Stats.TenanciesImported;
                stats.ExpensesImported = apiResponse.Stats.ExpensesImported;
                stats.DocumentsImported = apiResponse.Stats.DocumentsImported;
                stats.RentPaymentsImported = apiResponse.Stats.RentPaymentsImported;
                stats.DepositPaymentsImported = apiResponse.Stats.DepositPaymentsImported;
            }

            // Step 9: Complete
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

// DTOs matching backend API
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
    public int HouseOldId { get; set; }
    public int TenantOldId { get; set; }
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
    public int? TenantOldId { get; set; }
    public int? TenancyOldId { get; set; }
    public int? HouseOldId { get; set; }
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
    public int HouseOldId { get; set; }
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public int? ReceiptDocumentOldId { get; set; }
}

public class ImportRentPaymentDto
{
    public int OldId { get; set; }
    public int TenancyOldId { get; set; }
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
    public int TenancyOldId { get; set; }
    public DateTime DatePaid { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ProtectionScheme { get; set; }
    public string? ProtectionReference { get; set; }
    public string? Notes { get; set; }
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
}

public class BulkImportResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ImportStatsDto? Stats { get; set; }
    public List<string> Errors { get; set; } = new();
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
    public int TotalItemsImported { get; set; }
}
