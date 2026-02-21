using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    /// <summary>Converts a percentage (0-100) to a width in pixels for progress bars. Parameter is scale factor (e.g. 1.2 for max 120px).</summary>
    public class PercentToWidthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var scale = 1.2;
            if (parameter is string s && double.TryParse(s, NumberStyles.Any, culture, out var p))
                scale = p;
            else if (parameter is double d)
                scale = d;

            if (value is decimal dec)
                return Math.Max(0, (double)dec * scale);
            if (value is double dbl)
                return Math.Max(0, dbl * scale);
            if (value is int i)
                return Math.Max(0, i * scale);
            return 0.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
