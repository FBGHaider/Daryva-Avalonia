using System.Windows;
using LandLordBuddy.MVVM.ViewModels;

namespace LandLordBuddy.MVVM.Views
{
    public partial class AddTenantDialog : Window
    {
        public AddTenantDialog(AddTenantViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += (s, e) => DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
