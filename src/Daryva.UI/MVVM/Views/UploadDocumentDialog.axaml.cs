using Avalonia.Controls;
using Avalonia.Threading;
using Daryva.MVVM.ViewModels;

namespace Daryva.MVVM.Views;

public partial class UploadDocumentDialog : Window
{
    public UploadDocumentDialog()
    {
        InitializeComponent();
    }

    public UploadDocumentDialog(UploadDocumentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Dispatcher.UIThread.Post(() => Close());
    }
}
