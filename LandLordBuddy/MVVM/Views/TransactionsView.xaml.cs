using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LandLordBuddy.MVVM.ViewModels;

namespace LandLordBuddy.MVVM.Views
{
    public partial class TransactionsView : System.Windows.Controls.UserControl
    {
        public TransactionsView()
        {
            InitializeComponent();
            Loaded += TransactionsView_Loaded;
        }

        private async void TransactionsView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is TransactionsViewModel viewModel)
                {
                    // Load houses and tenants first
                    await Task.Run(async () =>
                    {
                        await Task.Delay(50); // Small delay to let UI render first
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            viewModel.LoadHousesCommand.Execute(null);
                            viewModel.LoadTenantsCommand.Execute(null);
                        });
                    });
                    
                    // Set defaults to "All" after houses and tenants are loaded
                    await Task.Delay(100);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (viewModel.SelectedHouseId == null && viewModel.Houses.Count > 0)
                        {
                            viewModel.SelectedHouseId = 0; // "All Houses"
                        }
                        if (viewModel.SelectedTenantId == null && viewModel.Tenants.Count > 0)
                        {
                            viewModel.SelectedTenantId = 0; // "All Tenants"
                        }
                        // Load transactions data
                        viewModel.LoadTransactionsCommand.Execute(null);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading transactions: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is TransactionsViewModel viewModel && viewModel.SelectedTransaction != null)
            {
                viewModel.ViewTransactionCommand.Execute(null);
            }
        }
    }
}
