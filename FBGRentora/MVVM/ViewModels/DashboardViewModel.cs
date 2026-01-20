using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FBGRentora.MVVM.Commands;
using FBGRentora.MVVM.Models;
using FBGRentora.Services.Business;
using FBGRentora.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace FBGRentora.MVVM.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly IHouseService _houseService;
        private readonly ITenantService _tenantService;
        private readonly IPaymentService _paymentService;
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _navigationService;

        private int _housesCount;
        private int _activeTenantsCount;
        private decimal _rentDueThisMonth;
        private int _overdueRentCount;
        private decimal _overdueRentAmount;
        private int _documentsExpiringSoonCount;

        // Static event to notify all DashboardViewModel instances when payment is recorded/unrecorded
        public static event EventHandler? PaymentDataChanged;

        private EventHandler<BaseViewModel?>? _navigationHandler;
        private EventHandler? _paymentDataHandler;

        public DashboardViewModel(IHouseService houseService, ITenantService tenantService, IPaymentService paymentService, IServiceProvider serviceProvider, INavigationService navigationService)
        {
            _houseService = houseService;
            _tenantService = tenantService;
            _paymentService = paymentService;
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;

            RentDueInNext7Days = new ObservableCollection<RentDueItem>();
            OverdueRent = new ObservableCollection<OverdueRentItem>();
            MissingDocuments = new ObservableCollection<MissingDocumentItem>();

            LoadDashboardDataCommand = new RelayCommand(async _ => 
            {
                try
                {
                    await LoadDashboardDataAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error loading dashboard: {ex.Message}", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
            
            // Subscribe to navigation changes - refresh when Dashboard becomes active
            _navigationHandler = (s, vm) =>
            {
                if (vm is DashboardViewModel dashboardVm && dashboardVm == this)
                {
                    // This Dashboard instance just became active, refresh data
                    LoadDashboardDataCommand.Execute(null);
                }
            };
            navigationService.CurrentViewModelChanged += _navigationHandler;
            
            // Subscribe to payment data changes - refresh when payments are recorded/unrecorded
            _paymentDataHandler = OnPaymentDataChanged;
            PaymentDataChanged += _paymentDataHandler;
            
            // Load data on initialization
            LoadDashboardDataCommand.Execute(null);
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
            
            if (_paymentDataHandler != null)
            {
                PaymentDataChanged -= _paymentDataHandler;
                _paymentDataHandler = null;
            }
        }

        private void OnPaymentDataChanged(object? sender, EventArgs e)
        {
            // Refresh this Dashboard instance when payment data changes
            try
            {
                // Ensure we're on the UI thread
                var dispatcher = System.Windows.Application.Current.Dispatcher;
                if (dispatcher == null)
                {
                    return;
                }
                
                if (dispatcher.CheckAccess())
                {
                    // Call LoadDashboardDataAsync directly - fire and forget
                    _ = LoadDashboardDataAsync();
                }
                else
                {
                    dispatcher.InvokeAsync(async () =>
                    {
                        await LoadDashboardDataAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error refreshing dashboard: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Call this static method to notify all DashboardViewModel instances to refresh
        /// </summary>
        public static void NotifyPaymentDataChanged()
        {
            PaymentDataChanged?.Invoke(null, EventArgs.Empty);
        }

        public ICommand LoadDashboardDataCommand { get; }
        
        /// <summary>
        /// Public method to refresh dashboard data. Can be called from other ViewModels.
        /// </summary>
        public void RefreshDashboard()
        {
            // Call LoadDashboardDataAsync directly instead of through the command
            _ = Task.Run(async () =>
            {
                try
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await LoadDashboardDataAsync();
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error refreshing dashboard: {ex.Message}", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
        }
        public ICommand AddHouseCommand => new RelayCommand(_ => ShowAddHouseDialog());
        public ICommand AddTenantCommand => new RelayCommand(_ => ShowAddTenantDialog());
        public ICommand RecordPaymentCommand => new RelayCommand(_ => ShowRecordPaymentDialog());
        public ICommand UploadDocumentCommand => new RelayCommand(_ => NavigateToDocuments());

        public int HousesCount
        {
            get => _housesCount;
            set => SetProperty(ref _housesCount, value);
        }

        public int ActiveTenantsCount
        {
            get => _activeTenantsCount;
            set => SetProperty(ref _activeTenantsCount, value);
        }

        public decimal RentDueThisMonth
        {
            get => _rentDueThisMonth;
            set => SetProperty(ref _rentDueThisMonth, value);
        }

        public int OverdueRentCount
        {
            get => _overdueRentCount;
            set => SetProperty(ref _overdueRentCount, value);
        }

        public decimal OverdueRentAmount
        {
            get => _overdueRentAmount;
            set => SetProperty(ref _overdueRentAmount, value);
        }

        public int DocumentsExpiringSoonCount
        {
            get => _documentsExpiringSoonCount;
            set => SetProperty(ref _documentsExpiringSoonCount, value);
        }

        public ObservableCollection<RentDueItem> RentDueInNext7Days { get; }
        public ObservableCollection<OverdueRentItem> OverdueRent { get; }
        public ObservableCollection<MissingDocumentItem> MissingDocuments { get; }

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                // Load basic counts - call services directly (they handle async properly)
                var houses = await _houseService.GetAllHousesAsync();
                var houseCount = houses.Count();
                
                var tenants = await _tenantService.GetAllTenantsAsync();
                var activeTenantCount = tenants.Count(t => !string.IsNullOrEmpty(t.CurrentHouseAddress));

                // Load rent data - get current month and previous 2 months to capture all overdue rents
                var currentDate = DateTime.Now;
                var allLedgerRows = new List<RentLedgerRowViewModel>();
                
                // Get current month
                var currentMonthLedger = await _paymentService.GetRentLedgerForMonthAsync(
                    currentDate.Year, currentDate.Month, null, null, null);
                allLedgerRows.AddRange(currentMonthLedger);
                
                // Small delay to ensure connection is released
                await Task.Delay(50);
                
                // Get previous 2 months to capture overdue rents
                for (int i = 1; i <= 2; i++)
                {
                    var checkDate = currentDate.AddMonths(-i);
                    var monthLedger = await _paymentService.GetRentLedgerForMonthAsync(
                        checkDate.Year, checkDate.Month, null, null, null);
                    allLedgerRows.AddRange(monthLedger);
                    
                    // Small delay between queries to ensure connection is released
                    if (i < 2)
                    {
                        await Task.Delay(50);
                    }
                }
                
                var ledgerList = allLedgerRows.ToList(); // Materialize

                // Helper method to check if rent is fully paid
                bool IsRentFullyPaid(RentLedgerRowViewModel row)
                {
                    decimal balance = row.AmountDue - row.AmountPaid;
                    return string.Equals(row.Status?.Trim(), "Paid", StringComparison.OrdinalIgnoreCase) || balance <= 0;
                }

                // Calculate rent due this month - includes current month unpaid/part-paid/overdue + all overdue from previous months
                var currentMonthUnpaid = ledgerList
                    .Where(r => r.DueDate.Year == currentDate.Year && 
                               r.DueDate.Month == currentDate.Month && 
                               !IsRentFullyPaid(r))
                    .Sum(r => r.AmountDue - r.AmountPaid);
                
                var overdueFromPreviousMonths = ledgerList
                    .Where(r => r.DueDate < new DateTime(currentDate.Year, currentDate.Month, 1) && 
                               !IsRentFullyPaid(r))
                    .Sum(r => r.AmountDue - r.AmountPaid);
                
                var totalRentDue = currentMonthUnpaid + overdueFromPreviousMonths;

                // Calculate rent due in next 7 days - includes current month and next month if needed
                var endDate = currentDate.AddDays(7);
                var rentDueInNext7DaysList = new List<RentDueItem>();
                
                // Also check next month if 7 days spans into it
                var nextMonthRows = new List<RentLedgerRowViewModel>();
                if (endDate.Month != currentDate.Month || endDate.Year != currentDate.Year)
                {
                    var nextMonthDate = endDate;
                    // Small delay before next query
                    await Task.Delay(50);
                    var nextMonthLedger = await _paymentService.GetRentLedgerForMonthAsync(
                        nextMonthDate.Year, nextMonthDate.Month, null, null, null);
                    nextMonthRows = nextMonthLedger.ToList();
                }
                
                // Combine current month rows with next month rows for the 7-day window
                var allRowsFor7Days = ledgerList
                    .Where(r => r.DueDate.Year == currentDate.Year && r.DueDate.Month == currentDate.Month)
                    .Concat(nextMonthRows)
                    .Where(r => r.DueDate >= currentDate && r.DueDate <= endDate && r.Balance > 0)
                    .OrderBy(r => r.DueDate)
                    .ToList();
                
                foreach (var row in allRowsFor7Days)
                {
                    rentDueInNext7DaysList.Add(new RentDueItem
                    {
                        TenantName = row.TenantName,
                        HouseAddress = row.HouseAddress,
                        Amount = row.Balance,
                        DueDate = row.DueDate
                    });
                }

                // Calculate overdue rent - only show tenants whose MOST RECENT period is unpaid/overdue
                // If a tenant paid their most recent period, they should NOT appear, even if they have older unpaid periods
                var overdueRentList = new List<OverdueRentItem>();
                decimal totalOverdue = 0;
                
                // Group by tenancy first, then check the most recent period for each tenant
                var overdueRows = ledgerList
                    .GroupBy(r => r.TenancyId)
                    .Select(g => 
                    {
                        // Get ALL periods for this tenancy, ordered by due date (most recent first)
                        var allPeriods = g.OrderByDescending(r => r.DueDate).ToList();
                        
                        // Find the most recent period (by due date)
                        var mostRecentPeriod = allPeriods.First();
                        
                        // Only include if:
                        // 1. The most recent period is overdue (due date in the past)
                        // 2. The most recent period has Balance > 0 (not fully paid)
                        if (mostRecentPeriod.DueDate < currentDate.Date && mostRecentPeriod.Balance > 0)
                        {
                            return mostRecentPeriod;
                        }
                        else
                        {
                            return null;
                        }
                    })
                    .Where(r => r != null) // Filter out nulls
                    .ToList();

                foreach (var row in overdueRows)
                {
                    if (row != null && row.Balance > 0)
                    {
                        var daysLate = (currentDate.Date - row.DueDate.Date).Days;
                        overdueRentList.Add(new OverdueRentItem
                        {
                            TenantName = row.TenantName,
                            HouseAddress = row.HouseAddress,
                            Amount = row.Balance,
                            DaysLate = daysLate
                        });
                        totalOverdue += row.Balance;
                    }
                }
                
                // Update ALL UI properties on UI thread in a single dispatcher call
                var dispatcher = System.Windows.Application.Current.Dispatcher;
                if (dispatcher.CheckAccess())
                {
                    // Already on UI thread - update directly
                    UpdateDashboardProperties(houseCount, activeTenantCount, totalRentDue, rentDueInNext7DaysList, overdueRentList, overdueRows.Count, totalOverdue);
                }
                else
                {
                    // Need to marshal to UI thread
                    await dispatcher.InvokeAsync(() =>
                    {
                        UpdateDashboardProperties(houseCount, activeTenantCount, totalRentDue, rentDueInNext7DaysList, overdueRentList, overdueRows.Count, totalOverdue);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading dashboard data: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void UpdateDashboardProperties(int houseCount, int activeTenantCount, decimal totalRentDue, 
            List<RentDueItem> rentDueInNext7DaysList, List<OverdueRentItem> overdueRentList, 
            int overdueCount, decimal overdueAmount)
        {
            // Update basic counts
            HousesCount = houseCount;
            ActiveTenantsCount = activeTenantCount;
            
            // Update rent due this month
            RentDueThisMonth = totalRentDue;
            
            // Update rent due in next 7 days
            RentDueInNext7Days.Clear();
            foreach (var item in rentDueInNext7DaysList)
            {
                RentDueInNext7Days.Add(item);
            }
            
            // Update overdue rent
            OverdueRent.Clear();
            foreach (var item in overdueRentList)
            {
                OverdueRent.Add(item);
            }
            
            OverdueRentCount = overdueCount;
            OverdueRentAmount = overdueAmount;
            DocumentsExpiringSoonCount = 0;
            
            // Force property change notifications
            OnPropertyChanged(nameof(RentDueThisMonth));
            OnPropertyChanged(nameof(OverdueRentAmount));
            OnPropertyChanged(nameof(OverdueRentCount));
            OnPropertyChanged(nameof(HousesCount));
            OnPropertyChanged(nameof(ActiveTenantsCount));
            OnPropertyChanged(nameof(RentDueInNext7Days));
            OnPropertyChanged(nameof(OverdueRent));
        }

        private void ShowAddHouseDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddHouseViewModel>();
            var dialog = new MVVM.Views.AddHouseDialog(viewModel);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                LoadDashboardDataCommand.Execute(null);
            }
        }

        private void ShowAddTenantDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddTenantViewModel>();
            var dialog = new MVVM.Views.AddTenantDialog(viewModel);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            if (dialog.ShowDialog() == true)
            {
                LoadDashboardDataCommand.Execute(null);
            }
        }

        private void ShowRecordPaymentDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<RecordPaymentViewModel>();
                var dialog = new MVVM.Views.RecordPaymentDialog(viewModel);
                
                if (System.Windows.Application.Current.MainWindow != null)
                {
                    dialog.Owner = System.Windows.Application.Current.MainWindow;
                    dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                }
                else
                {
                    dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                }
                
                var result = dialog.ShowDialog();
                
                if (result == true)
                {
                    // Refresh dashboard data after payment is recorded
                    LoadDashboardDataCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void NavigateToDocuments()
        {
            _navigationService.NavigateTo<DocumentsViewModel>();
        }
    }

    public class RentDueItem
    {
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }

    public class OverdueRentItem
    {
        public string TenantName { get; set; } = string.Empty;
        public string HouseAddress { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int DaysLate { get; set; }
    }

    public class MissingDocumentItem
    {
        public string TenantName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Missing, Expired, Expiring
    }
}
