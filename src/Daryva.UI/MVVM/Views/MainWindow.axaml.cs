using Avalonia.Controls;
using Daryva.MVVM.ViewModels;
using System.IO;

namespace Daryva.MVVM.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TrySetWindowIcon();
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

    private void TrySetWindowIcon()
    {
        var exeIconPath = Path.Combine(AppContext.BaseDirectory, "Daryva.exe");
        if (TryApplyIcon(exeIconPath))
        {
            return;
        }

        var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logo", "Daryva_icon.ico");
        _ = TryApplyIcon(icoPath);
    }

    private bool TryApplyIcon(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            Icon = new WindowIcon(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
