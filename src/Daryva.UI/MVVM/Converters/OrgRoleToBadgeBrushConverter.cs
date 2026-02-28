using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Returns a subtle badge background brush for org role (Owner=purple/blue, others=muted).</summary>
    public class OrgRoleToBadgeBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is OrgRole role)
            {
                return role switch
                {
                    OrgRole.Owner => new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x5C)),   // subtle purple/blue
                    OrgRole.Admin => new SolidColorBrush(Color.FromRgb(0x1A, 0x3A, 0x5C)),   // subtle blue
                    OrgRole.Member => new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C)),  // Surface2
                    OrgRole.ReadOnly => new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C)), // Surface2
                    _ => new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C))
                };
            }
            return new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
