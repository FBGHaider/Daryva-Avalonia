using System.Windows.Input;
using FBGRentora.MVVM.Commands;
using FBGRentora.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace FBGRentora.MVVM.ViewModels
{
    public class RentPaymentsViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly INavigationService _navigationService;
        private BaseViewModel? _currentTabViewModel;
        private string _selectedTab = "Ledger";

        public RentPaymentsViewModel(IServiceProvider serviceProvider, INavigationService navigationService)
        {
            _serviceProvider = serviceProvider;
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // Initialize tab ViewModels
            LedgerViewModel = _serviceProvider.GetRequiredService<RentLedgerViewModel>();
            TransactionsViewModel = _serviceProvider.GetRequiredService<TransactionsViewModel>();

            CurrentTabViewModel = LedgerViewModel;

            RecordPaymentCommand = new RelayCommand(_ => ShowRecordPaymentDialog());
            ExportLedgerCommand = new RelayCommand(_ => { /* Export ledger */ });
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
                    CurrentTabViewModel = value == "Ledger" ? LedgerViewModel : TransactionsViewModel;
                }
            }
        }

        private void ShowRecordPaymentDialog()
        {
            try
            {
                var viewModel = _serviceProvider.GetRequiredService<RecordPaymentViewModel>();
                var dialog = new MVVM.Views.RecordPaymentDialog(viewModel);
                dialog.Owner = System.Windows.Application.Current.MainWindow;
                if (dialog.ShowDialog() == true)
                {
                    // Refresh both tabs
                    LedgerViewModel.LoadLedgerCommand.Execute(null);
                    TransactionsViewModel.LoadTransactionsCommand.Execute(null);
                    
                    // Refresh dashboard if it's currently displayed
                    RefreshDashboardIfActive();
                }
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
