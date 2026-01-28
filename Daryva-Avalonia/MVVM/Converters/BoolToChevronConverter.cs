using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    public class BoolToChevronConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // If collapsed, show right chevron (▶), otherwise show left chevron (◀)
                return boolValue ? "▶" : "◀";
            }
            return "◀";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return false;
        }
    }
}
