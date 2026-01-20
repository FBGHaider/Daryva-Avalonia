using System.Collections.ObjectModel;

namespace Daryva.MVVM.Models
{
    public class RentLedgerRowViewModel
    {
        public int TenancyId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance => AmountDue - AmountPaid;
        public string Status { get; set; } = "Unpaid"; // Paid, PartPaid, Unpaid, Overdue
        public decimal DepositRemaining { get; set; }
        public bool IsExpanded { get; set; }
        public ObservableCollection<PaymentDetailViewModel> PaymentsForThisMonth { get; set; } = new();
    }

    public class PaymentDetailViewModel
    {
        public DateTime PaidOn { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }
}
