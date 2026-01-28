using System.Collections.Generic;

namespace Daryva.MVVM.Models
{
    public class LedgerRowModel
    {
        public string TenantName { get; set; } = string.Empty;
        public decimal RentAmount { get; set; }
        public string? RentCollectedBy { get; set; }
        public decimal DepositAmount { get; set; }
        public string? DepositCollectedBy { get; set; }
    }

    public class LedgerExportModel
    {
        public string HouseName { get; set; } = string.Empty;
        public string MonthYearDisplay { get; set; } = string.Empty;
        public IList<LedgerRowModel> Rows { get; set; } = new List<LedgerRowModel>();
        public decimal RentGivenToLandlord { get; set; }
        public IList<string> Collectors { get; set; } = new List<string>();
        public string OutputPath { get; set; } = string.Empty;
    }

    public class LedgerTotals
    {
        public decimal TotalRent { get; set; }
        public decimal TotalDeposit { get; set; }
        public Dictionary<string, decimal> RentCollectedBy { get; set; } = new();
        public decimal RentGivenToLandlord { get; set; }
        public decimal RemainingToLandlord => TotalRent - RentGivenToLandlord;
    }
}
