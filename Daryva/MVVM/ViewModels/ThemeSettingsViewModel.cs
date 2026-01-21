using System.Windows;
using System.Windows.Input;
using System.Reflection;
using System.Linq;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Controls;
using Daryva.MVVM.Commands;
using Daryva.Services.Business;
using Daryva.Services.Dialog;

namespace Daryva.MVVM.ViewModels
{
    public class ThemeSettingsViewModel : BaseViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        private string _selectedTheme = "Light";

        public ThemeSettingsViewModel(ISettingsService settingsService, IDialogService dialogService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ResetCommand = new RelayCommand(async _ => await LoadAsync());

            _ = LoadAsync();
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value))
                {
                    // Save to database immediately (auto-save)
                    _ = SaveThemeToDatabaseAsync(value);
                    
                    // Apply theme immediately on UI thread
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        ApplyTheme(value);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() => ApplyTheme(value), DispatcherPriority.Render);
                    }
                }
            }
        }
        
        /// <summary>
        /// Saves the theme to the database without showing a dialog.
        /// </summary>
        private async Task SaveThemeToDatabaseAsync(string theme)
        {
            try
            {
                await _settingsService.SetSettingAsync("Theme", theme);
            }
            catch
            {
                // Silently fail for auto-save
            }
        }

        public List<string> ThemeOptions { get; } = new() { "Dark", "Light" };

        private async Task LoadAsync()
        {
            try
            {
                var savedTheme = await _settingsService.GetSettingAsync("Theme", "Light") ?? "Light";
                // Validate theme option
                if (savedTheme != "Dark" && savedTheme != "Light")
                    savedTheme = "Light";
                    
                _selectedTheme = savedTheme;
                OnPropertyChanged(nameof(SelectedTheme));
            }
            catch
            {
                _selectedTheme = "Light";
                OnPropertyChanged(nameof(SelectedTheme));
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Theme is already saved when dropdown changes, so just confirm
                await _settingsService.SetSettingAsync("Theme", SelectedTheme);
                
                // Apply the theme (should already be applied, but ensure it is)
                ApplyTheme(SelectedTheme);
                
                _dialogService.ShowMessage("Theme saved successfully.", "Theme Saved");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error saving theme settings: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Applies the specified theme using WPF UI ApplicationThemeManager.
        /// Uses the same logic as App.xaml.cs ApplyWpfUiTheme to ensure consistency.
        /// </summary>
        private void ApplyTheme(string theme)
        {
            if (string.IsNullOrEmpty(theme))
                return;
            
            try
            {
                // Use cached assembly from App.xaml.cs, or try to find it
                var assembly = App.WpfUiAssembly 
                    ?? AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name == "Wpf.Ui")
                    ?? Assembly.Load("Wpf.Ui");

                if (assembly == null)
                    return;

                var app = Application.Current;

                // Remove old theme dictionaries
                var resourcesToRemove = app.Resources.MergedDictionaries
                    .Where(d => 
                    {
                        var source = d.Source?.ToString() ?? "";
                        return source.Contains("Wpf.Ui") && 
                               (source.Contains("Theme") || source.Contains("Controls.xaml"));
                    })
                    .ToList();
                
                foreach (var dict in resourcesToRemove)
                {
                    app.Resources.MergedDictionaries.Remove(dict);
                }

                // Load resource dictionaries
                try
                {
                    var effectiveTheme = theme;
                    var themeFileName = effectiveTheme == "Light" ? "Light.xaml" : "Dark.xaml";
                    var resourceUri1 = new Uri($"pack://application:,,,/Wpf.Ui;component/Styles/Theme/{themeFileName}", UriKind.Absolute);
                    var resourceUri2 = new Uri("pack://application:,,,/Wpf.Ui;component/Styles/Controls.xaml", UriKind.Absolute);
                    
                    var themeDict = new ResourceDictionary { Source = resourceUri1 };
                    var controlsDict = new ResourceDictionary { Source = resourceUri2 };
                    
                    app.Resources.MergedDictionaries.Insert(0, themeDict);
                    app.Resources.MergedDictionaries.Add(controlsDict);
                }
                catch
                {
                    // Resource dictionaries not available - use manual resource updates
                }
                
                // Update resources based on theme
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (theme == "Light")
                        {
                            // Modern light theme with professional color palette
                            app.Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(
                                Color.FromRgb(0xF6, 0xF7, 0xF9)) { Opacity = 1.0 };
                            app.Resources["CardFillColorDefaultBrush"] = new SolidColorBrush(Colors.White) { Opacity = 1.0 };
                            app.Resources["ControlFillColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(0xF3, 0xF4, 0xF6)) { Opacity = 1.0 };
                            app.Resources["ControlFillColorSecondaryBrush"] = new SolidColorBrush(
                                Color.FromRgb(0xE5, 0xE7, 0xEB)) { Opacity = 1.0 };
                            app.Resources["ControlStrokeColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(0xE5, 0xE7, 0xEB)) { Opacity = 1.0 };
                            app.Resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(
                                Color.FromRgb(0x1F, 0x29, 0x33)) { Opacity = 1.0 };
                            app.Resources["TextFillColorSecondaryBrush"] = new SolidColorBrush(
                                Color.FromRgb(0x6B, 0x72, 0x80)) { Opacity = 1.0 };
                            app.Resources["NavigationViewBackgroundBrush"] = new SolidColorBrush(
                                Color.FromRgb(0xEC, 0xEF, 0xF3)) { Opacity = 1.0 };
                            app.Resources["LayerFillColorDefaultBrush"] = new SolidColorBrush(Colors.White) { Opacity = 1.0 };
                            // Lighter blue for light theme - softer, more modern appearance
                            app.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(0x4A, 0x9E, 0xFF)) { Opacity = 1.0 }; // Lighter blue (#4A9EFF)
                            app.Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(
                                Color.FromRgb(0x6B, 0xAD, 0xFF)) { Opacity = 1.0 }; // Lighter blue hover (#6BADFF)
                        }
                        else // Dark theme
                        {
                            app.Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(
                                Color.FromRgb(32, 32, 32)) { Opacity = 1.0 };
                            app.Resources["CardFillColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(45, 45, 45)) { Opacity = 1.0 };
                            app.Resources["ControlFillColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(50, 50, 50)) { Opacity = 1.0 };
                            app.Resources["ControlFillColorSecondaryBrush"] = new SolidColorBrush(
                                Color.FromRgb(61, 61, 61)) { Opacity = 1.0 };
                            app.Resources["ControlStrokeColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(80, 80, 80)) { Opacity = 1.0 };
                            app.Resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(Colors.White) { Opacity = 1.0 };
                            app.Resources["TextFillColorSecondaryBrush"] = new SolidColorBrush(
                                Color.FromRgb(204, 204, 204)) { Opacity = 1.0 };
                            app.Resources["NavigationViewBackgroundBrush"] = new SolidColorBrush(
                                Color.FromRgb(30, 30, 30)) { Opacity = 1.0 };
                            app.Resources["LayerFillColorDefaultBrush"] = new SolidColorBrush(
                                Color.FromRgb(37, 37, 37)) { Opacity = 1.0 };
                        }
                        
                        // Update all windows
                        foreach (Window window in app.Windows)
                        {
                            if (window?.FindName("MainBorder") is Border mainBorder)
                            {
                                if (theme == "Light")
                                {
                                    mainBorder.Background = new SolidColorBrush(Color.FromRgb(0xF6, 0xF7, 0xF9)) { Opacity = 1.0 };
                                    mainBorder.Effect = null;
                                }
                                else // Dark theme
                                {
                                    mainBorder.Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)) { Opacity = 1.0 };
                                    mainBorder.Effect = null;
                                }
                                window.InvalidateVisual();
                            }
                        }
                    }
                    catch
                    {
                        // Silently handle resource update errors
                    }
                }), DispatcherPriority.Render);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _dialogService.ShowMessage($"Failed to apply {theme} theme: {ex.Message}", "Theme Error");
                }), DispatcherPriority.Normal);
            }
        }
    }
}
