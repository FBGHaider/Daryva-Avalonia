using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Extracts the Income series from CashFlowMonths for LineAreaChart.IncomeValues.</summary>
    public class CashFlowIncomeValuesConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IEnumerable points) return Array.Empty<double>();
            return points.Cast<CashFlowMonthPoint>().Select(p => (double)p.Income).ToList();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
