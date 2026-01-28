using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Daryva.Services;

namespace Daryva.MVVM.Converters;

public class DateTimeToDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "—";
        DateTime? dt = value switch
        {
            DateTime d => d,
            DateTimeOffset dto => dto.DateTime,
            _ => null
        };
        if (!dt.HasValue) return "—";
        var useDateOnly = string.Equals(parameter as string, "date", StringComparison.OrdinalIgnoreCase);
        return useDateOnly ? DateTimeFormatProvider.FormatDate(dt.Value) : DateTimeFormatProvider.FormatDateTime(dt.Value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
