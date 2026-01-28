using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;

namespace Daryva.MVVM.Converters
{
    public class BoolToAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // If collapsed, return Center, otherwise Left
                return boolValue ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            }
            return HorizontalAlignment.Left;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return false;
        }
    }
}
