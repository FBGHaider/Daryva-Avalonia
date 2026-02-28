namespace Daryva.Api.Dtos;

/// <summary>
/// Bulk import request containing all data from SQLite.
/// </summary>
public class BulkImportRequest
{
    public List<ImportHouse> Houses { get; set; } = new();
    public List<ImportTenant> Tenants { get; set; } = new();
    public List<ImportTenancy> Tenancies { get; set; } = new();
    public List<ImportExpense> Expenses { get; set; } = new();
    public List<ImportDocument> Documents { get; set; } = new();
    public List<ImportRentPayment> RentPayments { get; set; } = new();
    public List<ImportDepositPayment> DepositPayments { get; set; } = new();
    public List<ImportNotificationTemplate> NotificationTemplates { get; set; } = new();
    public List<ImportNotification> Notifications { get; set; } = new();
    public List<ImportNotificationAttempt> NotificationAttempts { get; set; } = new();
    public List<ImportDepositReturn> DepositReturns { get; set; } = new();
}

public class ImportHouse
{
    public int OldId { get; set; } // Original SQLite ID for mapping
    public string Name { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string Postcode { get; set; } = string.Empty;
    public int TotalRooms { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ImportTenant
{
    public int OldId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
}

public class ImportTenancy
{
    public int OldId { get; set; }
    public int OldHouseId { get; set; } // Maps to ImportHouse.OldId
    public int OldTenantId { get; set; } // Maps to ImportTenant.OldId
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
}

public class ImportExpense
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

public class ImportDocument
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
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class ImportRentPayment
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

public class ImportDepositPayment
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

public class ImportNotificationTemplate
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

public class ImportNotification
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

public class ImportNotificationAttempt
{
    public int OldId { get; set; }
    public int OldNotificationId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? ProviderMessageId { get; set; }
}

public class ImportDepositReturn
{
    public int OldId { get; set; }
    public int OldTenancyId { get; set; }
    public DateTime ReturnedDate { get; set; }
    public decimal AmountReturned { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Response from bulk import showing what was imported.
/// </summary>
public class BulkImportResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ImportStats Stats { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, Dictionary<int, Guid>> IdMappings { get; set; } = new();
}

public class ImportStats
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
    public int DepositReturnsImported { get; set; }
    public int TotalItemsImported { get; set; }
}
