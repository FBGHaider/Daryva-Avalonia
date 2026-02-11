namespace Daryva.MVVM.Models
{
    /// <summary>Record that a tenant's deposit was paid back. Used to hide them from deposit-return list and deposit ledger after the return date.</summary>
    public class DepositReturn
    {
        public int DepositReturnId { get; set; }
        public int TenancyId { get; set; }
        public DateTime ReturnedDate { get; set; }
        public decimal AmountReturned { get; set; }
        public string? Notes { get; set; }
    }
}
