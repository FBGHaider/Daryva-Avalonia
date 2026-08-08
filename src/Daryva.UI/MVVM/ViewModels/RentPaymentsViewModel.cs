using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.Services;
using Daryva.Services.Navigation;
using Daryva.Services.OrgContext;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class RentPaymentsViewModel : BaseViewModel, INavigationAware
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _navigationService;
        private readonly IOrgContext _orgContext;
        private readonly AsyncDebouncer _orgChangeDebouncer = new(TimeSpan.FromMilliseconds(400));
        private BaseViewModel? _currentTabViewModel;
        private string _selectedTab = "Rent Ledger";
        private int _selectedTabIndex = 0;

        public RentPaymentsViewModel(IServiceProvider serviceProvider, INavigationService navigationService, IOrgContext orgContext)
        {
            _serviceProvider = serviceProvider;
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));

            // Initialize tab ViewModels
            LedgerViewModel = _serviceProvider.GetRequiredService<RentLedgerViewModel>();
            TransactionsViewModel = _serviceProvider.GetRequiredService<TransactionsViewModel>();

            CurrentTabViewModel = LedgerViewModel;
            SelectedTab = "Rent Ledger";

            RecordPaymentCommand = new RelayCommand(_ => ShowRecordPaymentDialog());
            ExportLedgerCommand = new RelayCommand(_ => LedgerViewModel.ExportLedgerCommand.Execute(null));
            RefreshCommand = new RelayCommand(_ =>
            {
                // Reload the House/Tenant filter dropdowns too, not just the ledger/transaction rows.
                // Without this, switching org (or a platform admin entering/exiting a Support Session
                // on a different org) left the filter ComboBoxes showing the PREVIOUS org's houses and
                // tenants -- and if a non-"All" house filter was still selected, the ledger could
                // silently show zero rows for a house that isn't even in the new org, with no error.
                LedgerViewModel.LoadHousesCommand.Execute(null);
                LedgerViewModel.LoadLedgerCommand.Execute(null);
                LedgerViewModel.LoadDepositLedgerCommand.Execute(null);
                TransactionsViewModel.LoadHousesCommand.Execute(null);
                TransactionsViewModel.LoadTenantsCommand.Execute(null);
                TransactionsViewModel.LoadTransactionsCommand.Execute(null);
            });

            _orgContext.CurrentOrgChanged += OnCurrentOrgChanged;

            // Refresh Rent Ledger and Transactions when a payment is recorded (from any screen)
            DashboardViewModel.PaymentDataChanged += OnPaymentDataChanged;
        }

        private void OnCurrentOrgChanged(object? sender, CurrentOrgChangedEventArgs e)
        {
            _orgChangeDebouncer.Trigger(() => Dispatcher.UIThread.Post(() => RefreshCommand.Execute(null)));
        }

        public void Cleanup()
        {
            _orgContext.CurrentOrgChanged -= OnCurrentOrgChanged;
            // PaymentDataChanged is a static event on DashboardViewModel -- without this, every
            // past RentPaymentsViewModel instance stays subscribed for the rest of the app's life
            // and reacts to payment changes recorded from any other screen.
            DashboardViewModel.PaymentDataChanged -= OnPaymentDataChanged;
            // LedgerViewModel/TransactionsViewModel are owned sub-tabs, not separately navigated to
            // -- NavigationService only flips IsActive on the page it directly knows about (this
            // one), so their own abandoned-load error dialogs need this to cascade explicitly.
            LedgerViewModel.IsActive = false;
            TransactionsViewModel.IsActive = false;
        }

        private void OnPaymentDataChanged(object? sender, EventArgs e)
        {
            void Refresh()
            {
                LedgerViewModel.LoadLedgerCommand.Execute(null);
                // Explicitly refresh deposit ledger so it updates even if rent ledger load fails before reaching it
                LedgerViewModel.LoadDepositLedgerCommand.Execute(null);
                TransactionsViewModel.LoadTransactionsCommand.Execute(null);
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Refresh();
            }
            else
            {
                Dispatcher.UIThread.Post(Refresh);
            }
        }

        public ICommand RecordPaymentCommand { get; }
        public ICommand ExportLedgerCommand { get; }
        public ICommand RefreshCommand { get; }

        public RentLedgerViewModel LedgerViewModel { get; }
        public TransactionsViewModel TransactionsViewModel { get; }

        public BaseViewModel? CurrentTabViewModel
        {
            get => _currentTabViewModel;
            set => SetProperty(ref _currentTabViewModel, value);
        }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (SetProperty(ref _selectedTab, value))
                {
                    CurrentTabViewModel = value == "Rent Ledger" ? LedgerViewModel : TransactionsViewModel;
                    SelectedTabIndex = value == "Rent Ledger" ? 0 : 1;
                }
            }
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    SelectedTab = value == 0 ? "Rent Ledger" : "Transactions";
                    CurrentTabViewModel = value == 0 ? LedgerViewModel : TransactionsViewModel;
                }
            }
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
                // Refresh after dialog closes so rent and deposit ledger show new payments
                LedgerViewModel.LoadLedgerCommand.Execute(null);
                LedgerViewModel.LoadDepositLedgerCommand.Execute(null);
                TransactionsViewModel.LoadTransactionsCommand.Execute(null);
                RefreshDashboardIfActive();
            }
            catch
            {
                // Error opening dialog - ignore
            }
        }

        private void RefreshDashboardIfActive()
        {
            try
            {
                DashboardViewModel.NotifyPaymentDataChanged();
            }
            catch
            {
                // Error refreshing dashboard - ignore
            }
        }
    }
}
