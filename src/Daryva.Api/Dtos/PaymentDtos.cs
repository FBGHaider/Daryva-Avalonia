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
