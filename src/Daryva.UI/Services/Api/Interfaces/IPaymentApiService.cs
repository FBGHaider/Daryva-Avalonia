namespace Daryva.Services.Api;

public class RecordPaymentApiRequest
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

public class RecordPaymentApiResponse
{
    public bool Success { get; set; }
    public Guid? DepositPaymentId { get; set; }
    public Guid? RentPaymentId { get; set; }
}

public class TenancyLookupDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public DateTime? MoveOutDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PaymentDetailApiDto
{
    public Guid PaymentId { get; set; }
    public DateTime PaidOn { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? CollectedBy { get; set; }
}

public class RentLedgerItemApiDto
{
    public Guid TenancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal DepositRemaining { get; set; }
    public List<PaymentDetailApiDto> PaymentsForThisMonth { get; set; } = new();
}

public class DepositLedgerItemApiDto
{
    public Guid TenancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid HouseId { get; set; }
    public string HouseAddress { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public decimal DepositRequired { get; set; }
    public decimal AmountPaid { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<PaymentDetailApiDto> Payments { get; set; } = new();
}

public class TransactionItemApiDto
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

public interface IPaymentApiService
{
    Task<RecordPaymentApiResponse> RecordPaymentAsync(RecordPaymentApiRequest request, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalDepositPaidAsync(Guid tenancyId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalRentPaidForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default);
    Task<string> GetDepositStatusAsync(Guid tenancyId, decimal requiredAmount, CancellationToken cancellationToken = default);
    Task<string> GetRentStatusForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveTenancyApiIdAsync(int localTenancyId, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveHouseApiIdAsync(int localHouseId, CancellationToken cancellationToken = default);
    Task<Guid?> ResolveTenantApiIdAsync(int localTenantId, CancellationToken cancellationToken = default);
    int? ResolveLocalTenancyId(Guid apiTenancyId);
    int? ResolveLocalHouseId(Guid apiHouseId);
    int? ResolveLocalTenantId(Guid apiTenantId);
    int GetOrCreateLocalPaymentId(Guid apiPaymentId, string paymentType);
    Guid? ResolveApiPaymentId(int localPaymentId, string paymentType);
    Task<IReadOnlyList<RentLedgerItemApiDto>> GetRentLedgerForMonthAsync(int year, int month, Guid? houseId = null, string? statusFilter = null, string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepositLedgerItemApiDto>> GetDepositLedgerForMonthAsync(int year, int month, Guid? houseId = null, string? statusFilter = null, string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionItemApiDto>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, string? paymentType = null, Guid? houseId = null, Guid? tenantId = null, string? method = null, CancellationToken cancellationToken = default);
    Task<bool> UnrecordPaymentAsync(Guid apiPaymentId, string paymentType, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepositReturnReminderApiDto>> GetDepositReturnRemindersAsync(CancellationToken cancellationToken = default);
    Task RecordDepositReturnedAsync(Guid tenancyId, DateTime returnedDate, decimal amountReturned, string? notes, CancellationToken cancellationToken = default);
    Task DeleteAllTransactionsAsync(CancellationToken cancellationToken = default);
}

public class DepositReturnReminderApiDto
{
    public Guid TenancyId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string HouseAddress { get; set; } = string.Empty;
    public DateTime LeaveDate { get; set; }
    public decimal AmountToReturn { get; set; }
}
