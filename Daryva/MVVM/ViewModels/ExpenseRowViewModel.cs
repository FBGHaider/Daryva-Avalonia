namespace Daryva.MVVM.ViewModels
{
    public class ExpenseRowViewModel
    {
        public int ExpenseId { get; set; }
        public DateTime DateIncurred { get; set; }
        public string DateIncurredDisplay { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public bool HasReceipt { get; set; }
        public int? ReceiptDocumentId { get; set; }
        public int HouseId { get; set; }
    }
}
