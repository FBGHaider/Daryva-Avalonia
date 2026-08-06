using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

public interface IPaymentService
{
    /// <summary>
    /// Throws ArgumentException for validation failures (including the payment-amount sanity ceiling),
    /// KeyNotFoundException if the tenancy doesn't exist.
    /// </summary>
    Task<RecordPaymentResponse> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null return means the tenancy (or its same-group tenancies) wasn't found.</summary>
    Task<decimal?> GetTotalDepositPaidAsync(Guid tenancyId, CancellationToken cancellationToken = default);

    Task<decimal?> GetTotalRentPaidForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Null return means the tenancy wasn't found.</summary>
    Task<string?> GetDepositStatusAsync(Guid tenancyId, decimal? requiredAmount, CancellationToken cancellationToken = default);

    Task<string?> GetRentStatusForPeriodAsync(Guid tenancyId, int year, int month, CancellationToken cancellationToken = default);

    Task<IEnumerable<RentLedgerItemResponse>> GetRentLedgerAsync(int year, int month, Guid? houseId, string? statusFilter, string? searchTerm, CancellationToken cancellationToken = default);

    Task<IEnumerable<DepositLedgerItemResponse>> GetDepositLedgerAsync(int year, int month, Guid? houseId, string? statusFilter, string? searchTerm, CancellationToken cancellationToken = default);

    Task<IEnumerable<TransactionItemResponse>> GetTransactionsAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? paymentType,
        Guid? houseId,
        Guid? tenantId,
        string? method,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DepositReturnReminderResponse>> GetDepositReturnRemindersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Throws ArgumentException for validation failures (negative amount, invalid date, amount exceeds
    /// available deposit), KeyNotFoundException if the tenancy doesn't exist, InvalidOperationException
    /// if a return was already recorded for this tenancy.
    /// </summary>
    Task RecordDepositReturnedAsync(RecordDepositReturnedRequest request, CancellationToken cancellationToken = default);

    Task VoidAllTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the payment exists (voided now, or already voided -- both are a no-op success),
    /// false if not found. Throws ArgumentException if paymentType isn't "Rent" or "Deposit".
    /// </summary>
    Task<bool> UnrecordPaymentAsync(string paymentType, Guid paymentId, CancellationToken cancellationToken = default);
}
