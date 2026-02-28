using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services;
using Daryva.Services.Api;
using Daryva.Services.Business;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly IHouseService _houseService;
        private readonly ITenantService _tenantService;
        private readonly IPaymentService _paymentService;
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _navigationService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsService _settingsService;
        private readonly IAuthApiService _authApiService;
        private readonly IAuthSessionService _authSessionService;
        private readonly NotificationCenterViewModel _notificationCenter;
        private readonly ProfileMenuViewModel _profileMenu;
        private readonly IOrgContext _orgContext;

        private int _housesCount;
        private int _activeTenantsCount;
        private int _overdueRentCount;
        private decimal _overdueRentAmount;
        private int _documentsExpiringSoonCount;
        private bool _showEmptyDepositMessage = true;
        private string _greetingText = "Hello";

        // Static event to notify all DashboardViewModel instances when payment is recorded/unrecorded
        public static event EventHandler? PaymentDataChanged;

        private EventHandler<BaseViewModel?>? _navigationHandler;
        private EventHandler? _paymentDataHandler;

        public DashboardViewModel(IHouseService houseService, ITenantService tenantService, IPaymentService paymentService, IServiceProvider serviceProvider, INavigationService navigationService, IDialogService dialogService, ISettingsService settingsService, IAuthApiService authApiService, IAuthSessionService authSessionService, NotificationCenterViewModel notificationCenter, ProfileMenuViewModel profileMenu, IOrgContext orgContext)
        {
            _houseService = houseService;
            _tenantService = tenantService;
            _paymentService = paymentService;
            _serviceProvider = serviceProvider;
            _navigationService = navigationService;
            _dialogService = dialogService;
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _authApiService = authApiService ?? throw new ArgumentNullException(nameof(authApiService));
            _authSessionService = authSessionService ?? throw new ArgumentNullException(nameof(authSessionService));
            _notificationCenter = notificationCenter ?? throw new ArgumentNullException(nameof(notificationCenter));
            _profileMenu = profileMenu ?? throw new ArgumentNullException(nameof(profileMenu));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));

            OverdueRent = new ObservableCollection<OverdueRentItem>();
            MissingDocuments = new ObservableCollection<MissingDocumentItem>();
            DepositReturnReminders = new ObservableCollection<DepositReturnReminderItem>();

            LoadDashboardDataCommand = new RelayCommand(async _ => 
            {
                try
                {
                    await LoadDashboardDataAsync();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error loading dashboard: {ex.Message}", "Error");
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

            _orgContext.CurrentOrgChanged += OnCurrentOrgChanged;
            
            // Load data on initialization
            LoadDashboardDataCommand.Execute(null);

            // Refresh notification count on load
            _ = _notificationCenter.RefreshAsync();
        }

        /// <summary>Notification center (bell drawer) for the header.</summary>
        public NotificationCenterViewModel NotificationCenter => _notificationCenter;

        /// <summary>Profile menu (avatar dropdown) for the header.</summary>
        public ProfileMenuViewModel ProfileMenu => _profileMenu;

        /// <summary>
        /// Cleanup method to unsubscribe from events.
        /// </summary>
        private void OnCurrentOrgChanged(object? sender, CurrentOrgChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => LoadDashboardDataCommand.Execute(null));
        }

        public void Cleanup()
        {
            _orgContext.CurrentOrgChanged -= OnCurrentOrgChanged;
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
                if (Dispatcher.UIThread.CheckAccess())
                {
                    // Call LoadDashboardDataAsync directly - fire and forget
                    _ = LoadDashboardDataAsync();
                }
                else
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await LoadDashboardDataAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error refreshing dashboard: {ex.Message}", "Error");
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
                if (Dispatcher.UIThread.CheckAccess())
                {
                    await LoadDashboardDataAsync();
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await LoadDashboardDataAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error refreshing dashboard: {ex.Message}", "Error");
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

        public string GreetingText
        {
            get => _greetingText;
            private set => SetProperty(ref _greetingText, value);
        }

        public ObservableCollection<OverdueRentItem> OverdueRent { get; }
        public ObservableCollection<MissingDocumentItem> MissingDocuments { get; }
        public ObservableCollection<DepositReturnReminderItem> DepositReturnReminders { get; }

        /// <summary>True when there are no deposit return reminders (show placeholder message).</summary>
        public bool ShowEmptyDepositMessage
        {
            get => _showEmptyDepositMessage;
            private set => SetProperty(ref _showEmptyDepositMessage, value);
        }

        private async Task LoadDashboardDataAsync()
        {
            const int maxAttempts = 2;
            var attempt = 0;

            while (true)
            {
                try
                {
                    attempt++;

                    var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                    DateTimeFormatProvider.DateFormat = dateFormat;

                    await UpdateGreetingAsync();

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

                    // Get previous 2 months to capture overdue rents
                    for (int i = 1; i <= 2; i++)
                    {
                        var checkDate = currentDate.AddMonths(-i);
                        var monthLedger = await _paymentService.GetRentLedgerForMonthAsync(
                            checkDate.Year, checkDate.Month, null, null, null);
                        allLedgerRows.AddRange(monthLedger);
                    }
                    
                    var ledgerList = allLedgerRows.ToList(); // Materialize

                    // Rent due total: use charge-based unpaid balance (payments linked by RentChargeId) so it matches
                    // reality when all tenants have paid. Ledger aggregation can differ; this is the source of truth.
                    // NOTE: Rent due calculation kept for overdue rent logic but not displayed separately anymore
                    var currentMonthUnpaid = await _paymentService.GetTotalUnpaidBalanceForMonthAsync(currentDate.Year, currentDate.Month);

                    // Calculate rent due in next 7 days - includes current month and next month if needed
                    // NOTE: This calculation is kept internally but the "Rent Due in Next 7 Days" table is no longer displayed
                    var endDate = currentDate.AddDays(7);

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

                    var depositRemindersList = (await _paymentService.GetDepositReturnRemindersAsync()).ToList();
                    
                    // Update ALL UI properties on UI thread in a single dispatcher call
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        // Already on UI thread - update directly
                        UpdateDashboardProperties(houseCount, activeTenantCount, overdueRentList, overdueRows.Count, totalOverdue, depositRemindersList);
                    }
                    else
                    {
                        // Need to marshal to UI thread
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            UpdateDashboardProperties(houseCount, activeTenantCount, overdueRentList, overdueRows.Count, totalOverdue, depositRemindersList);
                        });
                    }

                    // Successfully completed without exceptions
                    return;
                }
                catch (InvalidOperationException ex) when (
                    attempt < maxAttempts &&
                    (
                        ex.Message.Contains("The connection is closed", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("BeginExecuteReader requires an open and available Connection", StringComparison.OrdinalIgnoreCase) ||
                        ex.Message.Contains("current state is connecting", StringComparison.OrdinalIgnoreCase)
                    ))
                {
                    // Sometimes the SQL connection may still be initializing when the dashboard loads.
                    // Wait a bit and retry once before showing an error to the user.
                    await Task.Delay(500);
                    continue;
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error loading dashboard data: {ex.Message}", "Error");
                    return;
                }
            }
        }

        private async Task UpdateGreetingAsync()
        {
            string? firstName = null;

            try
            {
                var me = await _authApiService.GetMeAsync();
                firstName = me?.FirstName?.Trim();
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(firstName))
            {
                firstName = ExtractFirstNameFromEmail(_authSessionService.Email);
            }

            GreetingText = string.IsNullOrWhiteSpace(firstName) ? "Hello" : $"Hello, {firstName}";
        }

        private static string? ExtractFirstNameFromEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var localPart = email.Split('@', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(localPart))
            {
                return null;
            }

            var firstChunk = localPart
                .Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(firstChunk))
            {
                return null;
            }

            return char.ToUpperInvariant(firstChunk[0]) + firstChunk[1..].ToLowerInvariant();
        }

        private void UpdateDashboardProperties(int houseCount, int activeTenantCount, 
            List<OverdueRentItem> overdueRentList, 
            int overdueCount, decimal overdueAmount, List<DepositReturnReminderItem> depositRemindersList)
        {
            // Update basic counts
            HousesCount = houseCount;
            ActiveTenantsCount = activeTenantCount;
            
            // Update overdue rent
            OverdueRent.Clear();
            foreach (var item in overdueRentList)
            {
                OverdueRent.Add(item);
            }

            DepositReturnReminders.Clear();
            foreach (var item in depositRemindersList)
            {
                DepositReturnReminders.Add(item);
            }
            ShowEmptyDepositMessage = depositRemindersList.Count == 0;
            
            OverdueRentCount = overdueCount;
            OverdueRentAmount = overdueAmount;
            DocumentsExpiringSoonCount = 0;
            
            // Force property change notifications
            OnPropertyChanged(nameof(OverdueRentAmount));
            OnPropertyChanged(nameof(OverdueRentCount));
            OnPropertyChanged(nameof(HousesCount));
            OnPropertyChanged(nameof(ActiveTenantsCount));
            OnPropertyChanged(nameof(OverdueRent));
            OnPropertyChanged(nameof(DepositReturnReminders));
            OnPropertyChanged(nameof(ShowEmptyDepositMessage));
        }

        private async void ShowAddHouseDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddHouseViewModel>();
            var dialog = new MVVM.Views.AddHouseDialog(viewModel);
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
            LoadDashboardDataCommand.Execute(null);
        }

        private async void ShowAddTenantDialog()
        {
            var viewModel = _serviceProvider.GetRequiredService<AddTenantViewModel>();
            var dialog = new MVVM.Views.AddTenantDialog(viewModel);
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
            LoadDashboardDataCommand.Execute(null);
        }

        private async void ShowRecordPaymentDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<RecordPaymentViewModel>();
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
                
                // Refresh dashboard data after payment is recorded
                LoadDashboardDataCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error: {ex.Message}", "Error");
            }
        }

        private void NavigateToDocuments()
        {
            _navigationService.NavigateTo<DocumentsViewModel>();
        }
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
