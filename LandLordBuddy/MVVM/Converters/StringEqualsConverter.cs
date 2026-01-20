using System.Globalization;
using System.Windows.Data;

namespace LandLordBuddy.MVVM.Converters
{
    /// <summary>
    /// Converter that returns Visible when the value equals the parameter, otherwise Collapsed.
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return System.Windows.Visibility.Collapsed;

            string valueStr = value.ToString() ?? string.Empty;
            string paramStr = parameter.ToString() ?? string.Empty;

            return valueStr.Equals(paramStr, StringComparison.OrdinalIgnoreCase)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
