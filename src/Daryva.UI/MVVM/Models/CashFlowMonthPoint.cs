namespace Daryva.MVVM.Models
{
    /// <summary>One point in the dashboard's monthly cash-flow chart. Deliberately carries no
    /// Avalonia UI types (no IBrush) -- the view decides colors via DynamicResource, matching how
    /// KpiCard.AccentBrush is already set from XAML rather than the ViewModel.</summary>
    public class CashFlowMonthPoint
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
    }
}
