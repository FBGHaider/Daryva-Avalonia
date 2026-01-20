using System.Windows;
using System.Windows.Controls;

namespace FBGRentora.MVVM.Views
{
    public partial class ExpensesView : System.Windows.Controls.UserControl
    {
        public ExpensesView()
        {
            InitializeComponent();
        }

        private void ExpensesView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ExpensesViewModel viewModel)
            {
                viewModel.LoadExpensesCommand.Execute(null);
                viewModel.LoadHousesCommand.Execute(null);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.ExpensesViewModel viewModel && sender is TabControl tabControl)
            {
                viewModel.SelectedTab = tabControl.SelectedIndex == 0 ? "List" : "Summary";
            }
        }
    }
}
