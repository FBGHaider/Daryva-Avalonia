using Avalonia.Controls;
using Avalonia.Interactivity;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class EditTenantDialog : Window
{
    public EditTenantDialog()
    {
        InitializeComponent();
    }

    public EditTenantDialog(EditTenantViewModel viewModel)
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
