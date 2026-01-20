using System.Collections.ObjectModel;

namespace LandLordBuddy.MVVM.Models
{
    public class DepositLedgerRowViewModel
    {
        public int TenancyId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public decimal DepositRequired { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Balance => DepositRequired - AmountPaid;
        public string Status { get; set; } = "Unpaid"; // Paid, PartPaid, Unpaid
        public bool IsExpanded { get; set; }
        public ObservableCollection<PaymentDetailViewModel> Payments { get; set; } = new();
    }
}
