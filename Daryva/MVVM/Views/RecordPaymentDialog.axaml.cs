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
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
