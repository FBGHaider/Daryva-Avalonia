using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace LandLordBuddy.MVVM.Views
{
    public partial class RentLedgerView : System.Windows.Controls.UserControl
    {
        public RentLedgerView()
        {
            InitializeComponent();
            Loaded += RentLedgerView_Loaded;
        }

        private async void RentLedgerView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ViewModels.RentLedgerViewModel viewModel)
                {
                    // Load houses first
                    await Task.Run(async () =>
                    {
                        await Task.Delay(50); // Small delay to let UI render first
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            viewModel.LoadHousesCommand.Execute(null);
                        });
                    });
                    
                    // Set default to "All Houses" (HouseId = 0) after houses are loaded
                    await Task.Delay(100);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (viewModel.SelectedHouseId == null && viewModel.Houses.Count > 0)
                        {
                            viewModel.SelectedHouseId = 0; // "All Houses"
                        }
                        // Load ledger data
                        viewModel.LoadLedgerCommand.Execute(null);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading rent ledger: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void MonthComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.RentLedgerViewModel viewModel && sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedIndex >= 0)
            {
                viewModel.SelectedMonth = comboBox.SelectedIndex + 1;
            }
        }

        private void YearComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.RentLedgerViewModel viewModel && sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is int year)
            {
                viewModel.SelectedYear = year;
            }
        }
    }
}
