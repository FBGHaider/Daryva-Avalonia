using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using LandLordBuddy.MVVM.ViewModels;
using LandLordBuddy.Services;

namespace LandLordBuddy
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
                // Dispose all services and clean up resources
                if (_serviceProvider != null)
                {
                    // Dispose all scoped services (repositories, services with connections)
                    using (_serviceProvider)
                    {
                        // ServiceProvider disposal will handle IDisposable services
                    }
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
