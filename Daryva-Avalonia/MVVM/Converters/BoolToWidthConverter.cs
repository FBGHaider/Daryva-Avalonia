using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Controls;

namespace Daryva.MVVM.Converters
{
    public class BoolToWidthConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // If collapsed, return 60 (icon-only width), otherwise 200 (full width)
                return boolValue ? new GridLength(60) : new GridLength(200);
            }
            return new GridLength(200);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
