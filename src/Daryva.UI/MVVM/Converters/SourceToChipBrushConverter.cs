using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Daryva.MVVM.Converters
{
    /// <summary>Returns a brush for document Source chip: muted for Uploaded, BrandSecondary for Generated, subtle for Imported.</summary>
    public class SourceToChipBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var source = (value as string)?.Trim();
            if (string.IsNullOrEmpty(source)) return new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C)); // Surface2
            if (source.Equals("Generated", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(Color.FromRgb(0x02, 0x89, 0xCD)); // BrandSecondary
            if (source.Equals("Imported", StringComparison.OrdinalIgnoreCase))
                return new SolidColorBrush(Color.FromRgb(0x22, 0x33, 0x52)); // Slightly lighter than Surface2
            return new SolidColorBrush(Color.FromRgb(0x18, 0x23, 0x3C)); // Surface2 muted
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
