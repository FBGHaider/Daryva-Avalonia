using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Extracts the month labels from CashFlowMonths for LineAreaChart.Labels.</summary>
    public class CashFlowLabelsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IEnumerable points) return Array.Empty<string>();
            return points.Cast<CashFlowMonthPoint>().Select(p => p.MonthLabel).ToList();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
