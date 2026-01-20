using System.Windows;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views
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
