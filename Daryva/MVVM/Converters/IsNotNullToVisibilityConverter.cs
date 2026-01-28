using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    public class IsNotNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
