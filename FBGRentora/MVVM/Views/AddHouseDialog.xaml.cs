using System.Windows;
using FBGRentora.MVVM.ViewModels;

namespace FBGRentora.MVVM.Views
{
    public partial class AddHouseDialog : Window
    {
        public AddHouseDialog(AddHouseViewModel viewModel)
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
