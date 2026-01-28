using System.Windows.Input;
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
        }

        public ICommand RecordPaymentCommand { get; }
        public ICommand ExportLedgerCommand { get; }

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

        private void ShowRecordPaymentDialog()
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
                    dialog.ShowDialog(mainWindow);
                }
                else
                {
                    dialog.WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterScreen;
                    dialog.Show();
                }
                // Refresh both tabs
                LedgerViewModel.LoadLedgerCommand.Execute(null);
                TransactionsViewModel.LoadTransactionsCommand.Execute(null);
                
                // Refresh dashboard if it's currently displayed
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
