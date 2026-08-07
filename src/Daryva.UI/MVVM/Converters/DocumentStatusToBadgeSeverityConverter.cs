using System.Globalization;
using Avalonia.Data.Converters;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Converters
{
    /// <summary>Maps Document.DocumentStatus ("NoExpiry"/"Valid"/"ExpiringSoon"/"Expired") to a
    /// StatusBadge severity.</summary>
    public class DocumentStatusToBadgeSeverityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Valid" => BadgeSeverity.Success,
                "ExpiringSoon" => BadgeSeverity.Warning,
                "Expired" => BadgeSeverity.Danger,
                _ => BadgeSeverity.Neutral // NoExpiry
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
