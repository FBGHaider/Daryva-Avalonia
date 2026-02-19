using Avalonia.Controls;
using Avalonia.Threading;
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
        viewModel.CloseRequested += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => Close());
        };
    }
}
