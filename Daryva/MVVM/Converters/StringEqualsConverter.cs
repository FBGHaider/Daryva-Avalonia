using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    /// <summary>
    /// Converter that returns Visible when the value equals the parameter, otherwise Collapsed.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string valueStr = value.ToString() ?? string.Empty;
            string paramStr = parameter.ToString() ?? string.Empty;

            return valueStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
