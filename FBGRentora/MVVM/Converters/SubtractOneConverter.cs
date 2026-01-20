using System.Globalization;
using System.Windows.Data;

namespace FBGRentora.MVVM.Converters
{
    public class SubtractOneConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return Math.Max(0, intValue - 1);
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int intValue)
                return intValue + 1;
            return 1;
        }
    }
}
