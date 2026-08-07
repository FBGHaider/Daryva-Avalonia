using System.Globalization;
using Avalonia.Data.Converters;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Maps a RentLedgerRowViewModel.Status string ("Paid"/"PartPaid"/"Unpaid"/"Overdue")
    /// to a StatusBadge severity. PartPaid and Unpaid both read as "not settled yet" (Warning) --
    /// only Overdue gets Danger.</summary>
    public class RentStatusToBadgeSeverityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Paid" => BadgeSeverity.Success,
                "Overdue" => BadgeSeverity.Danger,
                "PartPaid" => BadgeSeverity.Warning,
                "Unpaid" => BadgeSeverity.Warning,
                _ => BadgeSeverity.Neutral
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
