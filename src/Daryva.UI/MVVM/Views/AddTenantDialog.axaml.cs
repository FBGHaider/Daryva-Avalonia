using Avalonia.Controls;
using Avalonia.Interactivity;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class AddTenantDialog : Window
{
    public AddTenantDialog()
    {
        InitializeComponent();
    }

    public AddTenantDialog(AddTenantViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
