using Avalonia.Controls;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Cleanup();
        }
        base.OnClosed(e);
    }
}
