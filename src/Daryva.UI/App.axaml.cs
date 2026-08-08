using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Auth;
using Daryva.Services.Theme;
using Daryva.Services.Settings;
using Daryva.MVVM.ViewModels;
using Daryva.MVVM.Views;
using System.Diagnostics;

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

        try
        {
            var appPaths = ServiceProvider.GetRequiredService<Daryva.Services.Platform.IAppPaths>();
            Daryva.Services.AppLogger.Initialize(appPaths.Logs);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize AppLogger: {ex.Message}");
        }

        // Force API-only data mode.
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            configurationService.SetLocalValue("DataMode", "Api");
            configurationService.ReloadLocalConfig();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enforce API mode: {ex.Message}");
        }

        // Initialize theme from saved preference
        InitializeTheme();

        _ = InitializeDateFormatAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = InitializeDesktopAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitializeDesktopAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            var serviceProvider = ServiceProvider!;
            ApplyLoginPersistencePolicy(serviceProvider);

            try
            {
                var authService = serviceProvider.GetRequiredService<IAuthService>();
                await authService.HasValidSessionAsync().ConfigureAwait(false);
            }
            catch
            {
                // No OIDC session or config missing; continue with existing session or sign-in.
            }

            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            var mainViewModel = serviceProvider.GetRequiredService<MainViewModel>();
            mainWindow.DataContext = mainViewModel;

            _ = serviceProvider.GetRequiredService<Daryva.Services.Business.ScheduledNotificationProcessor>();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                desktop.MainWindow = mainWindow;
                mainWindow.Show();
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() => desktop.Shutdown());
        }
    }

    private static void ApplyLoginPersistencePolicy(IServiceProvider serviceProvider)
    {
        try
        {
            var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
            var keepLoggedInRaw = configurationService.GetValue("KeepMeLoggedIn");
            // Default true: keep session so startup always has token available after load from store.
            var keepLoggedIn = string.IsNullOrWhiteSpace(keepLoggedInRaw) || string.Equals(keepLoggedInRaw, "true", StringComparison.OrdinalIgnoreCase);

            if (!keepLoggedIn)
            {
                var authSession = serviceProvider.GetRequiredService<IAuthSessionService>();
                authSession.ClearSession();
            }
        }
        catch
        {
            // If anything fails here, keep startup resilient and continue.
        }
    }

    private void InitializeTheme()
    {
        try
        {
            var settingsStore = ServiceProvider?.GetService<ISettingsStore>();
            var themeService = ServiceProvider?.GetService<IThemeService>();

            if (themeService != null)
            {
                var savedTheme = settingsStore?.GetSetting("Theme", "Dark") ?? "Dark";
                if (string.IsNullOrEmpty(savedTheme) || !themeService.AvailableThemes.Contains(savedTheme))
                {
                    savedTheme = "Dark";
                }
                // Always load theme on startup
                themeService.SetTheme(savedTheme);
            }
        }
        catch
        {
            // If theme initialization fails, try loading default Dark theme
            try
            {
                var themeService = ServiceProvider?.GetService<IThemeService>();
                themeService?.SetTheme("Dark");
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

    private async Task<bool> EnsureApiAvailableOnStartupAsync()
    {
        if (ServiceProvider == null)
            return false;

        if (await IsApiHealthyAsync(ServiceProvider).ConfigureAwait(false))
            return true;

        var resultTcs = new TaskCompletionSource<bool>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var statusText = new TextBlock
            {
                Text = "Daryva could not connect to the API server.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };

            var detailsText = new TextBlock
            {
                Text = "Click Restart API to start the backend and retry, or close the app.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.8
            };

            var restartButton = new Button { Content = "Restart API", Width = 120 };
            var closeButton = new Button { Content = "Close App", Width = 120 };

            var dialog = new Window
            {
                Title = "API Server Not Running",
                Width = 520,
                MinHeight = 190,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 14,
                    Children =
                    {
                        statusText,
                        detailsText,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Children = { restartButton, closeButton }
                        }
                    }
                }
            };

            closeButton.Click += (_, _) =>
            {
                resultTcs.TrySetResult(false);
                dialog.Close();
            };

            restartButton.Click += async (_, _) =>
            {
                restartButton.IsEnabled = false;
                closeButton.IsEnabled = false;
                statusText.Text = "Starting API server...";

                var started = TryStartApiServerProcess();
                if (!started)
                {
                    statusText.Text = "Could not start API automatically. Please start it manually and try again.";
                    detailsText.Text = "Run the API project or use Scripts/restart-dev.ps1, then reopen Daryva.";
                    restartButton.IsEnabled = true;
                    closeButton.IsEnabled = true;
                    return;
                }

                statusText.Text = "Waiting for API health endpoint...";
                var healthy = await WaitForApiHealthyAsync(ServiceProvider, timeoutSeconds: 20);
                if (healthy)
                {
                    resultTcs.TrySetResult(true);
                    dialog.Close();
                    return;
                }

                statusText.Text = "API did not become healthy in time.";
                detailsText.Text = "Please verify the backend logs and click Restart API again.";
                restartButton.IsEnabled = true;
                closeButton.IsEnabled = true;
            };

            dialog.Show();
        });

        return await resultTcs.Task.ConfigureAwait(false);
    }

    private static bool TryStartApiServerProcess()
    {
        try
        {
            var repoRoot = FindRepositoryRoot();
            if (string.IsNullOrWhiteSpace(repoRoot))
                return false;

            var restartScriptPath = Path.Combine(repoRoot, "Scripts", "restart-dev.ps1");
            if (File.Exists(restartScriptPath))
            {
                var scriptStartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{restartScriptPath}\" -ApiOnly",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var scriptProcess = Process.Start(scriptStartInfo);
                if (scriptProcess != null)
                    return true;
            }

            var apiProjectPath = Path.Combine(repoRoot, "src", "Daryva.Api", "Daryva.Api.csproj");
            if (!File.Exists(apiProjectPath))
                return false;

            var info = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{apiProjectPath}\"",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(info);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var solutionPath = Path.Combine(current.FullName, "Daryva-Avalonia.sln");
            if (File.Exists(solutionPath))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static async Task<bool> WaitForApiHealthyAsync(IServiceProvider serviceProvider, int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsApiHealthyAsync(serviceProvider))
                return true;

            await Task.Delay(1000);
        }

        return false;
    }

    private static async Task<bool> IsApiHealthyAsync(IServiceProvider serviceProvider)
    {
        try
        {
            var apiClient = serviceProvider.GetRequiredService<IApiClient>();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await apiClient.HttpClient.GetAsync("health", cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
