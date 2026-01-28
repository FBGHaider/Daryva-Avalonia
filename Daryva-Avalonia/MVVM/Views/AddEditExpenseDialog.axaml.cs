using Avalonia.Controls;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class AddEditExpenseDialog : Window
{
    public AddEditExpenseDialog()
    {
        InitializeComponent();
    }

    public AddEditExpenseDialog(AddEditExpenseViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
