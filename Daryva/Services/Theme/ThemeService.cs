using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Markup.Xaml;
using Avalonia.Controls;

namespace Daryva.Services.Theme
{
    /// <summary>
    /// Implementation of IThemeService using Avalonia's theme system.
    /// </summary>
    public class ThemeService : IThemeService
    {
        private string _currentTheme;

        public ThemeService()
        {
            _currentTheme = "Light";
            AvailableThemes = new[] { "Light", "Dark" };
        }

        public string CurrentTheme => _currentTheme;

        public IReadOnlyList<string> AvailableThemes { get; }

        public event EventHandler<string>? ThemeChanged;

        private void LoadThemeStyles()
        {
            // Theme styles are loaded as ResourceDictionaries, not StyleIncludes
            // This method can be used for initialization if needed
        }

        public void SetTheme(string themeName)
        {
            if (!AvailableThemes.Contains(themeName))
            {
                throw new ArgumentException($"Unknown theme: {themeName}", nameof(themeName));
            }

            if (_currentTheme == themeName)
                return;

            _currentTheme = themeName;

            var app = Application.Current;
            if (app == null)
                return;

            // Set the theme variant (this handles the base Fluent theme)
            var themeVariant = themeName == "Dark" 
                ? ThemeVariant.Dark 
                : ThemeVariant.Light;

            app.RequestedThemeVariant = themeVariant;

            // Update custom theme styles
            UpdateThemeStyles(app, themeName);

            // Notify listeners
            ThemeChanged?.Invoke(this, themeName);
        }

        private void UpdateThemeStyles(Application app, string themeName)
        {
            try
            {
                // Update resource dictionaries
                if (app.Resources.MergedDictionaries != null)
                {
                    // Remove existing theme resource dictionaries by loading and comparing
                    var dictsToRemove = new List<IResourceProvider>();
                    foreach (var dict in app.Resources.MergedDictionaries)
                    {
                        if (dict is ResourceDictionary rd)
                        {
                            // Check if it's a theme dictionary by trying to match the URI
                            // Since we can't access Source directly, we'll use a different approach
                            // For now, just clear and reload
                        }
                    }

                    // Clear existing theme dictionaries and reload
                    // Note: In Avalonia, theme dictionaries are typically loaded via XAML
                    // This is a simplified approach - the theme switching is mainly handled
                    // by RequestedThemeVariant above
                }
            }
            catch
            {
                // If style update fails, continue with base Fluent theme
            }
        }
    }
}
