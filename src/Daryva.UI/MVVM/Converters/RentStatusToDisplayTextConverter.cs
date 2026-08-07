using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    /// <summary>Sentence-case display text for RentLedgerRowViewModel.Status -- "Part paid" not
    /// "PartPaid", matching the redesign's microcopy rule (sentence case, no robotic labels).</summary>
    public class RentStatusToDisplayTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Paid" => "Paid",
                "PartPaid" => "Part paid",
                "Unpaid" => "Unpaid",
                "Overdue" => "Overdue",
                _ => value?.ToString() ?? string.Empty
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
