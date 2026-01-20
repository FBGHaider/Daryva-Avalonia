using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using FBGRentora.MVVM.ViewModels;
using FBGRentora.Services;

namespace FBGRentora
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
        /// Called when the application starts.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply WPF UI theme FIRST (before creating windows)
            // This ensures theme resources are available when windows load
            ApplyWpfUiTheme();

            // Configure services
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();
            ServiceProvider = _serviceProvider;

            // Create and show the main window
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        /// <summary>
        /// Applies WPF UI theme using reflection (works around .NET Framework/.NET 8 compatibility issue).
        /// </summary>
        private void ApplyWpfUiTheme()
        {
            try
            {
                // Load WPF UI assembly
                var assembly = Assembly.Load("Wpf.Ui");
                if (assembly == null)
                {
                    System.Diagnostics.Debug.WriteLine("WPF UI assembly not found");
                    return;
                }

                // Get types
                var themeManagerType = assembly.GetType("Wpf.Ui.Appearance.ApplicationThemeManager");
                var themeEnumType = assembly.GetType("Wpf.Ui.Appearance.ApplicationTheme");
                var backdropEnumType = assembly.GetType("Wpf.Ui.Appearance.WindowBackdropType");

                if (themeManagerType == null || themeEnumType == null || backdropEnumType == null)
                {
                    System.Diagnostics.Debug.WriteLine("WPF UI types not found");
                    return;
                }

                // Try to load WPF UI resource dictionaries manually
                try
                {
                    var resourceUri1 = new Uri("pack://application:,,,/Wpf.Ui;component/Styles/Theme/Dark.xaml", UriKind.Absolute);
                    var resourceUri2 = new Uri("pack://application:,,,/Wpf.Ui;component/Styles/Controls.xaml", UriKind.Absolute);
                    
                    var darkThemeDict = new ResourceDictionary { Source = resourceUri1 };
                    var controlsDict = new ResourceDictionary { Source = resourceUri2 };
                    
                    // Merge into application resources
                    Resources.MergedDictionaries.Add(darkThemeDict);
                    Resources.MergedDictionaries.Add(controlsDict);
                    
                    System.Diagnostics.Debug.WriteLine("WPF UI resource dictionaries loaded");
                }
                catch (Exception resEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load WPF UI resources: {resEx.Message}");
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
                {
                    System.Diagnostics.Debug.WriteLine("WPF UI Apply method not found - using fallback dark theme");
                    return;
                }

                // Create enum values
                var darkTheme = Enum.Parse(themeEnumType, "Dark");

                // Apply theme
                if (applyMethod.GetParameters().Length == 3)
                {
                    var micaBackdrop = Enum.Parse(backdropEnumType, "Mica");
                    applyMethod.Invoke(null, new[] { darkTheme, micaBackdrop, true });
                }
                else if (applyMethod.GetParameters().Length == 2)
                {
                    var micaBackdrop = Enum.Parse(backdropEnumType, "Mica");
                    applyMethod.Invoke(null, new[] { darkTheme, micaBackdrop });
                }
                else
                {
                    applyMethod.Invoke(null, new[] { darkTheme });
                }
                
                System.Diagnostics.Debug.WriteLine("WPF UI Dark theme applied successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WPF UI theme application failed: {ex.Message}");
                
                // Fallback: Try applying just the theme without Mica
                try
                {
                    var assembly = Assembly.Load("Wpf.Ui");
                    var themeManagerType = assembly?.GetType("Wpf.Ui.Appearance.ApplicationThemeManager");
                    var themeEnumType = assembly?.GetType("Wpf.Ui.Appearance.ApplicationTheme");
                    
                    if (themeManagerType != null && themeEnumType != null)
                    {
                        var applyMethod = themeManagerType.GetMethod("Apply", new[] { themeEnumType });
                        if (applyMethod != null)
                        {
                            var darkTheme = Enum.Parse(themeEnumType, "Dark");
                            applyMethod.Invoke(null, new[] { darkTheme });
                            System.Diagnostics.Debug.WriteLine("WPF UI Dark theme applied (fallback)");
                        }
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"WPF UI theme fallback failed: {ex2.Message}");
                    // WPF UI theme application failed - app will use default styling
                }
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
