using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    public class DecimalToDatabaseSizeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return $"{d:N2} MB";
            return "— MB";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
