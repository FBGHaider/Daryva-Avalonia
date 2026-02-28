using System.Diagnostics;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;

namespace Daryva.MVVM.ViewModels;

/// <summary>
/// Shown when user is signed in but requires org or profile setup (GET /api/me flags).
/// Offers "Open setup in browser" and "Refresh".
/// </summary>
public class SetupRequiredViewModel : BaseViewModel
{
    private readonly IOrgContext _orgContext;
    private readonly IConfigurationService _configuration;
    private readonly INavigationService _navigationService;
    private bool _isRefreshing;

    public SetupRequiredViewModel(IOrgContext orgContext, IConfigurationService configuration, INavigationService navigationService)
    {
        _orgContext = orgContext;
        _configuration = configuration;
        _navigationService = navigationService;

        OpenOnboardingInBrowserCommand = new RelayCommand(_ => OpenOnboardingInBrowser());
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public RelayCommand OpenOnboardingInBrowserCommand { get; }
    public RelayCommand RefreshCommand { get; }

    private void OpenOnboardingInBrowser()
    {
        var url = _configuration.GetValue("AppOnboardingUrl")?.Trim()
            ?? "https://app.daryva.com/onboarding";
        try
        {
            if (OperatingSystem.IsWindows())
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start(new ProcessStartInfo("xdg-open", url) { CreateNoWindow = true });
            }
        }
        catch
        {
            // Ignore
        }
    }

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await _orgContext.RefreshAsync().ConfigureAwait(true);
            if (!_orgContext.RequiresOnboarding && !_orgContext.RequiresProfile)
            {
                _navigationService.NavigateTo<DashboardViewModel>();
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
