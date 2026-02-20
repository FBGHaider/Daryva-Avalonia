using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Daryva.Services.Theme
{
    /// <summary>
    /// Implementation of IThemeService using Avalonia's theme system.
    /// </summary>
    public class ThemeService : IThemeService
    {
        private string _currentTheme;
        private IResourceProvider? _currentThemeInclude;

        public ThemeService()
        {
            _currentTheme = "Dark";
            AvailableThemes = new[] { "Light", "Dark" };
        }

        public string CurrentTheme => _currentTheme;

        public IReadOnlyList<string> AvailableThemes { get; }

        public event EventHandler<string>? ThemeChanged;

        public void SetTheme(string themeName)
        {
            if (!AvailableThemes.Contains(themeName))
            {
                throw new ArgumentException($"Unknown theme: {themeName}", nameof(themeName));
            }

            _currentTheme = themeName;

            var app = Application.Current;
            if (app == null)
                return;

            // Set the theme variant (this handles the base Fluent theme)
            var themeVariant = themeName == "Dark" 
                ? ThemeVariant.Dark 
                : ThemeVariant.Light;

            app.RequestedThemeVariant = themeVariant;

            // Swap custom theme resource dictionary
            SwapThemeResourceDictionary(app, themeName);

            // Notify listeners
            ThemeChanged?.Invoke(this, themeName);
        }

        private void SwapThemeResourceDictionary(Application app, string themeName)
        {
            try
            {
                // Remove existing custom theme include
                if (_currentThemeInclude != null)
                {
                    app.Resources.MergedDictionaries.Remove(_currentThemeInclude);
                }

                // Load the new theme using ResourceInclude
                var themeUri = themeName == "Dark"
                    ? new Uri("avares://Daryva/Themes/DarkTheme.axaml")
                    : new Uri("avares://Daryva/Themes/LightTheme.axaml");

                _currentThemeInclude = new ResourceInclude(themeUri) { Source = themeUri };

                // Add to app resources
                app.Resources.MergedDictionaries.Add(_currentThemeInclude);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to swap theme: {ex.Message}");
            }
        }
    }
}
