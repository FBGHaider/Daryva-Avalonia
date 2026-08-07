using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    /// <summary>Converts an ObservableCollection.Count (int) to bool for IsVisible bindings --
    /// int doesn't implicitly convert to bool, and the "!" binding-path negation operator only
    /// works on genuine bool properties. Pass ConverterParameter="Invert" for "count == 0".</summary>
    public class CollectionCountToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var hasItems = value is int count && count > 0;
            var invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
            return invert ? !hasItems : hasItems;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
