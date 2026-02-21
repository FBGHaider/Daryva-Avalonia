using System.Windows.Input;
using Avalonia.Threading;
using Daryva.MVVM.Commands;
using Daryva.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace Daryva.MVVM.ViewModels
{
    public class RentPaymentsViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _navigationService;
        private BaseViewModel? _currentTabViewModel;
        private string _selectedTab = "Rent Ledger";
        private int _selectedTabIndex = 0;

        public RentPaymentsViewModel(IServiceProvider serviceProvider, INavigationService navigationService)
        {
            _serviceProvider = serviceProvider;
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Initialize tab ViewModels
            LedgerViewModel = _serviceProvider.GetRequiredService<RentLedgerViewModel>();
            TransactionsViewModel = _serviceProvider.GetRequiredService<TransactionsViewModel>();

            CurrentTabViewModel = LedgerViewModel;
            SelectedTab = "Rent Ledger";

            RecordPaymentCommand = new RelayCommand(_ => ShowRecordPaymentDialog());
            ExportLedgerCommand = new RelayCommand(_ => LedgerViewModel.ExportLedgerCommand.Execute(null));
            RefreshCommand = new RelayCommand(_ =>
            {
                LedgerViewModel.LoadLedgerCommand.Execute(null);
                LedgerViewModel.LoadDepositLedgerCommand.Execute(null);
                TransactionsViewModel.LoadTransactionsCommand.Execute(null);
            });

            // Refresh Rent Ledger and Transactions when a payment is recorded (from any screen)
            DashboardViewModel.PaymentDataChanged += OnPaymentDataChanged;
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
