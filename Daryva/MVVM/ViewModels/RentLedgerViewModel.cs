using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class RentLedgerViewModel : BaseViewModel
    {
        private readonly IPaymentService _paymentService;
        private readonly IHouseService _houseService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly IExportService _exportService;
        private readonly ISettingsService _settingsService;

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
            INavigationService navigationService,
            IExportService exportService,
            ISettingsService settingsService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            LedgerRows = new ObservableCollection<RentLedgerRowViewModel>();
            DepositLedgerRows = new ObservableCollection<DepositLedgerRowViewModel>();
            Houses = new ObservableCollection<House>();
            StatusFilterOptions = new ObservableCollection<string> { "All", "Paid", "PartPaid", "Unpaid", "Overdue" };
            
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
            ExportLedgerCommand = new RelayCommand(async _ => await ExportLedgerAsync());

            LoadHousesCommand.Execute(null);
            LoadLedgerCommand.Execute(null);
            LoadDepositLedgerCommand.Execute(null);
        }

        public ICommand LoadLedgerCommand { get; }
        public ICommand LoadDepositLedgerCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand RecordPaymentCommand { get; }
        public ICommand ExpandRowCommand { get; }
        public ICommand ExportLedgerCommand { get; }

        public ObservableCollection<RentLedgerRowViewModel> LedgerRows { get; }
        public ObservableCollection<DepositLedgerRowViewModel> DepositLedgerRows { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<string> StatusFilterOptions { get; }

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

        private string GetHouseDisplayName()
        {
            if (SelectedHouseId == null || SelectedHouseId == 0)
            {
                return "All Houses";
            }

            var house = Houses.FirstOrDefault(h => h.HouseId == SelectedHouseId);
            return house != null ? house.AddressLine1 : "House";
        }

        private async Task ExportLedgerAsync()
        {
            try
            {
                var defaultFile = $"Daryva_{GetHouseDisplayName().Replace(" ", "")}_{SelectedMonthDisplay.Replace(" ", "")}_RentDeposit.xlsx";
                var path = _dialogService.ShowSaveFileDialog(defaultFile, "Excel Files|*.xlsx", "Save Rent & Deposit Ledger");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                decimal rentGivenToLandlord = 0m;
                var input = await _dialogService.ShowInputDialogAsync("Enter Rent Given to Landlord (leave blank for 0):", "Rent Settlement", "0");
                if (!string.IsNullOrWhiteSpace(input))
                {
                    decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out rentGivenToLandlord);
                }

                var rows = new List<LedgerRowModel>();
                foreach (var rentRow in LedgerRows)
                {
                    var depositMatch = DepositLedgerRows.FirstOrDefault(d => string.Equals(d.TenantName, rentRow.TenantName, StringComparison.OrdinalIgnoreCase));
                    var rentCollector = rentRow.PaymentsForThisMonth.FirstOrDefault()?.CollectedBy ?? "Nil";
                    var depositCollector = depositMatch?.Payments.FirstOrDefault()?.CollectedBy ?? "Nil";
                    rows.Add(new LedgerRowModel
                    {
                        TenantName = rentRow.TenantName,
                        RentAmount = rentRow.AmountDue,
                        RentCollectedBy = rentCollector,
                        DepositAmount = depositMatch?.AmountPaid ?? 0m,
                        DepositCollectedBy = depositCollector
                    });
                }

                var model = new LedgerExportModel
                {
                    HouseName = GetHouseDisplayName(),
                    MonthYearDisplay = SelectedMonthDisplay,
                    Rows = rows,
                    RentGivenToLandlord = rentGivenToLandlord,
                    Collectors = new List<string>(),
                    OutputPath = path
                };

                await _exportService.ExportRentDepositLedgerAsync(model, CancellationToken.None);
                _dialogService.ShowMessage($"Exported to {path}", "Export Complete");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error exporting ledger: {ex.Message}", "Error");
            }
        }

        private async Task LoadHousesAsync()
        {
            try
            {
                // Run database query on background thread
                var houses = await Task.Run(async () => await _houseService.GetAllHousesAsync()).ConfigureAwait(false);
                
                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
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

                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                DateTimeFormatProvider.DateFormat = dateFormat;

                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LedgerRows.Clear();
                    foreach (var row in rows)
                    {
                        row.DueDateDisplay = DateTimeFormatProvider.FormatDate(row.DueDate);
                        LedgerRows.Add(row);
                    }
                });
                
                // Also load deposit ledger
                await LoadDepositLedgerAsync();
            }
            catch (Exception ex)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
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
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
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
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
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
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow 
                    : null;
                if (mainWindow != null)
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                    dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    dialog.Show();
                }
                // Reload ledger after payment is recorded
                LoadLedgerCommand.Execute(null);
                
                // Refresh dashboard if it's currently displayed
                RefreshDashboardIfActive();
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
