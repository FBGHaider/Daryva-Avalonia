namespace Daryva.MVVM.Models
{
    public class HouseExpense
    {
        public int HouseExpenseId { get; set; }
        public int HouseId { get; set; }
        public DateTime DateIncurred { get; set; }
        public string Category { get; set; } = string.Empty; // Repairs, Bills, CouncilTax, Internet, Furniture, Cleaning, Maintenance, Other
        public decimal Amount { get; set; }
        public string? Vendor { get; set; }
        public string? Notes { get; set; }
        public int? ReceiptDocumentId { get; set; }
        
        // Navigation
        public House? House { get; set; }
        public Document? ReceiptDocument { get; set; }
    }
}
