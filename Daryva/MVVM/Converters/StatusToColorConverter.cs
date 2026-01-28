using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Daryva.MVVM.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Check if we're in light theme by checking the application background color
            var isLightTheme = Application.Current?.Resources.TryGetValue("ApplicationBackgroundBrush", out var bgBrushRes) == true &&
                              bgBrushRes is SolidColorBrush bgBrush &&
                              bgBrush.Color.R > 200; // Light theme has light background (high R value)
            
            if (value is string status)
            {
                if (isLightTheme)
                {
                    // Lighter colors for light theme
                    return status switch
                    {
                        "Paid" => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)), // Light green (#66BB6A)
                        "PartPaid" => new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)), // Light orange (#FFB74D)
                        "Unpaid" => new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), // Light gray (#B0B0B0)
                        "Overdue" => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)), // Light red (#EF5350)
                        // Document statuses
                        "Active" => new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A)), // Light green
                        "Missing" => new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)), // Light gray
                        "Expired" => new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50)), // Light red
                        "ExpiringSoon" => new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D)), // Light orange
                        _ => new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)) // Light gray
                    };
                }
                else
                {
                    // Standard colors for dark theme
                    return status switch
                    {
                        "Paid" => new SolidColorBrush(Colors.Green),
                        "PartPaid" => new SolidColorBrush(Colors.Orange),
                        "Unpaid" => new SolidColorBrush(Colors.Gray),
                        "Overdue" => new SolidColorBrush(Colors.Red),
                        // Document statuses
                        "Active" => new SolidColorBrush(Colors.Green),
                        "Missing" => new SolidColorBrush(Colors.Gray),
                        "Expired" => new SolidColorBrush(Colors.Red),
                        "ExpiringSoon" => new SolidColorBrush(Colors.Orange),
                        _ => new SolidColorBrush(Colors.Gray)
                    };
                }
            }
            return new SolidColorBrush(isLightTheme ? Color.FromRgb(0xB0, 0xB0, 0xB0) : Colors.Gray);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
