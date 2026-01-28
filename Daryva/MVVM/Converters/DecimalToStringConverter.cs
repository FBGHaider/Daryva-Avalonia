using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    public class DecimalToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return d.ToString("G29", culture);
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var fmt = culture ?? CultureInfo.InvariantCulture;
            if (value is string s && decimal.TryParse(s, NumberStyles.Number, fmt, out var d))
                return d;
            return 0m;
        }
    }
}
