namespace Daryva.Api.Dtos;

/// <summary>
/// Payment detail for ledger display.
/// </summary>
public class PaymentDetailApiResponse
{
    public Guid PaymentId { get; set; }
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
}

/// <summary>
/// Single rent ledger row (one tenant's rent for a period).
/// Used by Rent & Payments tab and as source of truth for house monthly rent totals.
/// </summary>
public class RentLedgerItemResponse
{
    public Guid TenancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "Unpaid";
    public decimal DepositRemaining { get; set; }
    public List<PaymentDetailApiResponse> PaymentsForThisMonth { get; set; } = new();
}

public class RecordPaymentRequest
{
    public Guid TenancyId { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal RentAmount { get; set; }
    public int RentYear { get; set; }
    public int RentMonth { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
    public bool UseDepositForRent { get; set; }
}

public class RecordPaymentResponse
{
    public bool Success { get; set; }
    public Guid? DepositPaymentId { get; set; }
    public Guid? RentPaymentId { get; set; }
}

public class DepositLedgerItemResponse
{
    public Guid TenancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public decimal DepositRequired { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = "Unpaid";
    public List<PaymentDetailApiResponse> Payments { get; set; } = new();
}

public class TransactionItemResponse
{
    public Guid PaymentId { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public DateTime PaidOn { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseAddress { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
    public Guid? TenancyId { get; set; }
}

public class DepositReturnReminderResponse
{
    public Guid TenancyId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseAddress { get; set; } = string.Empty;
    public DateTime LeaveDate { get; set; }
    public decimal AmountToReturn { get; set; }
}

public class RecordDepositReturnedRequest
{
    public Guid TenancyId { get; set; }
    public DateTime ReturnedDate { get; set; }
    public decimal AmountReturned { get; set; }
    public string? Notes { get; set; }
}
