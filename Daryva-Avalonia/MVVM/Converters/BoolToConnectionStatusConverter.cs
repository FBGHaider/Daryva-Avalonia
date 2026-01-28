using System.Globalization;
using Avalonia.Data.Converters;

namespace Daryva.MVVM.Converters
{
    public class BoolToConnectionStatusConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "Connected" : "Disconnected";
            return "Disconnected";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
