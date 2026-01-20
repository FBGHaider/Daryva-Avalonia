namespace Daryva.MVVM.ViewModels
{
    public class RecipientViewModel
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public int? TenancyId { get; set; }
        public string HouseAddress { get; set; } = string.Empty;
        public bool HasEmail { get; set; }
        public bool HasWhatsApp { get; set; }
        public decimal? AmountDue { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsSelected { get; set; }
    }
}
