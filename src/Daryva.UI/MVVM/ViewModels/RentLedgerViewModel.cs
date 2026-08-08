using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        private List<HouseLedgerGroupViewModel> _houseGroups = new List<HouseLedgerGroupViewModel>();
        private bool _isLoading;

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
            FlatRentLedgerRows = new ObservableCollection<object>();
            FlatDepositLedgerRows = new ObservableCollection<object>();
            Houses = new ObservableCollection<House>();
            StatusFilterOptions = new ObservableCollection<string> { "All", "Paid", "PartPaid", "Unpaid", "Overdue" };
            InitializeYearOptions();
            
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
            RecordDepositPaymentCommand = new RelayCommand(parameter =>
            {
                if (parameter is DepositLedgerRowViewModel depositRow)
                {
                    RecordDepositPaymentForRow(depositRow);
                }
            });
            ExpandRowCommand = new RelayCommand(_ => ToggleRowExpansion(), _ => SelectedRow != null);
            ToggleHouseExpandedCommand = new RelayCommand(parameter =>
            {
                if (parameter is HouseLedgerGroupViewModel group)
                {
                    group.IsExpanded = !group.IsExpanded;
                    RebuildFlatLists();
                }
            });
            ExportLedgerCommand = new RelayCommand(async _ => await ExportLedgerAsync());
            ClearFiltersCommand = new RelayCommand(_ =>
            {
                SelectedMonth = DateTime.Now.Month;
                SelectedYear = DateTime.Now.Year;
                SelectedHouseId = 0;
                StatusFilter = "All";
                SearchTerm = "";
            });

            LoadHousesCommand.Execute(null);
            LoadLedgerCommand.Execute(null);
            LoadDepositLedgerCommand.Execute(null);
        }

        public ICommand LoadLedgerCommand { get; }
        public ICommand LoadDepositLedgerCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand RecordPaymentCommand { get; }
        public ICommand RecordDepositPaymentCommand { get; }
        public ICommand ExpandRowCommand { get; }
        public ICommand ToggleHouseExpandedCommand { get; }
        public ICommand ExportLedgerCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        public ObservableCollection<RentLedgerRowViewModel> LedgerRows { get; }
        public ObservableCollection<DepositLedgerRowViewModel> DepositLedgerRows { get; }
        /// <summary>Flattened list for UI: HouseLedgerGroupViewModel (header) then RentLedgerRowViewModel (when expanded).</summary>
        public ObservableCollection<object> FlatRentLedgerRows { get; }
        /// <summary>Flattened list for UI: HouseLedgerGroupViewModel (header) then DepositLedgerRowViewModel (when expanded).</summary>
        public ObservableCollection<object> FlatDepositLedgerRows { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<string> StatusFilterOptions { get; }
        public ObservableCollection<int> YearOptions { get; } = new();

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

        /// <summary>Selected item in the rent ledger list (house header or rent row). When set to a RentLedgerRowViewModel, updates SelectedRow.</summary>
        public object? SelectedRentLedgerItem
        {
            get => _selectedRow;
            set
            {
                SelectedRow = value as RentLedgerRowViewModel;
            }
        }

        public string SelectedMonthDisplay => new DateTime(SelectedYear, SelectedMonth, 1).ToString("MMMM yyyy");

        public decimal TotalDue => LedgerRows.Sum(r => r.AmountDue);
        public decimal TotalCollected => LedgerRows.Sum(r => r.AmountPaid);
        public decimal TotalOverdue => LedgerRows.Where(r => string.Equals(r.Status, "Overdue", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Balance);
        public decimal OutstandingBalance => LedgerRows.Sum(r => r.Balance);
        public int OverdueCount => LedgerRows.Count(r => string.Equals(r.Status, "Overdue", StringComparison.OrdinalIgnoreCase));
        public int HouseCountForDisplay => LedgerRows.Select(r => r.HouseId).Distinct().Count();
        public decimal DepositsOutstanding => DepositLedgerRows.Sum(d => d.Balance);

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public bool HasRentLedgerRows => LedgerRows.Count > 0;
        public bool HasDepositLedgerRows => DepositLedgerRows.Count > 0;

        /// <summary>Finance summary for command bar subtitle.</summary>
        public string SubtitleText =>
            $"{SelectedMonthDisplay} • {HouseCountForDisplay} houses • £{TotalDue:N2} due • £{TotalCollected:N2} collected • £{TotalOverdue:N2} overdue";

        private void InitializeYearOptions()
        {
            YearOptions.Clear();
            var currentYear = DateTime.Now.Year;

            // Show a sensible range: current year +/- 5 years
            for (int year = currentYear - 5; year <= currentYear + 5; year++)
            {
                YearOptions.Add(year);
            }

            // Ensure current SelectedYear is in the list
            if (!YearOptions.Contains(SelectedYear))
            {
                YearOptions.Add(SelectedYear);
            }
        }

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

                // Use document storage path from Document settings; fallback to project-relative folder
                var documentPath = await _settingsService.GetSettingAsync("DocumentStoragePath", string.Empty) ?? string.Empty;

                string baseFolder;
                if (!string.IsNullOrWhiteSpace(documentPath))
                {
                    baseFolder = documentPath;
                }
                else
                {
                    // Default: project directory + "RentAndDeposit"
                    var projectRoot = AppContext.BaseDirectory;
                    baseFolder = Path.Combine(projectRoot, "RentAndDeposit");
                }

                if (!Directory.Exists(baseFolder))
                {
                    Directory.CreateDirectory(baseFolder);
                }

                var path = Path.Combine(baseFolder, defaultFile);

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

                string? exportedPath = null;

                await _dialogService.RunWithProgressAsync(
                    "Exporting Rent & Deposit",
                    "Generating Excel file. Please wait...",
                    async () =>
                    {
                        exportedPath = await _exportService.ExportRentDepositLedgerAsync(model, CancellationToken.None);
                    });

                if (!string.IsNullOrWhiteSpace(exportedPath))
                {
                    _dialogService.ShowMessage($"Exported to {exportedPath}", "Export Complete");
                }
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

                    if (SelectedHouseId.HasValue && SelectedHouseId.Value != 0 && !Houses.Any(h => h.HouseId == SelectedHouseId.Value))
                    {
                        SelectedHouseId = 0;
                    }
                });
            }
            catch (Exception ex)
            {
                if (!IsActive)
                {
                    AppLogger.Log("RentLedger", $"Suppressing error dialog for abandoned load (navigated away): {ex.Message}");
                    return;
                }
                _dialogService.ShowMessage($"Error loading houses: {ex.Message}", "Error");
            }
        }

        private async Task LoadLedgerAsync()
        {
            try
            {
                // Repair any rent payments that are unlinked or linked to wrong charge (so past months show Paid correctly)
                try
                {
                    await Task.Run(async () => await _paymentService.RepairRentPaymentChargeLinksAsync()).ConfigureAwait(false);
                }
                catch
                {
                    // Non-critical maintenance step; continue loading ledger data.
                }

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
                    IsLoading = true;
                    SelectedRow = null;
                    LedgerRows.Clear();
                    foreach (var row in rows ?? Enumerable.Empty<RentLedgerRowViewModel>())
                    {
                        if (row == null) continue;
                        row.DueDateDisplay = DateTimeFormatProvider.FormatDate(row.DueDate);
                        LedgerRows.Add(row);
                    }
                });
                
                // Also load deposit ledger
                await LoadDepositLedgerAsync();
                
                // Build house groups and flat lists for expandable house rows
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    BuildHouseGroups();
                    RebuildFlatLists();
                    NotifyLedgerTotalsChanged();
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                if (!IsActive)
                {
                    AppLogger.Log("RentLedger", $"Suppressing error dialog for abandoned load (navigated away): {ex.Message}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
                    return;
                }
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = false;
                    _dialogService.ShowMessage($"Error loading ledger: {ex.Message}", "Error");
                });
            }
        }

        private void NotifyLedgerTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalDue));
            OnPropertyChanged(nameof(TotalCollected));
            OnPropertyChanged(nameof(TotalOverdue));
            OnPropertyChanged(nameof(OutstandingBalance));
            OnPropertyChanged(nameof(OverdueCount));
            OnPropertyChanged(nameof(HouseCountForDisplay));
            OnPropertyChanged(nameof(DepositsOutstanding));
            OnPropertyChanged(nameof(HasRentLedgerRows));
            OnPropertyChanged(nameof(HasDepositLedgerRows));
            OnPropertyChanged(nameof(SubtitleText));
        }

        private void BuildHouseGroups()
        {
            _houseGroups.Clear();
            var rentByHouse = LedgerRows.GroupBy(r => r.HouseId).ToDictionary(g => g.Key, g => g.ToList());
            var depositByHouse = DepositLedgerRows.GroupBy(d => d.HouseId).ToDictionary(g => g.Key, g => g.ToList());
            var allHouseIds = rentByHouse.Keys.Union(depositByHouse.Keys).Distinct().ToList();
            foreach (var hid in allHouseIds.OrderBy(hid =>
            {
                rentByHouse.TryGetValue(hid, out var r);
                depositByHouse.TryGetValue(hid, out var d);
                return (r?.FirstOrDefault()?.HouseAddress ?? d?.FirstOrDefault()?.HouseAddress ?? "");
            }))
            {
                rentByHouse.TryGetValue(hid, out var rentRows);
                depositByHouse.TryGetValue(hid, out var depositRows);
                var firstRent = (rentRows ?? new List<RentLedgerRowViewModel>()).FirstOrDefault();
                var firstDeposit = (depositRows ?? new List<DepositLedgerRowViewModel>()).FirstOrDefault();
                var address = firstRent?.HouseAddress ?? firstDeposit?.HouseAddress ?? "";
                _houseGroups.Add(new HouseLedgerGroupViewModel
                {
                    HouseId = hid,
                    HouseAddress = address,
                    IsExpanded = false
                });
                var g = _houseGroups[_houseGroups.Count - 1];
                if (rentRows != null) g.RentRows.AddRange(rentRows);
                if (depositRows != null) g.DepositRows.AddRange(depositRows);
            }
        }

        private void RebuildFlatLists()
        {
            FlatRentLedgerRows.Clear();
            FlatDepositLedgerRows.Clear();
            foreach (var group in _houseGroups)
            {
                FlatRentLedgerRows.Add(group);
                if (group.IsExpanded)
                {
                    foreach (var row in group.RentRows)
                        FlatRentLedgerRows.Add(row);
                }
                FlatDepositLedgerRows.Add(group);
                if (group.IsExpanded)
                {
                    foreach (var row in group.DepositRows)
                        FlatDepositLedgerRows.Add(row);
                }
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
                    SelectedYear,
                    SelectedMonth,
                    houseIdFilter,
                    StatusFilter == "All" ? null : StatusFilter,
                    string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm)).ConfigureAwait(false);

                // Update UI on UI thread (rows may be null if service threw before returning)
                var safeRows = rows ?? Enumerable.Empty<DepositLedgerRowViewModel>();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DepositLedgerRows.Clear();
                    foreach (var row in safeRows)
                    {
                        if (row == null) continue;
                        DepositLedgerRows.Add(row);
                    }
                    NotifyLedgerTotalsChanged();
                });
            }
            catch (Exception ex)
            {
                if (!IsActive)
                {
                    AppLogger.Log("RentLedger", $"Suppressing error dialog for abandoned load (navigated away): {ex.Message}");
                    return;
                }
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

        private async void RecordPaymentForSelectedRow()
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
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    dialog.Show();
                }
                // Reload both ledgers after dialog closes
                LoadLedgerCommand.Execute(null);
                LoadDepositLedgerCommand.Execute(null);
                RefreshDashboardIfActive();
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening record payment dialog: {ex.Message}", "Error");
            }
        }

        private async void RecordDepositPaymentForRow(DepositLedgerRowViewModel row)
        {
            if (row == null) return;

            try
            {
                var viewModel = _serviceProvider.GetRequiredService<RecordPaymentViewModel>();

                // Pre-select the tenancy from the deposit ledger row. Month/year will default to current
                viewModel.SetPreselectedTenancy(row.TenancyId);

                var dialog = new MVVM.Views.RecordPaymentDialog(viewModel);
                var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;
                if (mainWindow != null)
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner;
                    await dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    dialog.Show();
                }

                // Reload both ledgers after dialog closes
                LoadDepositLedgerCommand.Execute(null);
                LoadLedgerCommand.Execute(null);
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
