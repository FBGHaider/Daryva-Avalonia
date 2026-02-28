using System.Collections.ObjectModel;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Navigation;
using Avalonia.Controls;

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
            IOrganisationService? organisationService = null)
        {
            _navigationService = navigationService;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _organizationApiService = organizationApiService ?? throw new ArgumentNullException(nameof(organizationApiService));
            _authSessionService = authSessionService ?? throw new ArgumentNullException(nameof(authSessionService));
            _organisationService = organisationService;

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
                IsOnboardingMode = vm is OnboardingViewModel;
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

            // Initialize organization context before navigating
            _ = InitializeOrganizationContextAsync();
            _ = RefreshCurrentOrganizationLabelAsync();
        }

        private async System.Threading.Tasks.Task InitializeOrganizationContextAsync()
        {
            try
            {
                if (!_authSessionService.IsAuthenticated)
                {
                    NavigateToOnboarding();
                    return;
                }

                // Try to load organizations and auto-select
                var orgs = await _organizationApiService.GetUserOrganizationsAsync();

                var preferredOrgRaw = _configurationService.GetValue("ApiCurrentOrgId");
                Guid? preferredOrgId = Guid.TryParse(preferredOrgRaw, out var parsedOrgId) ? parsedOrgId : null;
                var preferredOrg = preferredOrgId.HasValue
                    ? orgs.FirstOrDefault(o => o.Id == preferredOrgId.Value)
                    : null;
                
                if (preferredOrg != null)
                {
                    _apiClient.SetCurrentOrgId(preferredOrg.Id);
                    _ = RefreshCurrentOrganizationLabelAsync(force: true);
                    NavigateToDashboard();
                    _ = LoadAppStartPageAndNavigateAsync();
                }
                else if (orgs.Count == 1)
                {
                    // Auto-select single organization
                    _apiClient.SetCurrentOrgId(orgs[0].Id);
                    _ = RefreshCurrentOrganizationLabelAsync(force: true);
                    NavigateToDashboard();
                    _ = LoadAppStartPageAndNavigateAsync();
                }
                else if (orgs.Count > 1)
                {
                    // Multiple orgs - default to first; user can switch from the shell org switch action
                    _apiClient.SetCurrentOrgId(orgs[0].Id);
                    _ = RefreshCurrentOrganizationLabelAsync(force: true);
                    NavigateToDashboard();
                    _ = LoadAppStartPageAndNavigateAsync();
                }
                else
                {
                    // No organizations - onboarding provides create/join flow
                    NavigateToOnboarding();
                }
            }
            catch
            {
                // Invalid token/session or API error -> onboarding
                NavigateToOnboarding();
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
