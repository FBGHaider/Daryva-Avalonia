using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Daryva.MVVM.ViewModels;
using Daryva.Services;

namespace Daryva
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;

        /// <summary>
        /// Gets the service provider for dependency injection.
        /// </summary>
        public static ServiceProvider? ServiceProvider { get; private set; }
        
        /// <summary>
        /// Gets the cached WPF UI assembly for theme management.
        /// </summary>
        public static Assembly? WpfUiAssembly { get; private set; }

        public App()
        {
            // Handle unhandled exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unhandled exception: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Unhandled exception: {ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Called when the application starts.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Apply default theme FIRST (before configuring services)
                // This ensures theme resources are available when windows load
                var startupTheme = "Light";
                ApplyWpfUiTheme(startupTheme);

                // Configure services
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                _serviceProvider = serviceCollection.BuildServiceProvider();
                ServiceProvider = _serviceProvider;

                // Create and show the main window
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                MainWindow = mainWindow;
                
                // Load saved theme from database after window is shown (non-blocking)
                mainWindow.Loaded += async (s, e) =>
                {
                        try
                        {
                            var settingsService = _serviceProvider.GetRequiredService<Services.Business.ISettingsService>();
                            var savedTheme = await settingsService.GetSettingAsync("Theme", "Light");
                            
                            if (!string.IsNullOrEmpty(savedTheme) && 
                                (savedTheme == "Light" || savedTheme == "Dark") && 
                                savedTheme != startupTheme)
                            {
                                ApplyWpfUiTheme(savedTheme);
                                ApplyThemeResources(savedTheme, mainWindow);
                            }
                            else
                            {
                                ApplyThemeResources(startupTheme, mainWindow);
                            }
                        }
                        catch
                        {
                            // If database access fails, use default theme
                            ApplyThemeResources(startupTheme, mainWindow);
                        }
                };
                
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application: {ex.Message}\n\n{ex.StackTrace}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
        
        /// <summary>
        /// Applies theme resources based on the theme name.
        /// </summary>
        private void ApplyThemeResources(string theme, System.Windows.Window mainWindow)
        {
            var mainBorder = mainWindow.FindName("MainBorder") as System.Windows.Controls.Border;
            
            if (theme == "Light")
            {
                // Modern light theme with professional color palette
                Resources["ApplicationBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF6, 0xF7, 0xF9)) { Opacity = 1.0 };
                Resources["CardFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.White) { Opacity = 1.0 };
                Resources["ControlFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF3, 0xF4, 0xF6)) { Opacity = 1.0 };
                Resources["ControlFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE5, 0xE7, 0xEB)) { Opacity = 1.0 };
                Resources["ControlStrokeColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE5, 0xE7, 0xEB)) { Opacity = 1.0 };
                Resources["TextFillColorPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1F, 0x29, 0x33)) { Opacity = 1.0 };
                Resources["TextFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x6B, 0x72, 0x80)) { Opacity = 1.0 };
                Resources["NavigationViewBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xEC, 0xEF, 0xF3)) { Opacity = 1.0 };
                Resources["LayerFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.White) { Opacity = 1.0 };
                // Lighter blue for light theme - softer, more modern appearance
                Resources["AccentFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x4A, 0x9E, 0xFF)) { Opacity = 1.0 }; // Lighter blue (#4A9EFF)
                Resources["AccentFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x6B, 0xAD, 0xFF)) { Opacity = 1.0 }; // Lighter blue hover (#6BADFF)
                
                if (mainBorder != null)
                {
                    mainBorder.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xF6, 0xF7, 0xF9)) { Opacity = 1.0 };
                }
            }
            else // Dark theme
            {
                Resources["ApplicationBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(32, 32, 32)) { Opacity = 1.0 };
                Resources["CardFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(45, 45, 45)) { Opacity = 1.0 };
                Resources["ControlFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(50, 50, 50)) { Opacity = 1.0 };
                Resources["ControlFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(61, 61, 61)) { Opacity = 1.0 };
                Resources["ControlStrokeColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(80, 80, 80)) { Opacity = 1.0 };
                Resources["TextFillColorPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.White) { Opacity = 1.0 };
                Resources["TextFillColorSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(204, 204, 204)) { Opacity = 1.0 };
                Resources["NavigationViewBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(30, 30, 30)) { Opacity = 1.0 };
                Resources["LayerFillColorDefaultBrush"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(37, 37, 37)) { Opacity = 1.0 };
                
                if (mainBorder != null)
                {
                    mainBorder.Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(32, 32, 32)) { Opacity = 1.0 };
                }
            }
        }

        /// <summary>
        /// Applies WPF UI theme using reflection (works around .NET Framework/.NET 8 compatibility issue).
        /// </summary>
        /// <param name="theme">Theme name: "Dark" or "Light"</param>
        private void ApplyWpfUiTheme(string theme = "Dark")
        {
            try
            {
                var effectiveTheme = theme;
                
                // Load WPF UI assembly
                var assembly = Assembly.Load("Wpf.Ui");
                if (assembly == null)
                    return;
                
                // Cache the assembly for use by ViewModels
                WpfUiAssembly = assembly;

                // Get types
                var themeManagerType = assembly.GetType("Wpf.Ui.Appearance.ApplicationThemeManager");
                var themeEnumType = assembly.GetType("Wpf.Ui.Appearance.ApplicationTheme");
                var backdropEnumType = assembly.GetType("Wpf.Ui.Appearance.WindowBackdropType");

                if (themeManagerType == null || themeEnumType == null || backdropEnumType == null)
                    return;

                // Try to load WPF UI resource dictionaries
                try
                {
                    // Remove old theme dictionaries if they exist
                    var resourcesToRemove = Resources.MergedDictionaries
                        .Where(d => d.Source?.ToString().Contains("Wpf.Ui") == true && d.Source.ToString().Contains("Theme"))
                        .ToList();
                    foreach (var dict in resourcesToRemove)
                    {
                        Resources.MergedDictionaries.Remove(dict);
                    }

                    var themeFileName = effectiveTheme == "Light" ? "Light.xaml" : "Dark.xaml";
                    var resourceUri1 = new Uri($"pack://application:,,,/Wpf.Ui;component/Styles/Theme/{themeFileName}", UriKind.Absolute);
                    var resourceUri2 = new Uri("pack://application:,,,/Wpf.Ui;component/Styles/Controls.xaml", UriKind.Absolute);
                    
                    var themeDict = new ResourceDictionary { Source = resourceUri1 };
                    var controlsDict = new ResourceDictionary { Source = resourceUri2 };
                    
                    // Merge into application resources
                    Resources.MergedDictionaries.Insert(0, themeDict);
                    Resources.MergedDictionaries.Add(controlsDict);
                }
                catch
                {
                    // Resource dictionaries may not be available - use manual resource updates instead
                }

                // Get Apply method with 3 parameters (theme, backdrop, updateAccent)
                var applyMethod = themeManagerType.GetMethod("Apply", new[] 
                { 
                    themeEnumType,
                    backdropEnumType,
                    typeof(bool)
                });

                if (applyMethod == null)
                {
                    // Try method with 2 parameters
                    applyMethod = themeManagerType.GetMethod("Apply", new[] 
                    { 
                        themeEnumType,
                        backdropEnumType
                    });
                }

                if (applyMethod == null)
                {
                    // Try method with 1 parameter (just theme)
                    applyMethod = themeManagerType.GetMethod("Apply", new[] { themeEnumType });
                }

                if (applyMethod == null)
                    return;

                // Use effectiveTheme with None backdrop
                var themeValue = Enum.Parse(themeEnumType, effectiveTheme);
                var backdrop = Enum.Parse(backdropEnumType, "None");

                // Apply theme with appropriate backdrop
                var paramCount = applyMethod.GetParameters().Length;
                if (paramCount == 3)
                {
                    applyMethod.Invoke(null, new[] { themeValue, backdrop, true });
                }
                else if (paramCount == 2)
                {
                    applyMethod.Invoke(null, new[] { themeValue, backdrop });
                }
                else
                {
                    applyMethod.Invoke(null, new[] { themeValue });
                }
            }
            catch
            {
                // Theme application failed - app will use default styling
            }
        }

        /// <summary>
        /// Configures the dependency injection services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private void ConfigureServices(IServiceCollection services)
        {
            // Add application services
            services.AddApplicationServices();

            // Add ViewModels
            services.AddViewModels();

            // Register MainWindow
            services.AddTransient<MainWindow>();
        }

        /// <summary>
        /// Called when the application is shutting down.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Cleanup main window ViewModel first
                if (MainWindow?.DataContext is MainWindowViewModel mainViewModel)
                {
                    mainViewModel.Cleanup();
                }
                
                // Dispose all services and clean up resources
                if (_serviceProvider != null)
                {
                    // Dispose all scoped services (repositories, services with connections)
                    _serviceProvider.Dispose();
                    _serviceProvider = null;
                    ServiceProvider = null;
                }
                
                // Force garbage collection to clean up any remaining resources
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            catch
            {
                // Ignore disposal errors during shutdown
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}
