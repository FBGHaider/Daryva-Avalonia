using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Returns a subtle badge background brush for member status (Active=green, Pending=orange).</summary>
    public class MemberStatusToBadgeBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is MemberStatus status)
            {
                return status switch
                {
                    MemberStatus.Active => new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x2F)),  // subtle green (StatusSuccess tint)
                    MemberStatus.Pending => new SolidColorBrush(Color.FromRgb(0x3D, 0x32, 0x1A)), // subtle orange (BrandPrimary tint)
                    _ => new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C))
                };
            }
            return new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
