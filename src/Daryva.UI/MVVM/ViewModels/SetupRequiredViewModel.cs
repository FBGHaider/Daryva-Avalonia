using System.Collections.ObjectModel;
using System.Diagnostics;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Auth;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;

namespace Daryva.MVVM.ViewModels;

/// <summary>
/// Shown when user is signed in but must create an org (0 orgs) or choose an org (multiple orgs).
/// Create-only: Create organisation, Restore from code, Open in browser, Refresh, Sign out.
/// Choose-org: Dropdown of organisations + Continue; or auto-continue if single org.
/// </summary>
public class SetupRequiredViewModel : BaseViewModel
{
    private readonly IOrgContext _orgContext;
    private readonly IConfigurationService _configuration;
    private readonly INavigationService _navigationService;
    private readonly IOrganizationApiService _organizationApiService;
    private readonly IDialogService _dialogService;
    private readonly IAuthService _authService;
    private readonly IAuthSessionService _authSession;
    private bool _isRefreshing;
    private OrgSummary? _selectedOrg;

    public SetupRequiredViewModel(
        IOrgContext orgContext,
        IConfigurationService configuration,
        INavigationService navigationService,
        IOrganizationApiService organizationApiService,
        IDialogService dialogService,
        IAuthService authService,
        IAuthSessionService authSession)
    {
        _orgContext = orgContext;
        _configuration = configuration;
        _navigationService = navigationService;
        _organizationApiService = organizationApiService;
        _dialogService = dialogService;
        _authService = authService;
        _authSession = authSession;

        OrganisationsList = new ObservableCollection<OrgSummary>();
        OpenOnboardingInBrowserCommand = new RelayCommand(_ => OpenOnboardingInBrowser());
        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
        CreateOrganisationCommand = new RelayCommand(async _ => await CreateOrganisationAsync());
        RestoreFromCodeCommand = new RelayCommand(async _ => await RestoreFromCodeAsync());
        ContinueWithSelectedOrgCommand = new RelayCommand(_ => ContinueWithSelectedOrg());
        SignOutCommand = new RelayCommand(async _ => await SignOutAsync());

        SyncOrganisationsFromContext();
        _ = AutoContinueIfSingleOrgAsync();
    }

    /// <summary>True when user has zero orgs: show Create org + Restore only.</summary>
    public bool ShowCreateOnly => _orgContext.Orgs.Count == 0;
    /// <summary>True when user has orgs but none selected: show Choose Org panel.</summary>
    public bool ShowChooseOrg => _orgContext.Orgs.Count > 0;

    /// <summary>e.g. "Signed in with user@example.com" for display on the setup screen.</summary>
    public string SignedInWithText =>
        string.IsNullOrWhiteSpace(_authSession.Email)
            ? "Signed in"
            : $"Signed in with {_authSession.Email.Trim()}";

    public ObservableCollection<OrgSummary> OrganisationsList { get; }
    public OrgSummary? SelectedOrg
    {
        get => _selectedOrg;
        set => SetProperty(ref _selectedOrg, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public RelayCommand OpenOnboardingInBrowserCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand CreateOrganisationCommand { get; }
    public RelayCommand RestoreFromCodeCommand { get; }
    public RelayCommand ContinueWithSelectedOrgCommand { get; }
    public RelayCommand SignOutCommand { get; }

    private void SyncOrganisationsFromContext()
    {
        OrganisationsList.Clear();
        foreach (var o in _orgContext.Orgs)
            OrganisationsList.Add(o);
        SelectedOrg = OrganisationsList.FirstOrDefault();
        OnPropertyChanged(nameof(ShowCreateOnly));
        OnPropertyChanged(nameof(ShowChooseOrg));
    }

    private async System.Threading.Tasks.Task AutoContinueIfSingleOrgAsync()
    {
        if (_orgContext.Orgs.Count != 1) return;
        await _orgContext.SetCurrentOrgAsync(_orgContext.Orgs[0].Id).ConfigureAwait(true);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            _navigationService.NavigateTo<DashboardViewModel>());
    }

    private void ContinueWithSelectedOrg()
    {
        if (SelectedOrg == null) return;
        _orgContext.SetCurrentOrgAsync(SelectedOrg.Id);
        _navigationService.NavigateTo<DashboardViewModel>();
    }

    private void OpenOnboardingInBrowser()
    {
        var url = _configuration.GetValue("AppOnboardingUrl")?.Trim()
            ?? "https://app.daryva.com/onboarding";
        try
        {
            if (OperatingSystem.IsWindows())
            {
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
            SyncOrganisationsFromContext();
            if (_orgContext.Orgs.Count > 0 && _orgContext.CurrentOrgId.HasValue)
            {
                _navigationService.NavigateTo<DashboardViewModel>();
                return;
            }
            _ = AutoContinueIfSingleOrgAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async System.Threading.Tasks.Task CreateOrganisationAsync()
    {
        var name = await _dialogService.ShowInputDialogAsync(
            "Enter a name for your organisation:",
            "Create organisation",
            "My Organisation").ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(name))
            return;

        IsRefreshing = true;
        try
        {
            var created = await _organizationApiService.CreateOrganizationAsync(name.Trim()).ConfigureAwait(false);
            await _orgContext.RefreshAsync().ConfigureAwait(false);
            // Explicitly set the new org as current so X-Org-Id is correct and "not a member" is avoided.
            await _orgContext.SetCurrentOrgFromRecoveryAsync(created.Id, created.Name).ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _navigationService.NavigateTo<DashboardViewModel>();
            });
        }
        catch (OperationCanceledException)
        {
            _dialogService.ShowMessage(
                "The request took too long. Check that the API is running and your API URL is correct (see Settings).",
                "Create organisation");
        }
        catch (Exception ex)
        {
            var msg = ex.Message ?? "";
            if (msg.Contains("401", StringComparison.OrdinalIgnoreCase) || msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                _dialogService.ShowMessage(
                    "The API rejected your sign-in (401 Unauthorized). Your session may have expired -- try signing out and back in.",
                    "Error");
            else
                _dialogService.ShowMessage($"Could not create organisation: {msg}", "Error");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async System.Threading.Tasks.Task RestoreFromCodeAsync()
    {
        var input = await _dialogService.ShowInputDialogAsync(
            "Paste your organisation recovery code (the long ID you saved earlier, e.g. for \"2 houses\"):",
            "Restore organisation",
            "").ConfigureAwait(true);
        var raw = input?.Trim() ?? "";
        if (string.IsNullOrEmpty(raw))
            return;
        if (!Guid.TryParse(raw, out var orgId) || orgId == Guid.Empty)
        {
            _dialogService.ShowMessage("That doesn't look like a valid recovery code. It should be a long ID like: 12345678-1234-1234-1234-123456789abc", "Invalid code");
            return;
        }
        IsRefreshing = true;
        try
        {
            var org = await _organizationApiService.GetOrganizationAsync(orgId).ConfigureAwait(false);
            await _orgContext.SetCurrentOrgFromRecoveryAsync(orgId, org.Name).ConfigureAwait(false);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _dialogService.ShowMessage($"Restored organisation \"{org.Name}\".", "Organisation restored");
                _navigationService.NavigateTo<DashboardViewModel>();
            });
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Contains("403", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("not a member", StringComparison.OrdinalIgnoreCase)
                ? "You are not a member of that organisation, or the code is wrong. Sign in with the account that owns that org."
                : ex.Message.Contains("404", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Not Found", StringComparison.OrdinalIgnoreCase)
                ? "That organisation was not found. It may have been permanently deleted (e.g. via Remove organisation), or you are not a member. Deleted organisations and their data cannot be recovered."
                : ex.Message;
            _dialogService.ShowMessage(msg, "Could not restore");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async System.Threading.Tasks.Task SignOutAsync()
    {
        await _authService.SignOutAsync().ConfigureAwait(true);
        // MainViewModel subscribes to StateChanged and will navigate to SignInView
    }
}
