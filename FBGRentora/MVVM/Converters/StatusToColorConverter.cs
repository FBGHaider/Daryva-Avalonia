using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FBGRentora.MVVM.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string status)
            {
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
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
