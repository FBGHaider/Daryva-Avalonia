using System.Windows;
using System.Threading.Tasks;

namespace LandLordBuddy.MVVM.Views
{
    public partial class DocumentsView : System.Windows.Controls.UserControl
    {
        public DocumentsView()
        {
            InitializeComponent();
            Loaded += DocumentsView_Loaded;
        }

        private async void DocumentsView_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ViewModels.DocumentsViewModel viewModel)
                {
                    await Task.Run(async () =>
                    {
                        await Task.Delay(50);
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            viewModel.LoadDocumentStatusCommand.Execute(null);
                            viewModel.LoadDocumentsCommand.Execute(null);
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading documents: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
