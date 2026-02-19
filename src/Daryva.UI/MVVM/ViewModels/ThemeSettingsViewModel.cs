using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Theme;
using Daryva.Services.Settings;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// ViewModel for theme settings.
    /// </summary>
    public class ThemeSettingsViewModel : BaseViewModel
    {
        private readonly IThemeService _themeService;
        private readonly ISettingsStore _settingsStore;
        private string _selectedTheme;

        public ThemeSettingsViewModel(IThemeService themeService, ISettingsStore settingsStore)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));

            _selectedTheme = _themeService.CurrentTheme;

            // Subscribe to theme changes
            _themeService.ThemeChanged += (s, theme) =>
            {
                if (_selectedTheme != theme)
                {
                    _selectedTheme = theme;
                    OnPropertyChanged(nameof(SelectedTheme));
                }
            };

            // Load saved theme preference
            var savedTheme = _settingsStore.GetSetting("Theme", "Light");
            if (!string.IsNullOrEmpty(savedTheme) && _themeService.AvailableThemes.Contains(savedTheme))
            {
                _selectedTheme = savedTheme;
                _themeService.SetTheme(savedTheme);
            }

            // Command to switch theme
            SwitchThemeCommand = new RelayCommand<string>(theme =>
            {
                if (!string.IsNullOrEmpty(theme) && _themeService.AvailableThemes.Contains(theme))
                {
                    SelectedTheme = theme;
                }
            });
        }

        public ICommand SwitchThemeCommand { get; }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && !string.IsNullOrEmpty(value))
                {
                    // Apply theme immediately
                    _themeService.SetTheme(value);
                    // Persist to settings
                    _settingsStore.SetSetting("Theme", value);
                }
            }
        }

        public IReadOnlyList<string> AvailableThemes => _themeService.AvailableThemes;
    }
}
