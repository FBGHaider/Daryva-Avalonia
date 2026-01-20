using System.Collections.ObjectModel;
using System.Windows.Input;
using Daryva.MVVM.Commands;
using Daryva.Services.Navigation;

namespace Daryva.MVVM.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private BaseViewModel? _currentViewModel;
        private MenuItemViewModel? _selectedMenuItem;
        private EventHandler<BaseViewModel?>? _navigationHandler;

        public MainWindowViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            
            // Subscribe to navigation changes
            _navigationHandler = (s, vm) =>
            {
                CurrentViewModel = vm;
                // Update SelectedMenuItem to match the current view model
                if (vm != null && MenuItems != null)
                {
                    var matchingMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == vm.GetType());
                    if (matchingMenuItem != null && matchingMenuItem != _selectedMenuItem)
                    {
                        // Set without triggering navigation to avoid circular calls
                        _selectedMenuItem = matchingMenuItem;
                        OnPropertyChanged(nameof(SelectedMenuItem));
                    }
                }
            };
            _navigationService.CurrentViewModelChanged += _navigationHandler;
            
            // Initialize menu items
            MenuItems = new ObservableCollection<MenuItemViewModel>
            {
                new MenuItemViewModel { Title = "Dashboard", Icon = "🏠", ViewModelType = typeof(DashboardViewModel) },
                new MenuItemViewModel { Title = "Houses", Icon = "🏘️", ViewModelType = typeof(HousesViewModel) },
                new MenuItemViewModel { Title = "Tenants", Icon = "👥", ViewModelType = typeof(TenantsViewModel) },
                new MenuItemViewModel { Title = "Rent & Payments", Icon = "💰", ViewModelType = typeof(RentPaymentsViewModel) },
                new MenuItemViewModel { Title = "Documents", Icon = "📄", ViewModelType = typeof(DocumentsViewModel) },
                new MenuItemViewModel { Title = "Expenses", Icon = "💳", ViewModelType = typeof(ExpensesViewModel) },
                new MenuItemViewModel { Title = "Notifications", Icon = "🔔", ViewModelType = typeof(NotificationsViewModel) },
                new MenuItemViewModel { Title = "Settings", Icon = "⚙️", ViewModelType = typeof(SettingsViewModel) }
            };

            // Navigate to Dashboard by default
            NavigateToDashboard();
        }

        public ObservableCollection<MenuItemViewModel> MenuItems { get; }

        public BaseViewModel? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public MenuItemViewModel? SelectedMenuItem
        {
            get => _selectedMenuItem;
            set
            {
                if (SetProperty(ref _selectedMenuItem, value) && value != null)
                {
                    Navigate(value);
                }
            }
        }

        private void Navigate(MenuItemViewModel? menuItem)
        {
            if (menuItem?.ViewModelType == null) return;

            if (menuItem.ViewModelType == typeof(DashboardViewModel))
                NavigateToDashboard();
            else if (menuItem.ViewModelType == typeof(HousesViewModel))
                NavigateToHouses();
            else if (menuItem.ViewModelType == typeof(TenantsViewModel))
                NavigateToTenants();
            else if (menuItem.ViewModelType == typeof(RentPaymentsViewModel))
                NavigateToRentPayments();
            else if (menuItem.ViewModelType == typeof(DocumentsViewModel))
                NavigateToDocuments();
            else if (menuItem.ViewModelType == typeof(ExpensesViewModel))
                NavigateToExpenses();
            else if (menuItem.ViewModelType == typeof(NotificationsViewModel))
                NavigateToNotifications();
            else if (menuItem.ViewModelType == typeof(SettingsViewModel))
                NavigateToSettings();
        }

        private void NavigateToDashboard()
        {
            _navigationService.NavigateTo<DashboardViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(DashboardViewModel));
        }
        private void NavigateToHouses()
        {
            _navigationService.NavigateTo<HousesViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(HousesViewModel));
        }
        private void NavigateToTenants()
        {
            _navigationService.NavigateTo<TenantsViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(TenantsViewModel));
        }
        private void NavigateToRentPayments()
        {
            _navigationService.NavigateTo<RentPaymentsViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(RentPaymentsViewModel));
        }
        private void NavigateToDocuments()
        {
            _navigationService.NavigateTo<DocumentsViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(DocumentsViewModel));
        }
        private void NavigateToExpenses()
        {
            _navigationService.NavigateTo<ExpensesViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(ExpensesViewModel));
        }
        private void NavigateToNotifications()
        {
            _navigationService.NavigateTo<NotificationsViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(NotificationsViewModel));
        }
        private void NavigateToSettings()
        {
            _navigationService.NavigateTo<SettingsViewModel>();
            SelectedMenuItem = MenuItems.FirstOrDefault(m => m.ViewModelType == typeof(SettingsViewModel));
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

    public class MenuItemViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public Type? ViewModelType { get; set; }
    }
}
