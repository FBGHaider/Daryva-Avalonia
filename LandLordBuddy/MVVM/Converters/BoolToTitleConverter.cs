using System.Globalization;
using System.Windows.Data;

namespace LandLordBuddy.MVVM.Converters
{
    public class BoolToTitleConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is string param && !string.IsNullOrEmpty(param))
            {
                var parts = param.Split('|');
                if (value is bool boolValue)
                {
                    return boolValue ? parts[0] : (parts.Length > 1 ? parts[1] : "");
                }
            }
            return "";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
