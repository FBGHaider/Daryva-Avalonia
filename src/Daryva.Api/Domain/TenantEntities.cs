using Daryva.Api.Domain.Interfaces;

namespace Daryva.Api.Domain;

/// <summary>
/// Tenant in an organization (person who rents).
/// </summary>
public class Tenant : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? UniversityName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsArchived { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public ICollection<Tenancy> Tenancies { get; set; } = new List<Tenancy>();
}

/// <summary>
/// Tenancy (assignment of tenant to house with rent details).
/// </summary>
public class Tenancy : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid HouseId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime MoveInDate { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public int? RentStartMonth { get; set; }
    public int? RentStartYear { get; set; }
    public decimal RentAmountMonthly { get; set; }
    public decimal DepositAmount { get; set; }
    public byte PaymentDueDay { get; set; }
    public string Status { get; set; } = "Active"; // Active, Ended
    public string? Notes { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public House House { get; set; } = null!;
    public Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// House expense.
/// </summary>
public class Expense : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid HouseId { get; set; }
    public DateTime DateIncurred { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Vendor { get; set; }
    public string? Notes { get; set; }
    public Guid? ReceiptDocumentId { get; set; }

    /// <summary>Soft-deleted: excluded from lists/totals by default but kept for financial history.</summary>
    public bool IsArchived { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public House House { get; set; } = null!;
    public Document? ReceiptDocument { get; set; }
}

/// <summary>
/// Document (file attachment for tenant, tenancy, or house).
/// </summary>
public class Document : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? TenancyId { get; set; }
    public Guid? HouseId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FileMimeType { get; set; }
    public string? StoragePath { get; set; }
    public string? Source { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Navigation
    public Organization Organization { get; set; } = null!;
    public Tenant? Tenant { get; set; }
    public Tenancy? Tenancy { get; set; }
    public House? House { get; set; }
}

/// <summary>
/// Rent payment.
/// </summary>
public class RentPayment : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TenancyId { get; set; }
    public DateTime DatePaid { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }

    /// <summary>Voided: excluded from ledgers/totals/transactions by default but kept for financial history.</summary>
    public bool IsVoided { get; set; }

    /// <summary>
    /// True when this rent was settled by drawing down the tenant's deposit rather than new money
    /// (RecordPaymentRequest.UseDepositForRent) -- no cash changed hands. Every deposit-remaining
    /// calculation (GetTotalDepositPaid, deposit-return reminders) must subtract the sum of
    /// non-voided payments with this flag set from the raw deposit-paid total, or the same deposit
    /// can be "used for rent" every month indefinitely and the full original amount still gets
    /// flagged for return at move-out even though it was already spent.
    /// </summary>
    public bool PaidFromDeposit { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public Tenancy Tenancy { get; set; } = null!;
}

/// <summary>
/// Deposit payment.
/// </summary>
public class DepositPayment : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TenancyId { get; set; }
    public DateTime DatePaid { get; set; }
    public decimal AmountPaid { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? ProtectionScheme { get; set; }
    public string? ProtectionReference { get; set; }
    public string? Notes { get; set; }

    /// <summary>Voided: excluded from ledgers/totals/transactions by default but kept for financial history.</summary>
    public bool IsVoided { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public Tenancy Tenancy { get; set; } = null!;
}

/// <summary>
/// Record that a deposit was returned to the tenant (so they drop off deposit-return reminders).
/// </summary>
public class DepositReturn : IOrgScopedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid TenancyId { get; set; }
    public DateTime ReturnedDate { get; set; }
    public decimal AmountReturned { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Organization Organization { get; set; } = null!;
    public Tenancy Tenancy { get; set; } = null!;
}
