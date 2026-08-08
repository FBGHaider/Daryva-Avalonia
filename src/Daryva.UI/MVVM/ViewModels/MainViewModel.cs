using System.Collections.ObjectModel;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Daryva.Services.Auth;
using Daryva.Services.Dialog;
using Avalonia.Controls;
using Avalonia.Threading;
using Material.Icons;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// ViewModel for the main window with navigation support.
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly ISettingsService _settingsService;
        private readonly IConfigurationService _configurationService;
        private readonly IApiClient _apiClient;
        private readonly IOrganizationApiService _organizationApiService;
        private readonly IOrganisationService? _organisationService;
        private readonly IAuthSessionService _authSessionService;
        private readonly IOrgContext _orgContext;
        private readonly IAuthService _authService;
        private readonly ISupportSessionApiService _supportSessionApiService;
        private readonly IDialogService _dialogService;
        private readonly ProfileMenuViewModel _profileMenu;
        private BaseViewModel? _currentViewModel;
        private NavigationItem? _selectedNavigationItem;
        private EventHandler<BaseViewModel?>? _navigationHandler;
        private bool _isNavigationCollapsed;
        private bool _isOnboardingMode;
        private string _currentOrganizationName = "(No org selected)";
        private string _currentOrgRole = string.Empty;
        private Guid? _lastDisplayedOrgId;
        private bool _isEndingSupportSession;
        private DispatcherTimer? _supportSessionCountdownTimer;

        public MainViewModel(
            INavigationService navigationService,
            ISettingsService settingsService,
            IConfigurationService configurationService,
            IApiClient apiClient,
            IOrganizationApiService organizationApiService,
            IAuthSessionService authSessionService,
            IOrgContext orgContext,
            IAuthService authService,
            ISupportSessionApiService supportSessionApiService,
            IDialogService dialogService,
            ProfileMenuViewModel profileMenu,
            IOrganisationService? organisationService = null)
        {
            _navigationService = navigationService;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _organizationApiService = organizationApiService ?? throw new ArgumentNullException(nameof(organizationApiService));
            _authSessionService = authSessionService ?? throw new ArgumentNullException(nameof(authSessionService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _supportSessionApiService = supportSessionApiService ?? throw new ArgumentNullException(nameof(supportSessionApiService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _profileMenu = profileMenu ?? throw new ArgumentNullException(nameof(profileMenu));
            _organisationService = organisationService;

            _authService.StateChanged += OnAuthStateChanged;
            _orgContext.CurrentOrgDetailsChanged += OnCurrentOrgDetailsChanged;
            _orgContext.CurrentOrgChanged += OnCurrentOrgChangedForSupportBanner;

            EndActiveSupportSessionCommand = new MVVM.Commands.RelayCommand(async _ => await EndActiveSupportSessionAsync(), _ => !_isEndingSupportSession && _orgContext.ActiveSupportSession != null);

            CurrentOrgs = new ObservableCollection<OrgSummary>();

            // Initialize navigation items
            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Title = "Dashboard", Icon = MaterialIconKind.ViewDashboardOutline, ViewModelType = typeof(DashboardViewModel) },
                new NavigationItem { Title = "Houses", Icon = MaterialIconKind.HomeCityOutline, ViewModelType = typeof(HousesViewModel) },
                new NavigationItem { Title = "Tenants", Icon = MaterialIconKind.AccountGroupOutline, ViewModelType = typeof(TenantsViewModel) },
                new NavigationItem { Title = "Rent & Payments", Icon = MaterialIconKind.CashMultiple, ViewModelType = typeof(RentPaymentsViewModel) },
                new NavigationItem { Title = "Expenses", Icon = MaterialIconKind.CreditCardOutline, ViewModelType = typeof(ExpensesViewModel) },
                new NavigationItem { Title = "Documents", Icon = MaterialIconKind.FileDocumentOutline, ViewModelType = typeof(DocumentsViewModel) },
                new NavigationItem { Title = "Notifications", Icon = MaterialIconKind.BellOutline, ViewModelType = typeof(NotificationsViewModel) },
                new NavigationItem { Title = "Organisation", Icon = MaterialIconKind.Domain, ViewModelType = typeof(OrganisationViewModel) },
                new NavigationItem { Title = "Audit Log", Icon = MaterialIconKind.ClipboardTextClockOutline, ViewModelType = typeof(AuditLogViewModel) },
                new NavigationItem { Title = "Account", Icon = MaterialIconKind.AccountOutline, ViewModelType = typeof(AccountViewModel) },
                new NavigationItem { Title = "Settings", Icon = MaterialIconKind.CogOutline, ViewModelType = typeof(SettingsViewModel) }
            };

            // Subscribe to navigation changes
            _navigationHandler = (s, vm) =>
            {
                CurrentViewModel = vm;
                // The nav sidebar must be hidden on every pre-authenticated/pre-org-selection
                // screen, not just OnboardingViewModel -- computed here as the single source of
                // truth on every navigation, rather than relying on each call site to separately
                // remember to also set IsOnboardingMode = true right after navigating (which is
                // exactly what SignInViewModel's "Forgot password"/"Back to login" and
                // OnboardingViewModel's reset-success paths missed, leaving the sidebar visible
                // and clickable while signed out).
                IsOnboardingMode = vm is OnboardingViewModel || vm is SignInViewModel || vm is SetupRequiredViewModel;
                _ = RefreshCurrentOrganizationLabelAsync();
                // Update SelectedNavigationItem to match the current view model
                if (vm != null && NavigationItems != null)
                {
                    var matchingItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == vm.GetType());
                    if (matchingItem != null && matchingItem != _selectedNavigationItem)
                    {
                        _selectedNavigationItem = matchingItem;
                        OnPropertyChanged(nameof(SelectedNavigationItem));
                    }
                }

                if (IsOnboardingMode)
                {
                    _selectedNavigationItem = null;
                    OnPropertyChanged(nameof(SelectedNavigationItem));
                }
            };
            _navigationService.CurrentViewModelChanged += _navigationHandler;

            // Initialize collapse command
            ToggleNavigationCommand = new MVVM.Commands.RelayCommand(_ => IsNavigationCollapsed = !IsNavigationCollapsed);
            SwitchOrganizationCommand = new MVVM.Commands.RelayCommand(_ => NavigateToOnboarding());
            SignOutCommand = new MVVM.Commands.RelayCommand(async _ => await _authService.SignOutAsync().ConfigureAwait(true));
            // Quick org switch from the sidebar footer's Flyout -- distinct from
            // SwitchOrganizationCommand above, which navigates away to the full onboarding/org-setup
            // flow. This one stays on the current page and just re-selects CurrentOrgId.
            SwitchToOrgCommand = new MVVM.Commands.RelayCommand<OrgSummary>(
                async org => { if (org != null) await _orgContext.SetCurrentOrgAsync(org.Id).ConfigureAwait(true); },
                org => org != null && org.Id != _orgContext.CurrentOrgId);

            // Initialize organization context before navigating
            _ = InitializeOrganizationContextAsync();
            _ = RefreshCurrentOrganizationLabelAsync();
        }

        private void OnAuthStateChanged(object? sender, AuthStateChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!e.IsSignedIn)
                {
                    _lastDisplayedOrgId = null;
                    // Remove the admin-only nav item so a different, non-admin user signing in
                    // afterward doesn't see a stale "Support Mode" entry left over from this session.
                    var supportItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(SupportModeViewModel));
                    if (supportItem != null)
                        NavigationItems.Remove(supportItem);
                    _navigationService.NavigateTo<SignInViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Sign in)";
                    return;
                }
                _ = ApplyPostSignInAsync();
            });
        }

        /// <summary>
        /// Idempotently adds the "Support Mode" nav item for platform admins (checked after
        /// IOrgContext.RefreshAsync resolves IsPlatformAdmin), and removes it if the signed-in
        /// account is not an admin (e.g. a non-admin signed in without an intervening sign-out
        /// re-using the same MainViewModel instance, since it's a singleton) -- OR while a Support
        /// Session is currently active: the red banner is the only way to end it while acting inside
        /// another org's data, so the nav item (which would let the admin browse for and start a
        /// second, overlapping session) is hidden until that one ends. Called again on every
        /// CurrentOrgChanged (see OnCurrentOrgChangedForSupportBanner) so it reacts immediately to
        /// entering/exiting a session, not just at sign-in.
        /// </summary>
        private void EnsureAdminNavItems()
        {
            var supportItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(SupportModeViewModel));
            if (_orgContext.IsPlatformAdmin && _orgContext.ActiveSupportSession == null)
            {
                if (supportItem == null)
                {
                    NavigationItems.Add(new NavigationItem { Title = "Support Mode", Icon = MaterialIconKind.Lifebuoy, ViewModelType = typeof(SupportModeViewModel) });
                }
            }
            else if (supportItem != null)
            {
                NavigationItems.Remove(supportItem);
            }
        }

        private void OnCurrentOrgDetailsChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _ = RefreshCurrentOrganizationLabelAsync(force: true);
                SyncOrgDisplayState();
            });
        }

        private async System.Threading.Tasks.Task ApplyPostSignInAsync()
        {
            try
            {
                // Use the new account's token for all subsequent API calls (fixes "another account still shows old dashboard").
                _apiClient.ApplyAuthState();
                await _orgContext.RefreshAsync().ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(EnsureAdminNavItems);
                await Dispatcher.UIThread.InvokeAsync(SyncOrgDisplayState);
                // A platform admin needs no org memberships of their own -- Support Mode is the whole
                // point of that account. Only force SetupRequired on a non-admin with no orgs.
                if ((_orgContext.Orgs.Count == 0 && !_orgContext.IsPlatformAdmin) || (!_orgContext.CurrentOrgId.HasValue && _orgContext.Orgs.Count > 0))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _navigationService.NavigateTo<SetupRequiredViewModel>();
                        IsOnboardingMode = true;
                        CurrentOrganizationName = "(Select organization)";
                    });
                    return;
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _ = RefreshCurrentOrganizationLabelAsync(force: true);
                    _navigationService.NavigateTo<DashboardViewModel>();
                    _ = LoadAppStartPageAndNavigateAsync();
                });
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _navigationService.NavigateTo<SetupRequiredViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Select organization)";
                });
            }
        }

        private async System.Threading.Tasks.Task InitializeOrganizationContextAsync()
        {
            try
            {
                var hasSession = await _authService.HasValidSessionAsync().ConfigureAwait(true);
                if (!hasSession)
                {
                    _navigationService.NavigateTo<SignInViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Sign in)";
                    return;
                }

                // Ensure token is in session and ApiClient has it before first API call (avoids 401 on startup).
                _ = await _authService.GetAccessTokenAsync().ConfigureAwait(true);
                _apiClient.ApplyAuthState();

                await _orgContext.RefreshAsync().ConfigureAwait(true);
                EnsureAdminNavItems();
                // Pushes what RefreshAsync just loaded into CurrentOrgRole/CurrentOrgs -- see
                // SyncOrgDisplayState's doc comment for why IOrgContext can't be bound to directly.
                SyncOrgDisplayState();

                // Decision based on org count and current selection only. Do NOT treat CurrentOrgId == null as "no orgs exist".
                // A platform admin needs no org memberships of their own -- Support Mode is the whole
                // point of that account -- so zero orgs alone must not force SetupRequired for them.
                if (_orgContext.Orgs.Count == 0 && !_orgContext.IsPlatformAdmin)
                {
                    _navigationService.NavigateTo<SetupRequiredViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Select organization)";
                    return;
                }
                if (!_orgContext.CurrentOrgId.HasValue && _orgContext.Orgs.Count > 0)
                {
                    // Orgs exist but none selected (e.g. multiple orgs, user must choose). Show Choose Org.
                    _navigationService.NavigateTo<SetupRequiredViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Select organization)";
                    return;
                }
                if (_orgContext.RequiresProfile)
                {
                    // Profile setup can be done from dashboard; still enter.
                }

                _ = RefreshCurrentOrganizationLabelAsync(force: true);
                NavigateToDashboard();
                _ = LoadAppStartPageAndNavigateAsync();
            }
            catch
            {
                // e.g. network error or 401: show setup so user can tap Refresh instead of assuming no orgs.
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _navigationService.NavigateTo<SetupRequiredViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Select organization)";
                });
            }
        }

        private async System.Threading.Tasks.Task LoadAppStartPageAndNavigateAsync()
        {
            try
            {
                var page = await _settingsService.GetSettingAsync("AppStartPage", "Dashboard") ?? "Dashboard";
                var v = (page ?? "").Trim();
                if (string.Equals(v, "Houses", StringComparison.OrdinalIgnoreCase))
                    NavigateToHouses();
                else if (string.Equals(v, "Rent", StringComparison.OrdinalIgnoreCase))
                    NavigateToRentPayments();
            }
            catch { /* ignore */ }
        }

        /// <summary>
        /// Gets the collection of navigation items.
        /// </summary>
        public ObservableCollection<NavigationItem> NavigationItems { get; }

        /// <summary>
        /// Gets or sets whether the navigation panel is collapsed.
        /// </summary>
        public bool IsNavigationCollapsed
        {
            get => _isNavigationCollapsed;
            set
            {
                if (SetProperty(ref _isNavigationCollapsed, value))
                {
                    OnPropertyChanged(nameof(NavigationColumnWidth));
                }
            }
        }

        public bool IsOnboardingMode
        {
            get => _isOnboardingMode;
            set
            {
                if (SetProperty(ref _isOnboardingMode, value))
                {
                    OnPropertyChanged(nameof(NavigationColumnWidth));
                }
            }
        }

        public GridLength NavigationColumnWidth
            => IsOnboardingMode ? new GridLength(0) : (IsNavigationCollapsed ? new GridLength(60) : new GridLength(200));

        public string CurrentOrganizationName
        {
            get => _currentOrganizationName;
            set => SetProperty(ref _currentOrganizationName, value);
        }

        /// <summary>Role for the current org, e.g. "Landlord" -- flat and properly notified for
        /// SidebarProfileFooter's org switcher. IOrgContext itself has no INotifyPropertyChanged
        /// (Orgs/CurrentOrg are plain reads over a private field), so a binding straight through
        /// {Binding OrgContext.CurrentOrg.Role} only ever evaluates once: raising PropertyChanged
        /// for a container property doesn't force Avalonia to re-walk a nested path when that
        /// property's own resolved value is still the same object reference, which OrgContext
        /// always is here (confirmed with a headless render test -- the org name silently never
        /// appeared after RefreshAsync populated it). SyncOrgDisplayState() keeps this and
        /// CurrentOrgs in sync with _orgContext at every point that already updates
        /// CurrentOrganizationName the same way.</summary>
        public string CurrentOrgRole
        {
            get => _currentOrgRole;
            private set => SetProperty(ref _currentOrgRole, value);
        }

        /// <summary>All orgs the signed-in user belongs to, for the sidebar's org-switcher Flyout.
        /// See CurrentOrgRole's doc comment for why this can't just be a read-through property.</summary>
        public ObservableCollection<OrgSummary> CurrentOrgs { get; }

        private void SyncOrgDisplayState()
        {
            CurrentOrgRole = _orgContext.CurrentOrg?.Role ?? string.Empty;
            CurrentOrgs.Clear();
            foreach (var org in _orgContext.Orgs)
                CurrentOrgs.Add(org);
        }

        /// <summary>Read-through for SidebarProfileFooter's user row -- the same app-wide singleton
        /// the Dashboard header's avatar button already opens, not a second instance.</summary>
        public ProfileMenuViewModel ProfileMenu => _profileMenu;

        /// <summary>Quick org switch from the sidebar footer -- stays on the current page. See
        /// SwitchOrganizationCommand for the full onboarding/org-setup navigation instead.</summary>
        public MVVM.Commands.RelayCommand<OrgSummary> SwitchToOrgCommand { get; }

        /// <summary>Whether the signed-in admin is currently acting inside an org entered via
        /// Support Mode -- drives the always-visible red banner so it's obvious from any tab,
        /// not just the Support Mode screen.</summary>
        public bool IsInSupportSession => _orgContext.ActiveSupportSession != null;
        public string SupportSessionOrgName => _orgContext.ActiveSupportSession?.OrganizationName ?? string.Empty;

        /// <summary>Live "in Xm" / "in Xh Ym" countdown, not a fixed clock time -- ticked by
        /// _supportSessionCountdownTimer so the banner visibly counts down instead of looking stuck.</summary>
        public string SupportSessionExpiresDisplay
        {
            get
            {
                var active = _orgContext.ActiveSupportSession;
                if (active == null)
                    return string.Empty;
                var remaining = active.ExpiresAtUtc - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    return "expired";
                return remaining.TotalHours >= 1
                    ? $"in {(int)remaining.TotalHours}h {remaining.Minutes}m"
                    : $"in {Math.Max(1, (int)remaining.TotalMinutes)}m";
            }
        }

        public MVVM.Commands.RelayCommand EndActiveSupportSessionCommand { get; }

        private void OnCurrentOrgChangedForSupportBanner(object? sender, CurrentOrgChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                EnsureAdminNavItems();
                OnPropertyChanged(nameof(IsInSupportSession));
                OnPropertyChanged(nameof(SupportSessionOrgName));
                OnPropertyChanged(nameof(SupportSessionExpiresDisplay));
                // Same reason as InitializeOrganizationContextAsync/ApplyPostSignInAsync -- fires on
                // every explicit org switch (SwitchToOrgCommand) and support-session enter/exit, so
                // the sidebar's org name/role stay in sync with whatever org is now current.
                SyncOrgDisplayState();
                EndActiveSupportSessionCommand.RaiseCanExecuteChanged();

                if (IsInSupportSession)
                {
                    if (_supportSessionCountdownTimer == null)
                    {
                        _supportSessionCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                        _supportSessionCountdownTimer.Tick += (_, _) => OnPropertyChanged(nameof(SupportSessionExpiresDisplay));
                    }
                    _supportSessionCountdownTimer.Start();
                }
                else
                {
                    _supportSessionCountdownTimer?.Stop();
                }
            });
        }

        private async Task EndActiveSupportSessionAsync()
        {
            var active = _orgContext.ActiveSupportSession;
            if (active == null || _isEndingSupportSession)
                return;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                $"End the support session on \"{active.OrganizationName}\"?",
                "End support session");
            if (!confirmed)
                return;

            _isEndingSupportSession = true;
            EndActiveSupportSessionCommand.RaiseCanExecuteChanged();
            try
            {
                await _supportSessionApiService.EndSessionAsync(active.SessionId).ConfigureAwait(true);
                await _orgContext.ExitSupportOrgAsync().ConfigureAwait(true);
                NavigateToDashboard();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync($"Could not end support session: {ex.Message}", "Error");
            }
            finally
            {
                _isEndingSupportSession = false;
                EndActiveSupportSessionCommand.RaiseCanExecuteChanged();
            }
        }

        private void NavigateToOnboarding()
        {
            _navigationService.NavigateTo<OnboardingViewModel>();
            IsOnboardingMode = true;
            CurrentOrganizationName = "(Select organization)";
        }

        private async Task RefreshCurrentOrganizationLabelAsync(bool force = false)
        {
            try
            {
                var orgId = _apiClient.CurrentOrgId;
                if (!orgId.HasValue)
                {
                    _lastDisplayedOrgId = null;
                    CurrentOrganizationName = "(No org selected)";
                    return;
                }

                if (!force && _lastDisplayedOrgId == orgId.Value && !string.IsNullOrWhiteSpace(CurrentOrganizationName))
                    return;

                var org = await _organizationApiService.GetOrganizationAsync(orgId.Value);
                CurrentOrganizationName = org.Name;
                _lastDisplayedOrgId = orgId.Value;
            }
            catch
            {
                if (_apiClient.CurrentOrgId.HasValue)
                {
                    var orgId = _apiClient.CurrentOrgId.Value;
                    if (_organisationService != null)
                    {
                        try
                        {
                            var localOrg = await _organisationService.GetOrganisationAsync(orgId);
                            CurrentOrganizationName = localOrg?.Name ?? orgId.ToString();
                        }
                        catch
                        {
                            CurrentOrganizationName = orgId.ToString();
                        }
                    }
                    else
                    {
                        CurrentOrganizationName = orgId.ToString();
                    }
                    _lastDisplayedOrgId = _apiClient.CurrentOrgId;
                }
                else
                {
                    CurrentOrganizationName = "(No org selected)";
                    _lastDisplayedOrgId = null;
                }
            }
        }

        /// <summary>
        /// Gets the command to toggle navigation collapse state.
        /// </summary>
        public MVVM.Commands.RelayCommand ToggleNavigationCommand { get; }

        /// <summary>
        /// Gets the command to switch organization.
        /// </summary>
        public MVVM.Commands.RelayCommand SwitchOrganizationCommand { get; }

        /// <summary>
        /// Gets the command to sign out.
        /// </summary>
        public MVVM.Commands.RelayCommand SignOutCommand { get; }

        /// <summary>
        /// Gets or sets the current ViewModel displayed in the content area.
        /// </summary>
        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        /// <summary>
        /// Gets or sets the selected navigation item.
        /// </summary>
        public NavigationItem? SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                if (SetProperty(ref _selectedNavigationItem, value) && value != null)
                {
                    Navigate(value);
                }
            }
        }

        private void Navigate(NavigationItem? item)
        {
            if (item?.ViewModelType == null) return;

            if (item.ViewModelType == typeof(DashboardViewModel))
                NavigateToDashboard();
            else if (item.ViewModelType == typeof(HousesViewModel))
                NavigateToHouses();
            else if (item.ViewModelType == typeof(TenantsViewModel))
                NavigateToTenants();
            else if (item.ViewModelType == typeof(RentPaymentsViewModel))
                NavigateToRentPayments();
            else if (item.ViewModelType == typeof(DocumentsViewModel))
                NavigateToDocuments();
            else if (item.ViewModelType == typeof(ExpensesViewModel))
                NavigateToExpenses();
            else if (item.ViewModelType == typeof(NotificationsViewModel))
                NavigateToNotifications();
            else if (item.ViewModelType == typeof(OrganisationViewModel))
                NavigateToOrganisation();
            else if (item.ViewModelType == typeof(AuditLogViewModel))
                NavigateToAuditLog();
            else if (item.ViewModelType == typeof(SupportModeViewModel))
                NavigateToSupportMode();
            else if (item.ViewModelType == typeof(AccountViewModel))
                NavigateToAccount();
            else if (item.ViewModelType == typeof(SettingsViewModel))
                NavigateToSettings();
        }

        private void NavigateToDashboard()
        {
            _navigationService.NavigateTo<DashboardViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(DashboardViewModel));
        }

        private void NavigateToHouses()
        {
            _navigationService.NavigateTo<HousesViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(HousesViewModel));
        }

        private void NavigateToTenants()
        {
            _navigationService.NavigateTo<TenantsViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(TenantsViewModel));
        }

        private void NavigateToRentPayments()
        {
            _navigationService.NavigateTo<RentPaymentsViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(RentPaymentsViewModel));
        }

        private void NavigateToDocuments()
        {
            _navigationService.NavigateTo<DocumentsViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(DocumentsViewModel));
        }

        private void NavigateToExpenses()
        {
            _navigationService.NavigateTo<ExpensesViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(ExpensesViewModel));
        }

        private void NavigateToNotifications()
        {
            _navigationService.NavigateTo<NotificationsViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(NotificationsViewModel));
        }

        private void NavigateToOrganisation()
        {
            _navigationService.NavigateTo<OrganisationViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(OrganisationViewModel));
        }

        private void NavigateToAuditLog()
        {
            _navigationService.NavigateTo<AuditLogViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(AuditLogViewModel));
        }

        private void NavigateToSupportMode()
        {
            _navigationService.NavigateTo<SupportModeViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(SupportModeViewModel));
        }

        private void NavigateToAccount()
        {
            _navigationService.NavigateTo<AccountViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(AccountViewModel));
        }

        private void NavigateToSettings()
        {
            _navigationService.NavigateTo<SettingsViewModel>();
            SelectedNavigationItem = NavigationItems.FirstOrDefault(m => m.ViewModelType == typeof(SettingsViewModel));
        }

        /// <summary>
        /// Cleanup method to unsubscribe from events.
        /// </summary>
        public void Cleanup()
        {
            if (_navigationHandler != null && _navigationService != null)
            {
                _navigationService.CurrentViewModelChanged -= _navigationHandler;
                _navigationHandler = null;
            }
        }
    }
}
