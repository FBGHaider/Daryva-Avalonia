using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Daryva.MVVM.Converters
{
    /// <summary>Returns a subtle badge background brush for a member's role (Owner=purple/blue, Landlord=muted).</summary>
    public class IsPrimaryOwnerToBadgeBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPrimaryOwner && isPrimaryOwner)
                return new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x5C)); // subtle purple/blue

            return new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C)); // Surface2
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
