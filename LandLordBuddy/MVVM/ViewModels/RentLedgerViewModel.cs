using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using LandLordBuddy.MVVM.Commands;
using LandLordBuddy.MVVM.Models;
using LandLordBuddy.Services.Business;
using LandLordBuddy.Services.Data;
using LandLordBuddy.Services.Dialog;
using LandLordBuddy.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace LandLordBuddy.MVVM.ViewModels
{
    public class RentLedgerViewModel : BaseViewModel
    {
        private readonly IPaymentService _paymentService;
        private readonly IHouseService _houseService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;

        private int _selectedYear = DateTime.Now.Year;
        private int _selectedMonth = DateTime.Now.Month;
        private int? _selectedHouseId = 0; // Default to "All Houses"
        private string _statusFilter = "All";
        private string _searchTerm = "";
        private RentLedgerRowViewModel? _selectedRow;

        public RentLedgerViewModel(
            IPaymentService paymentService,
            IHouseService houseService,
            IServiceProvider serviceProvider,
            IDialogService dialogService,
            INavigationService navigationService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            LedgerRows = new ObservableCollection<RentLedgerRowViewModel>();
            DepositLedgerRows = new ObservableCollection<DepositLedgerRowViewModel>();
            Houses = new ObservableCollection<House>();
            
            LoadLedgerCommand = new RelayCommand(async _ => await LoadLedgerAsync());
            LoadDepositLedgerCommand = new RelayCommand(async _ => await LoadDepositLedgerAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            RecordPaymentCommand = new RelayCommand(parameter => 
            {
                // If parameter is provided (from button click), use it; otherwise use SelectedRow
                if (parameter is RentLedgerRowViewModel row)
                {
                    SelectedRow = row;
                }
                RecordPaymentForSelectedRow();
            }, _ => SelectedRow != null);
            ExpandRowCommand = new RelayCommand(_ => ToggleRowExpansion(), _ => SelectedRow != null);
        }

        public ICommand LoadLedgerCommand { get; }
        public ICommand LoadDepositLedgerCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand RecordPaymentCommand { get; }
        public ICommand ExpandRowCommand { get; }

        public ObservableCollection<RentLedgerRowViewModel> LedgerRows { get; }
        public ObservableCollection<DepositLedgerRowViewModel> DepositLedgerRows { get; }
        public ObservableCollection<House> Houses { get; }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                {
                    LoadLedgerCommand.Execute(null);
                }
            }
        }

        public int SelectedMonth
        {
            get => _selectedMonth;
            set
            {
                if (SetProperty(ref _selectedMonth, value))
                {
                    LoadLedgerCommand.Execute(null);
                }
            }
        }

        public int? SelectedHouseId
        {
            get => _selectedHouseId;
            set
            {
                if (SetProperty(ref _selectedHouseId, value))
                {
                    LoadLedgerCommand.Execute(null);
                }
            }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    LoadLedgerCommand.Execute(null);
                }
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    LoadLedgerCommand.Execute(null);
                }
            }
        }

        public RentLedgerRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                {
                    ((RelayCommand)RecordPaymentCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)ExpandRowCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string SelectedMonthDisplay => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");

        private async Task LoadHousesAsync()
        {
            try
            {
                // Run database query on background thread
                var houses = await Task.Run(async () => await _houseService.GetAllHousesAsync()).ConfigureAwait(false);
                
                // Update UI on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Houses.Clear();
                    Houses.Add(new House { HouseId = 0, AddressLine1 = "All Houses" }); // Placeholder for "All"
                    foreach (var house in houses)
                    {
                        Houses.Add(house);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        private async Task LoadLedgerAsync()
        {
            try
            {
                // Convert SelectedHouseId: 0 means "All Houses" (pass null), otherwise pass the actual ID
                int? houseIdFilter = (SelectedHouseId == null || SelectedHouseId == 0) ? null : SelectedHouseId;
                
                // Run database query on background thread
                var rows = await Task.Run(async () => await _paymentService.GetRentLedgerForMonthAsync(
                    SelectedYear,
                    SelectedMonth,
                    houseIdFilter,
                    StatusFilter == "All" ? null : StatusFilter,
                    string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm)).ConfigureAwait(false);

                // Update UI on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    LedgerRows.Clear();
                    foreach (var row in rows)
                    {
                        LedgerRows.Add(row);
                    }
                });
                
                // Also load deposit ledger
                await LoadDepositLedgerAsync();
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dialogService.ShowMessage($"Error loading ledger: {ex.Message}", "Error");
                });
            }
        }

        private async Task LoadDepositLedgerAsync()
        {
            try
            {
                // Convert SelectedHouseId: 0 means "All Houses" (pass null), otherwise pass the actual ID
                int? houseIdFilter = (SelectedHouseId == null || SelectedHouseId == 0) ? null : SelectedHouseId;
                
                // Run database query on background thread
                var rows = await Task.Run(async () => await _paymentService.GetDepositLedgerForMonthAsync(
                    houseIdFilter,
                    StatusFilter == "All" ? null : StatusFilter,
                    string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm)).ConfigureAwait(false);

                // Update UI on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    DepositLedgerRows.Clear();
                    foreach (var row in rows)
                    {
                        DepositLedgerRows.Add(row);
                    }
                });
            }
            catch (Exception ex)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dialogService.ShowMessage($"Error loading deposit ledger: {ex.Message}", "Error");
                });
            }
        }

        private void ToggleRowExpansion()
        {
            if (SelectedRow != null)
            {
                SelectedRow.IsExpanded = !SelectedRow.IsExpanded;
            }
        }

        private void RecordPaymentForSelectedRow()
        {
            if (SelectedRow == null) return;

            try
            {
                var viewModel = _serviceProvider.GetRequiredService<RecordPaymentViewModel>();
                
                // Pre-select the tenancy and month/year from the selected row
                viewModel.SetPreselectedTenancy(SelectedRow.TenancyId, SelectedYear, SelectedMonth);
                
                var dialog = new MVVM.Views.RecordPaymentDialog(viewModel);
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    // Reload ledger after payment is recorded
                    LoadLedgerCommand.Execute(null);
                    
                    // Refresh dashboard if it's currently displayed
                    RefreshDashboardIfActive();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening record payment dialog: {ex.Message}", "Error");
            }
        }

        private void RefreshDashboardIfActive()
        {
            // Notify all DashboardViewModel instances to refresh using static event
            DashboardViewModel.NotifyPaymentDataChanged();
        }
    }
}
