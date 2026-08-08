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
    public class DashboardViewModel : BaseViewModel, INavigationAware
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

        // Below the fixed Cash flow/Rent collection row, these three rows reorder by whether they
        // have real data to show -- see UpdateDashboardProperties' RecomputeRowRanks. 0/1/2 is a
        // permutation (Grid.Row position), not a priority weight.
        private int _upcomingRowRank;
        private int _recentActivityRowRank = 1;
        private int _depositRemindersRowRank = 2;

        // Static event to notify all DashboardViewModel instances when payment is recorded/unrecorded
        public static event EventHandler? PaymentDataChanged;

        private EventHandler? _paymentDataHandler;
        // Short-lived cache of the last successfully-built snapshot per org, shared across every
        // DashboardViewModel instance (static, since a fresh transient instance is created on every
        // visit -- see NavigationService.NavigateTo). Confirmed via the session log that repeatedly
        // revisiting Dashboard (each visit is ~15 independent API calls) is enough on its own to
        // exceed the backend's 200-requests/minute budget under rapid tab switching, even with no
        // other bug involved -- a few seconds of staleness on KPI counts is a reasonable trade for
        // not re-fetching everything on every single revisit. Explicit data-changing actions (a
        // payment recorded, a house added, etc.) still force a real refresh -- see forceRefresh.
        private static readonly Dictionary<Guid, (DateTime CachedAtUtc, DashboardSnapshot Snapshot)> _snapshotCache = new();
        private static readonly object _snapshotCacheLock = new();
        private static readonly TimeSpan SnapshotCacheTtl = TimeSpan.FromSeconds(20);
        private readonly AsyncDebouncer _orgChangeDebouncer = new(TimeSpan.FromMilliseconds(400));
        // Bumped every time a load starts; a load only applies its result (or shows its error
        // dialog) if it's still the most recent one requested on THIS instance by the time it
        // finishes. Guards against two overlapping LoadDashboardDataCommand invocations on the
        // same instance (e.g. the constructor's initial load and a debounced org-change reload
        // landing close together) racing and having the OLDER one's stale result win, or popping
        // an error dialog for a request nobody's waiting on anymore.
        private int _loadGeneration;

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
            UpcomingEvents = new ObservableCollection<UpcomingEventItem>();
            RecentRentRows = new ObservableCollection<RentLedgerRowViewModel>();
            RecentDocuments = new ObservableCollection<Document>();

            LoadDashboardDataCommand = new RelayCommand(async param =>
            {
                try
                {
                    // Passing true forces a real refresh past the short-lived cache -- used after
                    // an action that actually changed data (recording a payment, adding a house,
                    // etc.), where showing a stale cached snapshot would defeat the point.
                    await LoadDashboardDataAsync(forceRefresh: param is true);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error loading dashboard: {ex.Message}", "Error");
                }
            });
            
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
            _orgChangeDebouncer.Trigger(() => Dispatcher.UIThread.Post(() => LoadDashboardDataCommand.Execute(null)));
        }

        public void Cleanup()
        {
            _orgContext.CurrentOrgChanged -= OnCurrentOrgChanged;

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
                    // Call LoadDashboardDataAsync directly - fire and forget. Forced past the
                    // cache: a payment was just recorded/unrecorded elsewhere, so a stale snapshot
                    // would show wrong numbers right after the exact action meant to update them.
                    _ = LoadDashboardDataAsync(forceRefresh: true);
                }
                else
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        await LoadDashboardDataAsync(forceRefresh: true);
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

        private ObservableCollection<CashFlowMonthPoint> _cashFlowMonths = new();

        /// <summary>Last 6 months of income (recorded rent transactions) vs. expenses, for the
        /// cash-flow chart. Replaced wholesale (not Clear()+Add() in place) on every update --
        /// LineAreaChart binds three separate properties (IncomeValues/ExpenseValues/Labels) off
        /// this same collection via three independent converters, so an in-place Clear() followed
        /// by six individual Add() calls fires seven separate change notifications the three
        /// bindings can transiently observe out of sync with each other (e.g. Labels still empty
        /// from the Clear while IncomeValues has already caught up to an Add) -- if a render lands
        /// in that window, LineAreaChart sees mismatched counts and draws nothing, which is exactly
        /// the "cash flow widget randomly goes blank" symptom reported after rapid tab switching.
        /// A single property replacement fires one notification, so all three bindings always see
        /// the same, fully-populated collection at once.</summary>
        public ObservableCollection<CashFlowMonthPoint> CashFlowMonths
        {
            get => _cashFlowMonths;
            private set => SetProperty(ref _cashFlowMonths, value);
        }

        /// <summary>Rent due soon (next 3 days) and documents expiring soon (next 30 days),
        /// merged and sorted by date. No maintenance/lease-renewal rows -- neither concept exists
        /// anywhere in this product.</summary>
        public ObservableCollection<UpcomingEventItem> UpcomingEvents { get; }

        /// <summary>This month's rent ledger rows (already fetched for the collection breakdown
        /// above), most-recently-due first -- "this month's rent status per tenancy," not a
        /// rolling transaction feed, since the ledger is what actually carries Status/DueDate.</summary>
        public ObservableCollection<RentLedgerRowViewModel> RecentRentRows { get; }

        public ObservableCollection<Document> RecentDocuments { get; }

        /// <summary>Grid.Row for the Upcoming/Quick-actions row -- see RecomputeRowRanks.</summary>
        public int UpcomingRowRank
        {
            get => _upcomingRowRank;
            private set => SetProperty(ref _upcomingRowRank, value);
        }

        /// <summary>Grid.Row for the This month's rent/Recent documents row -- see RecomputeRowRanks.</summary>
        public int RecentActivityRowRank
        {
            get => _recentActivityRowRank;
            private set => SetProperty(ref _recentActivityRowRank, value);
        }

        /// <summary>Grid.Row for the Deposit return reminders row -- see RecomputeRowRanks.</summary>
        public int DepositRemindersRowRank
        {
            get => _depositRemindersRowRank;
            private set => SetProperty(ref _depositRemindersRowRank, value);
        }

        /// <summary>True when there are no deposit return reminders (show placeholder message).</summary>
        public bool ShowEmptyDepositMessage
        {
            get => _showEmptyDepositMessage;
            private set => SetProperty(ref _showEmptyDepositMessage, value);
        }

        private async Task LoadDashboardDataAsync(bool forceRefresh = false)
        {
            var myGeneration = ++_loadGeneration;

            var currentOrgId = _orgContext.CurrentOrgId;
            if (!forceRefresh && currentOrgId.HasValue)
            {
                DashboardSnapshot? cached = null;
                lock (_snapshotCacheLock)
                {
                    if (_snapshotCache.TryGetValue(currentOrgId.Value, out var entry) &&
                        DateTime.UtcNow - entry.CachedAtUtc < SnapshotCacheTtl)
                        cached = entry.Snapshot;
                }
                if (cached != null)
                {
                    AppLogger.Log("Dashboard", $"Using cached snapshot for org {currentOrgId} (skipping ~15 API calls).");
                    if (Dispatcher.UIThread.CheckAccess())
                        UpdateDashboardProperties(cached);
                    else
                        await Dispatcher.UIThread.InvokeAsync(() => UpdateDashboardProperties(cached));
                    return;
                }
            }

            const int maxAttempts = 2;
            var attempt = 0;

            while (true)
            {
                try
                {
                    attempt++;

                    var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                    DateTimeFormatProvider.DateFormat = dateFormat;

                    var currentDate = DateTime.Now;
                    var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
                    var nextMonth = currentDate.Date.AddMonths(1);

                    // All of the reads below are independent of each other -- previously each was
                    // awaited one at a time, meaning this method made ~18 sequential API round
                    // trips before the dashboard could render. That's barely noticeable against a
                    // local dev API but adds up to several real seconds against the production API
                    // (each switch of organisation re-runs this whole method). Firing them
                    // concurrently and awaiting them together cuts wall-clock time to roughly the
                    // single slowest call instead of the sum of all of them.
                    var greetingTask = BuildGreetingTextAsync();
                    var housesTask = _houseService.GetAllHousesAsync();
                    var tenantsTask = _tenantService.GetAllTenantsAsync();
                    // This month's ledger: source for the rent-collection breakdown and the
                    // "recent rent activity" table -- the only current-month ledger fetch needed
                    // now that overdue rent uses the dedicated GetOverdueRentAsync() below instead
                    // of a hand-rolled multi-month scan.
                    var currentMonthLedgerTask = _paymentService.GetRentLedgerForMonthAsync(
                        currentDate.Year, currentDate.Month, null, null, null);
                    // Next month's ledger, needed only for the "rent due soon" window below --
                    // fetched alongside everything else since it doesn't depend on any other
                    // result, rather than as a nested await inside GetRentDueSoon.
                    var nextMonthLedgerTask = _paymentService.GetRentLedgerForMonthAsync(
                        nextMonth.Year, nextMonth.Month, null, null, null);
                    var overdueItemsTask = _paymentService.GetOverdueRentAsync();
                    var depositRemindersTask = _paymentService.GetDepositReturnRemindersAsync();
                    // Documents expiring in the next 30 days -- feeds both the KPI count (fixing
                    // a bug where it was hardcoded to 0 and never actually computed) and the
                    // Upcoming widget's document-expiry rows. One call for both.
                    var expiringDocumentsTask = _documentService.GetExpiringDocumentsAsync(30);
                    // This calendar month's recorded rent transactions.
                    var monthlyIncomeTask = _paymentService.GetTransactionsAsync(monthStart, currentDate, "Rent");
                    var recentDocumentsTask = _documentService.GetDocumentsAsync();
                    var cashFlowMonthsTask = BuildCashFlowMonthsAsync(currentDate);

                    await Task.WhenAll(
                        greetingTask, housesTask, tenantsTask, currentMonthLedgerTask, nextMonthLedgerTask,
                        overdueItemsTask, depositRemindersTask, expiringDocumentsTask, monthlyIncomeTask,
                        recentDocumentsTask, cashFlowMonthsTask);

                    var houses = housesTask.Result;
                    var houseCount = houses.Count();

                    var tenants = tenantsTask.Result;
                    var activeTenantCount = tenants.Count(t => !string.IsNullOrEmpty(t.CurrentHouseAddress));

                    var currentMonthLedger = currentMonthLedgerTask.Result.ToList();

                    var overdueItems = overdueItemsTask.Result.ToList();
                    var overdueRentList = overdueItems.Select(i => new OverdueRentItem
                    {
                        TenantName = i.TenantName,
                        HouseAddress = i.HouseAddress,
                        Amount = i.Amount,
                        DaysLate = i.DaysLate
                    }).ToList();
                    var totalOverdue = overdueItems.Sum(i => i.Amount);

                    var depositRemindersList = depositRemindersTask.Result.ToList();

                    var expiringDocuments = expiringDocumentsTask.Result.ToList();

                    // Rent due in the next 3 days -- same window/logic NotificationFeedService
                    // already uses for the header bell's "Rent due soon" items.
                    var rentDueSoon = GetRentDueSoon(currentDate, currentMonthLedger, nextMonthLedgerTask.Result.ToList());

                    var monthlyIncome = monthlyIncomeTask.Result.Sum(t => t.Amount);

                    var cashFlowMonths = cashFlowMonthsTask.Result;

                    var recentDocuments = recentDocumentsTask.Result
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
                        GreetingText = greetingTask.Result,
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

                    if (currentOrgId.HasValue)
                    {
                        lock (_snapshotCacheLock)
                        {
                            _snapshotCache[currentOrgId.Value] = (DateTime.UtcNow, snapshot);
                        }
                    }

                    if (myGeneration != _loadGeneration)
                    {
                        AppLogger.Log("Dashboard", $"Discarding stale load result (generation {myGeneration}, current is {_loadGeneration}).");
                        return;
                    }

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
                    if (myGeneration != _loadGeneration)
                    {
                        AppLogger.Log("Dashboard", $"Suppressing error dialog for stale/abandoned load (generation {myGeneration}, current is {_loadGeneration}): {ex.Message}");
                        return;
                    }
                    _dialogService.ShowMessage($"Error loading dashboard data: {ex.Message}", "Error");
                    return;
                }
            }
        }

        /// <summary>Ports NotificationFeedService's "rent due soon" rule (next 3 days, Balance > 0)
        /// so the dashboard's Upcoming widget and the notification bell agree on what counts as
        /// due soon. Takes both months' ledgers as already-fetched data (see LoadDashboardDataAsync,
        /// which now fetches them concurrently with everything else) rather than awaiting the
        /// next-month fetch itself.</summary>
        private static List<RentLedgerRowViewModel> GetRentDueSoon(
            DateTime currentDate, List<RentLedgerRowViewModel> currentMonthLedger, List<RentLedgerRowViewModel> nextMonthLedger)
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

            var expenseSummaryTask = _expenseService.GetExpenseSummaryAsync(startDate: earliestMonth, endDate: currentDate);

            var monthRanges = new List<(DateTime Start, DateTime End)>();
            for (var i = monthsBack; i >= 0; i--)
            {
                var monthDate = currentMonthStart.AddMonths(-i);
                var monthEnd = monthDate.AddMonths(1).AddDays(-1);
                if (monthEnd > currentDate)
                    monthEnd = currentDate; // Don't count days that haven't happened yet for the in-progress month.
                monthRanges.Add((monthDate, monthEnd));
            }

            // One call per month, same as before, but fired concurrently instead of one at a time
            // -- this loop alone used to be 6 sequential round trips.
            var incomeTasks = monthRanges
                .Select(r => _paymentService.GetTransactionsAsync(r.Start, r.End, "Rent"))
                .ToList();

            await Task.WhenAll(incomeTasks.Cast<Task>().Append(expenseSummaryTask));

            var expensesByMonth = expenseSummaryTask.Result.ByMonth.ToDictionary(m => (m.Year, m.Month), m => m.Total);

            var points = new List<CashFlowMonthPoint>();
            for (var i = 0; i < monthRanges.Count; i++)
            {
                var monthDate = monthRanges[i].Start;
                var income = incomeTasks[i].Result.Sum(t => t.Amount);
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

        /// <summary>Returns the greeting text rather than setting GreetingText directly, so it can
        /// be carried in DashboardSnapshot and restored on a cache hit like everything else --
        /// previously this set the property as a side effect outside the snapshot, so a cache hit
        /// (which skips this call entirely) left GreetingText stuck at the new instance's
        /// constructor default ("Hello", no name) instead of the real greeting.</summary>
        private async Task<string> BuildGreetingTextAsync()
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

            return string.IsNullOrWhiteSpace(firstName) ? "Hello" : $"Hello, {firstName}";
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
            AppLogger.Log("Dashboard",
                $"Applying snapshot: houses={s.HouseCount}, cashFlowMonths={s.CashFlowMonths.Count} " +
                $"(allZero={s.CashFlowMonths.All(p => p.Income == 0 && p.Expenses == 0)}), " +
                $"upcoming={s.UpcomingEvents.Count}, recentRent={s.RecentRentRows.Count}, recentDocs={s.RecentDocuments.Count}");

            GreetingText = s.GreetingText;
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

            CashFlowMonths = new ObservableCollection<CashFlowMonthPoint>(s.CashFlowMonths);
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

            RecomputeRowRanks(s);
        }

        /// <summary>Reorders the three rows below the fixed Cash flow/Rent collection row so
        /// whichever ones actually have data to show sit above the ones that don't -- a landlord
        /// with no upcoming events shouldn't see an empty "Upcoming" card above a populated
        /// "Recent documents" list. Quick actions is excluded from the "has data" check for its
        /// own row (it's always-populated action buttons, not user data) -- the Upcoming/Quick
        /// actions row's rank follows Upcoming alone. A stable partition (has-data rows keep their
        /// original relative order, then no-data rows keep theirs) rather than a full sort, so two
        /// equally-empty rows don't swap places from run to run for no visible reason.</summary>
        private void RecomputeRowRanks(DashboardSnapshot s)
        {
            var upcomingHasData = s.UpcomingEvents.Count > 0;
            var recentActivityHasData = s.RecentRentRows.Count > 0 || s.RecentDocuments.Count > 0;
            var depositHasData = s.DepositRemindersList.Count > 0;

            var hasData = new[] { upcomingHasData, recentActivityHasData, depositHasData };
            var order = Enumerable.Range(0, 3)
                .OrderBy(i => hasData[i] ? 0 : 1)
                .ToList();

            UpcomingRowRank = order.IndexOf(0);
            RecentActivityRowRank = order.IndexOf(1);
            DepositRemindersRowRank = order.IndexOf(2);
        }

        /// <summary>Everything LoadDashboardDataAsync fetches off the UI thread, bundled so it can
        /// be handed to UpdateDashboardProperties in one Dispatcher call -- replaces the previous
        /// 6-parameter version now that there's meaningfully more state to carry.</summary>
        private sealed class DashboardSnapshot
        {
            public string GreetingText { get; set; } = "Hello";
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
            LoadDashboardDataCommand.Execute(true);
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
            LoadDashboardDataCommand.Execute(true);
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
                LoadDashboardDataCommand.Execute(true);
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
                LoadDashboardDataCommand.Execute(true);
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
                LoadDashboardDataCommand.Execute(true);
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
