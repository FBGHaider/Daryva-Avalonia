namespace Daryva.Services;

/// <summary>
/// Holds the current date/time format from Settings → General (DateFormat).
/// Set at app startup and when General settings are loaded/saved.
/// Used by DateTimeFormatConverter and when building table display strings.
/// </summary>
public static class DateTimeFormatProvider
{
    private static string _dateFormat = "dd/MM/yyyy";

    public static string DateFormat
    {
        get => _dateFormat;
        set => _dateFormat = string.IsNullOrWhiteSpace(value) ? "dd/MM/yyyy" : value.Trim();
    }

    public static string DateTimeFormat => DateFormat + " h:mm tt";

    public static string FormatDate(DateTime d)
    {
        try { return d.ToString(DateFormat); }
        catch { return d.ToString("dd/MM/yyyy"); }
    }

    public static string FormatDate(DateTime? d)
    {
        if (!d.HasValue) return "—";
        return FormatDate(d.Value);
    }

    public static string FormatDateTime(DateTime d)
    {
        try { return d.ToString(DateTimeFormat); }
        catch { return d.ToString("dd/MM/yyyy h:mm tt"); }
    }

    public static string FormatDateTime(DateTime? d)
    {
        if (!d.HasValue) return "—";
        return FormatDateTime(d.Value);
    }
}
