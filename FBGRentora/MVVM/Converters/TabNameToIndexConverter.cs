using System.Globalization;
using System.Windows.Data;

namespace FBGRentora.MVVM.Converters
{
    public class TabNameToIndexConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string tabName)
            {
                return tabName == "Ledger" ? 0 : 1;
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index == 0 ? "Ledger" : "Transactions";
            }
            return "Ledger";
        }
    }
}
