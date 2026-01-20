using System.Windows;
using System.Windows.Controls;

namespace FBGRentora.MVVM.Views
{
    public partial class NotificationsView : System.Windows.Controls.UserControl
    {
        public NotificationsView()
        {
            InitializeComponent();
        }

        private void NotificationsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.NotificationsViewModel viewModel)
            {
                // Only load if not already loaded (prevent duplicate loading)
                if (!viewModel.Templates.Any())
                {
                    viewModel.LoadTemplatesCommand.Execute(null);
                }
                if (!viewModel.Tenants.Any())
                {
                    viewModel.LoadTenantsCommand.Execute(null);
                }
                if (!viewModel.Houses.Any())
                {
                    viewModel.LoadHousesCommand.Execute(null);
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ViewModels.NotificationsViewModel viewModel && sender is TabControl tabControl)
            {
                viewModel.SelectedTab = tabControl.SelectedIndex == 0 ? "Compose" : (tabControl.SelectedIndex == 1 ? "Queue" : "History");
            }
        }
    }
}

