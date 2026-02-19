namespace Daryva.Services.Theme
{
    /// <summary>
    /// Service for managing application themes.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Gets the current theme name.
        /// </summary>
        string CurrentTheme { get; }

        /// <summary>
        /// Sets the application theme.
        /// </summary>
        /// <param name="themeName">The theme name (e.g., "Light", "Dark").</param>
        void SetTheme(string themeName);

        /// <summary>
        /// Event raised when the theme changes.
        /// </summary>
        event EventHandler<string>? ThemeChanged;

        /// <summary>
        /// Gets the list of available theme names.
        /// </summary>
        IReadOnlyList<string> AvailableThemes { get; }
    }
}
