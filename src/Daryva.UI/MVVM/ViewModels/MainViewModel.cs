using System.Collections.ObjectModel;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Daryva.Services.Auth;
using Avalonia.Controls;
using Avalonia.Threading;

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
        private BaseViewModel? _currentViewModel;
        private NavigationItem? _selectedNavigationItem;
        private EventHandler<BaseViewModel?>? _navigationHandler;
        private bool _isNavigationCollapsed;
        private bool _isOnboardingMode;
        private string _currentOrganizationName = "(No org selected)";
        private Guid? _lastDisplayedOrgId;

        public MainViewModel(
            INavigationService navigationService,
            ISettingsService settingsService,
            IConfigurationService configurationService,
            IApiClient apiClient,
            IOrganizationApiService organizationApiService,
            IAuthSessionService authSessionService,
            IOrgContext orgContext,
            IAuthService authService,
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
            _organisationService = organisationService;

            _authService.StateChanged += OnAuthStateChanged;
            _orgContext.CurrentOrgDetailsChanged += OnCurrentOrgDetailsChanged;

            // Initialize navigation items
            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Title = "Dashboard", Icon = "🏠", ViewModelType = typeof(DashboardViewModel) },
                new NavigationItem { Title = "Houses", Icon = "🏘️", ViewModelType = typeof(HousesViewModel) },
                new NavigationItem { Title = "Tenants", Icon = "👥", ViewModelType = typeof(TenantsViewModel) },
                new NavigationItem { Title = "Rent & Payments", Icon = "💰", ViewModelType = typeof(RentPaymentsViewModel) },
                new NavigationItem { Title = "Expenses", Icon = "💳", ViewModelType = typeof(ExpensesViewModel) },
                new NavigationItem { Title = "Documents", Icon = "📄", ViewModelType = typeof(DocumentsViewModel) },
                new NavigationItem { Title = "Notifications", Icon = "🔔", ViewModelType = typeof(NotificationsViewModel) },
                new NavigationItem { Title = "Organisation", Icon = "🏢", ViewModelType = typeof(OrganisationViewModel) },
                new NavigationItem { Title = "Account", Icon = "👤", ViewModelType = typeof(AccountViewModel) },
                new NavigationItem { Title = "Settings", Icon = "⚙️", ViewModelType = typeof(SettingsViewModel) }
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
                    _navigationService.NavigateTo<SignInViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Sign in)";
                    return;
                }
                _ = ApplyPostSignInAsync();
            });
        }

        private void OnCurrentOrgDetailsChanged(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(() => _ = RefreshCurrentOrganizationLabelAsync(force: true));
        }

        private async System.Threading.Tasks.Task ApplyPostSignInAsync()
        {
            try
            {
                // Use the new account's token for all subsequent API calls (fixes "another account still shows old dashboard").
                _apiClient.ApplyAuthState();
                await _orgContext.RefreshAsync().ConfigureAwait(false);
                if (_orgContext.Orgs.Count == 0 || !_orgContext.CurrentOrgId.HasValue)
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

                // Decision based on org count and current selection only. Do NOT treat CurrentOrgId == null as "no orgs exist".
                if (_orgContext.Orgs.Count == 0)
                {
                    _navigationService.NavigateTo<SetupRequiredViewModel>();
                    IsOnboardingMode = true;
                    CurrentOrganizationName = "(Select organization)";
                    return;
                }
                if (!_orgContext.CurrentOrgId.HasValue)
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
