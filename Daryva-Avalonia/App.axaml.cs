using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Daryva.Services;
using Daryva.Services.Theme;
using Daryva.Services.Settings;
using Daryva.MVVM.ViewModels;
using Daryva.MVVM.Views;

namespace Daryva;

public partial class App : Application
{
    public static ServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        // Initialize theme from saved preference
        InitializeTheme();

        _ = InitializeDateFormatAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = mainViewModel;
            desktop.MainWindow = mainWindow;

            _ = ServiceProvider.GetRequiredService<Daryva.Services.Business.ScheduledNotificationProcessor>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeTheme()
    {
        try
        {
            var settingsStore = ServiceProvider?.GetService<ISettingsStore>();
            var themeService = ServiceProvider?.GetService<IThemeService>();

            if (themeService != null)
            {
                var savedTheme = settingsStore?.GetSetting("Theme", "Light") ?? "Light";
                if (string.IsNullOrEmpty(savedTheme) || !themeService.AvailableThemes.Contains(savedTheme))
                {
                    savedTheme = "Light";
                }
                // Always load theme on startup
                themeService.SetTheme(savedTheme);
            }
        }
        catch
        {
            // If theme initialization fails, try loading default Light theme
            try
            {
                var themeService = ServiceProvider?.GetService<IThemeService>();
                themeService?.SetTheme("Light");
            }
            catch { /* ignore */ }
        }
    }

    private static async System.Threading.Tasks.Task InitializeDateFormatAsync()
    {
        try
        {
            var sp = ServiceProvider;
            if (sp == null) return;
            using var scope = sp.CreateScope();
            var settings = scope.ServiceProvider.GetService<Daryva.Services.Business.ISettingsService>();
            if (settings != null)
            {
                var format = await settings.GetSettingAsync("DateFormat", "dd/MM/yyyy").ConfigureAwait(false);
                Daryva.Services.DateTimeFormatProvider.DateFormat = format ?? "dd/MM/yyyy";
            }
        }
        catch { /* ignore */ }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add theme and settings services first
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IThemeService, ThemeService>();

        // Add application services
        services.AddApplicationServices();

        // Add ViewModels
        services.AddViewModels();

        // Register MainWindow
        services.AddSingleton<MainWindow>();
    }
}
