using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.MVVM.Models;
using Daryva.Services.Business;
using Daryva.Services.Data;
using Daryva.Services.Dialog;
using Daryva.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class TransactionsViewModel : BaseViewModel
    {
        private readonly IPaymentService _paymentService;
        private readonly IHouseService _houseService;
        private readonly ITenantService _tenantService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly INavigationService _navigationService;
        private readonly ISettingsService _settingsService;

        private string _dateRangeFilter = "This Month";
        private string _paymentTypeFilter = "All";
        private int? _selectedHouseId = 0; // Default to "All Houses"
        private int? _selectedTenantId = 0; // Default to "All Tenants"
        private string _methodFilter = "All";
        private TransactionRowViewModel? _selectedTransaction;

        public TransactionsViewModel(
            IPaymentService paymentService,
            IHouseService houseService,
            ITenantService tenantService,
            IServiceProvider serviceProvider,
            IDialogService dialogService,
            INavigationService navigationService,
            ISettingsService settingsService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            Transactions = new ObservableCollection<TransactionRowViewModel>();
            RentTransactions = new ObservableCollection<TransactionRowViewModel>();
            DepositTransactions = new ObservableCollection<TransactionRowViewModel>();
            Houses = new ObservableCollection<House>();
            Tenants = new ObservableCollection<Tenant>();
            DateRangeFilterOptions = new ObservableCollection<string> { "All", "This Month", "Last Month", "Last 3 Months", "Last 12 Months", "All Time" };
            PaymentTypeFilterOptions = new ObservableCollection<string> { "All", "Rent", "Deposit" };
            MethodFilterOptions = new ObservableCollection<string> { "All", "Bank Transfer", "Cash", "Card", "Other" };

            LoadTransactionsCommand = new RelayCommand(async _ => await LoadTransactionsAsync());
            LoadHousesCommand = new RelayCommand(async _ => await LoadHousesAsync());
            LoadTenantsCommand = new RelayCommand(async _ => await LoadTenantsAsync());
            ViewTransactionCommand = new RelayCommand(_ => ViewTransactionDetails(), _ => SelectedTransaction != null);
            UnrecordPaymentCommand = new RelayCommand(async _ => await UnrecordPaymentAsync(), _ => SelectedTransaction != null);
            DeleteAllTransactionsCommand = new RelayCommand(async _ => await DeleteAllTransactionsAsync()); // For testing

            LoadHousesCommand.Execute(null);
            LoadTenantsCommand.Execute(null);
            LoadTransactionsCommand.Execute(null);
        }

        public ICommand LoadTransactionsCommand { get; }
        public ICommand LoadHousesCommand { get; }
        public ICommand LoadTenantsCommand { get; }
        public ICommand ViewTransactionCommand { get; }
        public ICommand UnrecordPaymentCommand { get; }
        public ICommand DeleteAllTransactionsCommand { get; } // For testing

        public ObservableCollection<TransactionRowViewModel> Transactions { get; }
        public ObservableCollection<TransactionRowViewModel> RentTransactions { get; }
        public ObservableCollection<TransactionRowViewModel> DepositTransactions { get; }
        public ObservableCollection<House> Houses { get; }
        public ObservableCollection<Tenant> Tenants { get; }
        public ObservableCollection<string> DateRangeFilterOptions { get; }
        public ObservableCollection<string> PaymentTypeFilterOptions { get; }
        public ObservableCollection<string> MethodFilterOptions { get; }

        public string DateRangeFilter
        {
            get => _dateRangeFilter;
            set
            {
                if (SetProperty(ref _dateRangeFilter, value))
                {
                    LoadTransactionsCommand.Execute(null);
                }
            }
        }

        public string PaymentTypeFilter
        {
            get => _paymentTypeFilter;
            set
            {
                if (SetProperty(ref _paymentTypeFilter, value))
                {
                    LoadTransactionsCommand.Execute(null);
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
                    LoadTransactionsCommand.Execute(null);
                }
            }
        }

        public int? SelectedTenantId
        {
            get => _selectedTenantId;
            set
            {
                if (SetProperty(ref _selectedTenantId, value))
                {
                    LoadTransactionsCommand.Execute(null);
                }
            }
        }

        public string MethodFilter
        {
            get => _methodFilter;
            set
            {
                if (SetProperty(ref _methodFilter, value))
                {
                    LoadTransactionsCommand.Execute(null);
                }
            }
        }

        public TransactionRowViewModel? SelectedTransaction
        {
            get => _selectedTransaction;
            set
            {
                if (SetProperty(ref _selectedTransaction, value))
                {
                    ((RelayCommand)ViewTransactionCommand).RaiseCanExecuteChanged();
                    ((RelayCommand)UnrecordPaymentCommand).RaiseCanExecuteChanged();
                }
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
                    Houses.Add(new House { HouseId = 0, AddressLine1 = "All Houses" });
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

        private async Task LoadTenantsAsync()
        {
            try
            {
                // Run database query on background thread
                var tenants = await Task.Run(async () => await _tenantService.GetAllTenantsAsync()).ConfigureAwait(false);
                
                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Tenants.Clear();
                    Tenants.Add(new Tenant { TenantId = 0, FullName = "All Tenants" });
                    foreach (var tenant in tenants)
                    {
                        Tenants.Add(tenant);
                    }
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error loading tenants: {ex.Message}", "Error");
            }
        }

        private async Task LoadTransactionsAsync()
        {
            try
            {
                DateTime? startDate = null;
                DateTime? endDate = null;

                // Calculate date range based on filter
                var now = DateTime.Now;
                switch (DateRangeFilter)
                {
                    case "This Month":
                        startDate = new DateTime(now.Year, now.Month, 1);
                        endDate = startDate.Value.AddMonths(1).AddDays(-1);
                        break;
                    case "Last Month":
                        startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                        endDate = startDate.Value.AddMonths(1).AddDays(-1);
                        break;
                    case "Last 3 Months":
                        startDate = now.AddMonths(-3);
                        endDate = now;
                        break;
                    case "Last 12 Months":
                        startDate = now.AddMonths(-12);
                        endDate = now;
                        break;
                    case "This Year":
                        startDate = new DateTime(now.Year, 1, 1);
                        endDate = new DateTime(now.Year, 12, 31);
                        break;
                    case "All":
                    case "All Time":
                        // No date filter
                        break;
                }

                // Convert filter values - they should already be strings from SelectedValue binding
                string? paymentTypeValue = PaymentTypeFilter == "All" ? null : PaymentTypeFilter;
                string? methodValue = MethodFilter == "All" ? null : MethodFilter;
                
                // Convert IDs: 0 means "All" (pass null)
                int? houseIdFilter = (SelectedHouseId == null || SelectedHouseId == 0) ? null : SelectedHouseId;
                int? tenantIdFilter = (SelectedTenantId == null || SelectedTenantId == 0) ? null : SelectedTenantId;
                
                // Run database query on background thread
                var transactions = await Task.Run(async () => await _paymentService.GetTransactionsAsync(
                    startDate,
                    endDate,
                    paymentTypeValue == "All" ? null : paymentTypeValue,
                    houseIdFilter,
                    tenantIdFilter,
                    methodValue == "All" ? null : methodValue)).ConfigureAwait(false);

                var dateFormat = await _settingsService.GetSettingAsync("DateFormat", "dd/MM/yyyy") ?? "dd/MM/yyyy";
                Daryva.Services.DateTimeFormatProvider.DateFormat = dateFormat;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Transactions.Clear();
                    RentTransactions.Clear();
                    DepositTransactions.Clear();
                    foreach (var transaction in transactions)
                    {
                        transaction.PaidOnDisplay = Daryva.Services.DateTimeFormatProvider.FormatDate(transaction.PaidOn);
                        Transactions.Add(transaction);
                        if (transaction.PaymentType == "Rent")
                            RentTransactions.Add(transaction);
                        else if (transaction.PaymentType == "Deposit")
                            DepositTransactions.Add(transaction);
                    }
                });
            }
            catch (Exception ex)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _dialogService.ShowMessage($"Error loading transactions: {ex.Message}", "Error");
                });
            }
        }

        private void ViewTransactionDetails()
        {
            if (SelectedTransaction == null) return;

            // Payment details view can be added in future version
            _dialogService.ShowMessage($"Viewing transaction: {SelectedTransaction.PaymentType} payment of £{SelectedTransaction.Amount:N2} on {SelectedTransaction.PaidOnDisplay}", "Transaction Details");
        }

        private void RefreshDashboardIfActive()
        {
            // Notify all DashboardViewModel instances to refresh using static event
            DashboardViewModel.NotifyPaymentDataChanged();
        }

        private async Task UnrecordPaymentAsync()
        {
            if (SelectedTransaction == null) return;

            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                $"Are you sure you want to unrecord this {SelectedTransaction.PaymentType.ToLower()} payment of £{SelectedTransaction.Amount:N2}?\n\nThis action cannot be undone.",
                "Confirm Unrecord Payment");

            if (!confirmed)
                return;

            try
            {
                var success = await _paymentService.UnrecordPaymentAsync(SelectedTransaction.PaymentId, SelectedTransaction.PaymentType);
                
                if (success)
                {
                    _dialogService.ShowMessage("Payment unrecorded successfully.", "Success");
                    
                    // Refresh transactions list
                    await LoadTransactionsAsync();
                    
                    // Refresh ledger if RentPaymentsViewModel is active
                    var rentPaymentsViewModel = _navigationService.GetViewModel<RentPaymentsViewModel>();
                    if (rentPaymentsViewModel != null)
                    {
                        rentPaymentsViewModel.LedgerViewModel.LoadLedgerCommand.Execute(null);
                    }
                    
                    // Refresh dashboard using the same method as other refresh calls
                    RefreshDashboardIfActive();
                }
                else
                {
                    _dialogService.ShowMessage("Failed to unrecord payment. Please try again.", "Error");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error unrecording payment: {ex.Message}", "Error");
            }
        }

        private async Task DeleteAllTransactionsAsync()
        {
            var requireConfirm = await _settingsService.GetSettingAsync<bool>("ConfirmDestructiveActions", true) ?? true;
            var confirmed = !requireConfirm || await _dialogService.ShowConfirmationAsync(
                "⚠️ WARNING: This will DELETE ALL transactions (rent and deposit payments) from the database!\n\n" +
                "This action cannot be undone. Are you absolutely sure you want to continue?\n\n" +
                "This is a TESTING feature only.",
                "Confirm Delete All Transactions");

            if (!confirmed)
                return;

            try
            {
                var success = await _paymentService.DeleteAllTransactionsAsync();
                
                if (success)
                {
                    _dialogService.ShowMessage("All transactions have been deleted successfully.", "Success");
                    
                    // Refresh transactions list
                    await LoadTransactionsAsync();
                    
                    // Refresh ledger if RentPaymentsViewModel is active
                    var rentPaymentsViewModel = _navigationService.GetViewModel<RentPaymentsViewModel>();
                    if (rentPaymentsViewModel != null)
                    {
                        rentPaymentsViewModel.LedgerViewModel.LoadLedgerCommand.Execute(null);
                    }
                    
                    // Refresh dashboard
                    RefreshDashboardIfActive();
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error deleting all transactions: {ex.Message}", "Error");
            }
        }
    }
}
