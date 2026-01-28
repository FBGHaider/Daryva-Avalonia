using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    public class ByteToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is byte b)
                return b.ToString(culture);
            return "1";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var fmt = culture ?? CultureInfo.InvariantCulture;
            if (value is string s && byte.TryParse(s, NumberStyles.Integer, fmt, out var b))
                return b;
            return (byte)1;
        }
    }
}
