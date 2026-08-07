using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Extracts the Expenses series from CashFlowMonths for LineAreaChart.ExpenseValues.</summary>
    public class CashFlowExpenseValuesConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IEnumerable points) return Array.Empty<double>();
            return points.Cast<CashFlowMonthPoint>().Select(p => (double)p.Expenses).ToList();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
