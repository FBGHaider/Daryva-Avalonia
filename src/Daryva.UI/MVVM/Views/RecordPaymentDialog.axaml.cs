using Avalonia.Controls;
using Avalonia.Interactivity;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class RecordPaymentDialog : Window
{
    public RecordPaymentDialog()
    {
        InitializeComponent();
    }

    public RecordPaymentDialog(RecordPaymentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close the dialog when the view model requests it
        viewModel.CloseRequested += (_, _) => Close();

        // Load tenancies and other data once the window is opened
        Opened += async (_, _) =>
        {
            if (DataContext is RecordPaymentViewModel vm)
            {
                try
                {
                    await vm.InitializeAsync();
                }
                catch
                {
                    // Let the view model / calling code handle any errors
                }
            }
        };
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
