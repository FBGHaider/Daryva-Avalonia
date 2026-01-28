using System.Collections.ObjectModel;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Navigation;

namespace Daryva.MVVM.ViewModels
{
    /// <summary>
    /// ViewModel for the main window with navigation support.
    /// </summary>
    public class MainViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private readonly ISettingsService _settingsService;
        private BaseViewModel? _currentViewModel;
        private NavigationItem? _selectedNavigationItem;
        private EventHandler<BaseViewModel?>? _navigationHandler;
        private bool _isNavigationCollapsed;

        public MainViewModel(INavigationService navigationService, ISettingsService settingsService)
        {
            _navigationService = navigationService;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

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
                new NavigationItem { Title = "Settings", Icon = "⚙️", ViewModelType = typeof(SettingsViewModel) }
            };

            // Subscribe to navigation changes
            _navigationHandler = (s, vm) =>
            {
                CurrentViewModel = vm;
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
            };
            _navigationService.CurrentViewModelChanged += _navigationHandler;

            NavigateToDashboard();
            _ = LoadAppStartPageAndNavigateAsync();

            // Initialize collapse command
            ToggleNavigationCommand = new MVVM.Commands.RelayCommand(_ => IsNavigationCollapsed = !IsNavigationCollapsed);
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
            set => SetProperty(ref _isNavigationCollapsed, value);
        }

        /// <summary>
        /// Gets the command to toggle navigation collapse state.
        /// </summary>
        public MVVM.Commands.RelayCommand ToggleNavigationCommand { get; }

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
