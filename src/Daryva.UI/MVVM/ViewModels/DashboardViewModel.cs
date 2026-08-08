using System.Collections.ObjectModel;
using System.Globalization;
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
        private readonly IExpenseService _expenseService;
        private readonly IDocumentService _documentService;
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
        private decimal _monthlyIncome;
        private decimal _rentCollectedPaidAmount;
        private decimal _rentCollectedPendingAmount;
        private decimal _rentCollectedOverdueAmount;
        private decimal _rentCollectedPercent;
        private bool _showEmptyDepositMessage = true;
        private bool _showEmptyCashFlowMessage;
        private bool _showEmptyRentCollectionMessage;
        private string _greetingText = "Hello";

        // Static event to notify all DashboardViewModel instances when payment is recorded/unrecorded
        public static event EventHandler? PaymentDataChanged;

        private EventHandler<BaseViewModel?>? _navigationHandler;
        private EventHandler? _paymentDataHandler;

        public DashboardViewModel(IHouseService houseService, ITenantService tenantService, IPaymentService paymentService, IExpenseService expenseService, IDocumentService documentService, IServiceProvider serviceProvider, INavigationService navigationService, IDialogService dialogService, ISettingsService settingsService, IAuthApiService authApiService, IAuthSessionService authSessionService, NotificationCenterViewModel notificationCenter, ProfileMenuViewModel profileMenu, IOrgContext orgContext)
        {
            _houseService = houseService;
            _tenantService = tenantService;
            _paymentService = paymentService;
            _expenseService = expenseService ?? throw new ArgumentNullException(nameof(expenseService));
            _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
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
            CashFlowMonths = new ObservableCollection<CashFlowMonthPoint>();
            UpcomingEvents = new ObservableCollection<UpcomingEventItem>();
            RecentRentRows = new ObservableCollection<RentLedgerRowViewModel>();
            RecentDocuments = new ObservableCollection<Document>();

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
        public ICommand UploadDocumentCommand => new RelayCommand(_ => ShowUploadDocumentDialog());
        public ICommand AddExpenseCommand => new RelayCommand(_ => ShowAddExpenseDialog());
        public ICommand InviteMemberCommand => new RelayCommand(_ => ShowInviteMemberDialog(), _ => _orgContext.CurrentOrgId.HasValue);

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

        /// <summary>Sum of recorded rent transactions so far this calendar month.</summary>
        public decimal MonthlyIncome
        {
            get => _monthlyIncome;
            set => SetProperty(ref _monthlyIncome, value);
        }

        /// <summary>Sum of this month's ledger rows' AmountDue where Status == "Paid".</summary>
        public decimal RentCollectedPaidAmount
        {
            get => _rentCollectedPaidAmount;
            set => SetProperty(ref _rentCollectedPaidAmount, value);
        }

        /// <summary>Sum of this month's ledger rows' AmountDue where Status is "Unpaid" or "PartPaid"
        /// (not yet overdue).</summary>
        public decimal RentCollectedPendingAmount
        {
            get => _rentCollectedPendingAmount;
            set => SetProperty(ref _rentCollectedPendingAmount, value);
        }

        /// <summary>Sum of this month's ledger rows' AmountDue where Status == "Overdue".</summary>
        public decimal RentCollectedOverdueAmount
        {
            get => _rentCollectedOverdueAmount;
            set => SetProperty(ref _rentCollectedOverdueAmount, value);
        }

        /// <summary>RentCollectedPaidAmount as a percentage of the month's total AmountDue.</summary>
        public decimal RentCollectedPercent
        {
            get => _rentCollectedPercent;
            set => SetProperty(ref _rentCollectedPercent, value);
        }

        /// <summary>True when every month in CashFlowMonths has zero income and zero expenses --
        /// distinct from an empty collection, since CashFlowMonths always has 6 points structurally.</summary>
        public bool ShowEmptyCashFlowMessage
        {
            get => _showEmptyCashFlowMessage;
            private set => SetProperty(ref _showEmptyCashFlowMessage, value);
        }

        /// <summary>True when this month's rent ledger has no rows at all (nothing due from anyone).</summary>
        public bool ShowEmptyRentCollectionMessage
        {
            get => _showEmptyRentCollectionMessage;
            private set => SetProperty(ref _showEmptyRentCollectionMessage, value);
        }

        public string GreetingText
        {
            get => _greetingText;
            private set => SetProperty(ref _greetingText, value);
        }

        public ObservableCollection<OverdueRentItem> OverdueRent { get; }
        public ObservableCollection<MissingDocumentItem> MissingDocuments { get; }
        public ObservableCollection<DepositReturnReminderItem> DepositReturnReminders { get; }

        /// <summary>Last 6 months of income (recorded rent transactions) vs. expenses, for the
        /// cash-flow chart.</summary>
        public ObservableCollection<CashFlowMonthPoint> CashFlowMonths { get; }

        /// <summary>Rent due soon (next 3 days) and documents expiring soon (next 30 days),
        /// merged and sorted by date. No maintenance/lease-renewal rows -- neither concept exists
        /// anywhere in this product.</summary>
        public ObservableCollection<UpcomingEventItem> UpcomingEvents { get; }

        /// <summary>This month's rent ledger rows (already fetched for the collection breakdown
        /// above), most-recently-due first -- "this month's rent status per tenancy," not a
        /// rolling transaction feed, since the ledger is what actually carries Status/DueDate.</summary>
        public ObservableCollection<RentLedgerRowViewModel> RecentRentRows { get; }

        public ObservableCollection<Document> RecentDocuments { get; }

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

                    var currentDate = DateTime.Now;

                    // This month's ledger: source for the rent-collection breakdown and the
                    // "recent rent activity" table -- the only ledger fetch needed now that
                    // overdue rent uses the dedicated GetOverdueRentAsync() below instead of a
                    // hand-rolled multi-month scan.
                    var currentMonthLedger = (await _paymentService.GetRentLedgerForMonthAsync(
                        currentDate.Year, currentDate.Month, null, null, null)).ToList();

                    var overdueItems = (await _paymentService.GetOverdueRentAsync()).ToList();
                    var overdueRentList = overdueItems.Select(i => new OverdueRentItem
                    {
                        TenantName = i.TenantName,
                        HouseAddress = i.HouseAddress,
                        Amount = i.Amount,
                        DaysLate = i.DaysLate
                    }).ToList();
                    var totalOverdue = overdueItems.Sum(i => i.Amount);

                    var depositRemindersList = (await _paymentService.GetDepositReturnRemindersAsync()).ToList();

                    // Documents expiring in the next 30 days -- feeds both the KPI count (fixing
                    // a bug where it was hardcoded to 0 and never actually computed) and the
                    // Upcoming widget's document-expiry rows. One call for both.
                    var expiringDocuments = (await _documentService.GetExpiringDocumentsAsync(30)).ToList();

                    // Rent due in the next 3 days -- same window/logic NotificationFeedService
                    // already uses for the header bell's "Rent due soon" items.
                    var rentDueSoon = await GetRentDueSoonAsync(currentDate, currentMonthLedger);

                    // This calendar month's recorded rent transactions.
                    var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
                    var monthlyIncome = (await _paymentService.GetTransactionsAsync(monthStart, currentDate, "Rent"))
                        .Sum(t => t.Amount);

                    var cashFlowMonths = await BuildCashFlowMonthsAsync(currentDate);

                    var recentDocuments = (await _documentService.GetDocumentsAsync())
                        .OrderByDescending(d => d.UploadedAt)
                        .Take(5)
                        .ToList();

                    var upcomingEvents = BuildUpcomingEvents(rentDueSoon, expiringDocuments, currentDate);

                    var recentRentRows = currentMonthLedger
                        .OrderBy(r => r.DueDate)
                        .ThenBy(r => r.TenantName)
                        .Take(8)
                        .ToList();

                    var (paidAmount, pendingAmount, overdueAmount) = SummarizeRentCollection(currentMonthLedger);
                    var totalDue = currentMonthLedger.Sum(r => r.AmountDue);
                    var collectedPercent = totalDue > 0 ? paidAmount / totalDue * 100m : 0m;

                    var snapshot = new DashboardSnapshot
                    {
                        HouseCount = houseCount,
                        ActiveTenantCount = activeTenantCount,
                        OverdueRentList = overdueRentList,
                        OverdueCount = overdueItems.Count,
                        OverdueAmount = totalOverdue,
                        DepositRemindersList = depositRemindersList,
                        DocumentsExpiringSoonCount = expiringDocuments.Count,
                        MonthlyIncome = monthlyIncome,
                        CashFlowMonths = cashFlowMonths,
                        UpcomingEvents = upcomingEvents,
                        RecentRentRows = recentRentRows,
                        RecentDocuments = recentDocuments,
                        RentCollectedPaidAmount = paidAmount,
                        RentCollectedPendingAmount = pendingAmount,
                        RentCollectedOverdueAmount = overdueAmount,
                        RentCollectedPercent = collectedPercent
                    };

                    // Update ALL UI properties on UI thread in a single dispatcher call
                    if (Dispatcher.UIThread.CheckAccess())
                    {
                        UpdateDashboardProperties(snapshot);
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => UpdateDashboardProperties(snapshot));
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

        /// <summary>Ports NotificationFeedService's "rent due soon" rule (next 3 days, Balance > 0)
        /// so the dashboard's Upcoming widget and the notification bell agree on what counts as
        /// due soon. Reuses the already-fetched current-month ledger instead of re-fetching it.</summary>
        private async Task<List<RentLedgerRowViewModel>> GetRentDueSoonAsync(DateTime currentDate, List<RentLedgerRowViewModel> currentMonthLedger)
        {
            var today = currentDate.Date;
            var endSoon = today.AddDays(3);
            var seenTenancies = new HashSet<int>();
            var result = new List<RentLedgerRowViewModel>();

            void Collect(IEnumerable<RentLedgerRowViewModel> rows)
            {
                foreach (var row in rows)
                {
                    if (row.DueDate < today || row.DueDate > endSoon || row.Balance <= 0)
                        continue;
                    if (!seenTenancies.Add(row.TenancyId))
                        continue;
                    result.Add(row);
                }
            }

            Collect(currentMonthLedger);
            var nextMonth = today.AddMonths(1);
            var nextMonthLedger = await _paymentService.GetRentLedgerForMonthAsync(nextMonth.Year, nextMonth.Month, null, null, null);
            Collect(nextMonthLedger);

            return result;
        }

        /// <summary>Last 6 months (5 back + current) of income (per-month sum of recorded rent
        /// transactions) vs. expenses (IExpenseService's own ByMonth aggregate). No combined
        /// endpoint exists for this, so income is composed client-side the same way the old
        /// overdue-rent logic used to loop over months -- an accepted, explicitly-confirmed
        /// tradeoff over adding a new API aggregate endpoint.</summary>
        private async Task<List<CashFlowMonthPoint>> BuildCashFlowMonthsAsync(DateTime currentDate)
        {
            const int monthsBack = 5;
            var currentMonthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var earliestMonth = currentMonthStart.AddMonths(-monthsBack);

            var expenseSummary = await _expenseService.GetExpenseSummaryAsync(startDate: earliestMonth, endDate: currentDate);
            var expensesByMonth = expenseSummary.ByMonth.ToDictionary(m => (m.Year, m.Month), m => m.Total);

            var points = new List<CashFlowMonthPoint>();
            for (var i = monthsBack; i >= 0; i--)
            {
                var monthDate = currentMonthStart.AddMonths(-i);
                var monthEnd = monthDate.AddMonths(1).AddDays(-1);
                if (monthEnd > currentDate)
                    monthEnd = currentDate; // Don't count days that haven't happened yet for the in-progress month.

                var income = (await _paymentService.GetTransactionsAsync(monthDate, monthEnd, "Rent")).Sum(t => t.Amount);
                expensesByMonth.TryGetValue((monthDate.Year, monthDate.Month), out var expenses);

                points.Add(new CashFlowMonthPoint
                {
                    Year = monthDate.Year,
                    Month = monthDate.Month,
                    MonthLabel = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                    Income = income,
                    Expenses = expenses
                });
            }
            return points;
        }

        private static List<UpcomingEventItem> BuildUpcomingEvents(
            List<RentLedgerRowViewModel> rentDueSoon, List<Document> expiringDocuments, DateTime currentDate)
        {
            var today = currentDate.Date;
            var items = new List<UpcomingEventItem>();

            foreach (var row in rentDueSoon)
            {
                items.Add(new UpcomingEventItem
                {
                    Category = UpcomingEventCategory.RentDueSoon,
                    Title = "Rent due soon",
                    Description = $"{row.TenantName} — £{row.Balance:N2}",
                    Date = row.DueDate,
                    DateDisplay = row.DueDateDisplay,
                    IsUrgent = (row.DueDate.Date - today).Days <= 1
                });
            }

            foreach (var doc in expiringDocuments.Where(d => d.ValidTo.HasValue))
            {
                items.Add(new UpcomingEventItem
                {
                    Category = UpcomingEventCategory.DocumentExpiring,
                    Title = "Document expiring",
                    Description = doc.DisplayName,
                    Date = doc.ValidTo!.Value,
                    DateDisplay = doc.ValidTo.Value.ToString("dd MMM"),
                    IsUrgent = doc.ValidTo.Value <= today.AddDays(7)
                });
            }

            return items.OrderBy(i => i.Date).ToList();
        }

        private static (decimal Paid, decimal Pending, decimal Overdue) SummarizeRentCollection(List<RentLedgerRowViewModel> ledger)
        {
            decimal paid = 0, pending = 0, overdue = 0;
            foreach (var row in ledger)
            {
                switch (row.Status)
                {
                    case "Paid":
                        paid += row.AmountDue;
                        break;
                    case "Overdue":
                        overdue += row.AmountDue;
                        break;
                    default: // Unpaid, PartPaid
                        pending += row.AmountDue;
                        break;
                }
            }
            return (paid, pending, overdue);
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

        private void UpdateDashboardProperties(DashboardSnapshot s)
        {
            HousesCount = s.HouseCount;
            ActiveTenantsCount = s.ActiveTenantCount;

            OverdueRent.Clear();
            foreach (var item in s.OverdueRentList)
                OverdueRent.Add(item);
            OverdueRentCount = s.OverdueCount;
            OverdueRentAmount = s.OverdueAmount;

            DepositReturnReminders.Clear();
            foreach (var item in s.DepositRemindersList)
                DepositReturnReminders.Add(item);
            ShowEmptyDepositMessage = s.DepositRemindersList.Count == 0;

            DocumentsExpiringSoonCount = s.DocumentsExpiringSoonCount;
            MonthlyIncome = s.MonthlyIncome;

            CashFlowMonths.Clear();
            foreach (var point in s.CashFlowMonths)
                CashFlowMonths.Add(point);
            ShowEmptyCashFlowMessage = s.CashFlowMonths.All(p => p.Income == 0 && p.Expenses == 0);

            UpcomingEvents.Clear();
            foreach (var item in s.UpcomingEvents)
                UpcomingEvents.Add(item);

            RecentRentRows.Clear();
            foreach (var row in s.RecentRentRows)
                RecentRentRows.Add(row);

            RecentDocuments.Clear();
            foreach (var doc in s.RecentDocuments)
                RecentDocuments.Add(doc);

            RentCollectedPaidAmount = s.RentCollectedPaidAmount;
            RentCollectedPendingAmount = s.RentCollectedPendingAmount;
            RentCollectedOverdueAmount = s.RentCollectedOverdueAmount;
            RentCollectedPercent = s.RentCollectedPercent;
            ShowEmptyRentCollectionMessage = s.RentCollectedPaidAmount == 0 && s.RentCollectedPendingAmount == 0 && s.RentCollectedOverdueAmount == 0;
        }

        /// <summary>Everything LoadDashboardDataAsync fetches off the UI thread, bundled so it can
        /// be handed to UpdateDashboardProperties in one Dispatcher call -- replaces the previous
        /// 6-parameter version now that there's meaningfully more state to carry.</summary>
        private sealed class DashboardSnapshot
        {
            public int HouseCount { get; set; }
            public int ActiveTenantCount { get; set; }
            public List<OverdueRentItem> OverdueRentList { get; set; } = new();
            public int OverdueCount { get; set; }
            public decimal OverdueAmount { get; set; }
            public List<DepositReturnReminderItem> DepositRemindersList { get; set; } = new();
            public int DocumentsExpiringSoonCount { get; set; }
            public decimal MonthlyIncome { get; set; }
            public List<CashFlowMonthPoint> CashFlowMonths { get; set; } = new();
            public List<UpcomingEventItem> UpcomingEvents { get; set; } = new();
            public List<RentLedgerRowViewModel> RecentRentRows { get; set; } = new();
            public List<Document> RecentDocuments { get; set; } = new();
            public decimal RentCollectedPaidAmount { get; set; }
            public decimal RentCollectedPendingAmount { get; set; }
            public decimal RentCollectedOverdueAmount { get; set; }
            public decimal RentCollectedPercent { get; set; }
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

        private async void ShowUploadDocumentDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<UploadDocumentViewModel>();
                var dialog = new MVVM.Views.UploadDocumentDialog(viewModel);
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
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening upload document dialog: {ex.Message}", "Error");
            }
        }

        private async void ShowAddExpenseDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<AddEditExpenseViewModel>();
                viewModel.IsEditMode = false;
                var dialog = new MVVM.Views.AddEditExpenseDialog(viewModel);
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
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening add expense dialog: {ex.Message}", "Error");
            }
        }

        private async void ShowInviteMemberDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<InviteMemberViewModel>();
                var dialog = new MVVM.Views.InviteMemberDialog(viewModel);
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
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error opening invite member dialog: {ex.Message}", "Error");
            }
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
