using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Daryva.MVVM.Converters
{
    /// <summary>Returns a brush for document row left border: Transparent for Valid/NoExpiry, danger for Expired, warning for ExpiringSoon.</summary>
    public class DocumentStatusToLeftBorderBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status switch
                {
                    "Expired" => new SolidColorBrush(Color.FromRgb(0xF3, 0x5B, 0x05)), // StatusDanger
                    "ExpiringSoon" => new SolidColorBrush(Color.FromRgb(0xF6, 0xA6, 0x09)), // StatusWarning
                    _ => new SolidColorBrush(Colors.Transparent)
                };
            }
            return new SolidColorBrush(Colors.Transparent);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
