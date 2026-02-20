using Avalonia.Controls;
using Daryva.MVVM.ViewModels;
using System.ComponentModel;
using System.IO;

namespace Daryva.MVVM.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _mainViewModel;
    private OnboardingViewModel? _onboardingViewModel;

    public MainWindow()
    {
        InitializeComponent();
        TrySetWindowIcon();
        DataContextChanged += OnDataContextChanged;
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        DetachOnboardingViewModel();
        DetachMainViewModel();

        if (DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.Cleanup();
        }
        base.OnClosed(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachMainViewModel(DataContext as MainViewModel);
        UpdateWindowForAuthScene();
    }

    private void AttachMainViewModel(MainViewModel? vm)
    {
        if (ReferenceEquals(_mainViewModel, vm))
            return;

        DetachMainViewModel();
        _mainViewModel = vm;

        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;
            AttachOnboardingViewModel(_mainViewModel.CurrentViewModel as OnboardingViewModel);
        }
    }

    private void DetachMainViewModel()
    {
        if (_mainViewModel == null)
            return;

        _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        _mainViewModel = null;
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentViewModel))
        {
            AttachOnboardingViewModel(_mainViewModel?.CurrentViewModel as OnboardingViewModel);
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentViewModel) ||
            e.PropertyName == nameof(MainViewModel.IsOnboardingMode))
        {
            UpdateWindowForAuthScene();
        }
    }

    private void AttachOnboardingViewModel(OnboardingViewModel? vm)
    {
        if (ReferenceEquals(_onboardingViewModel, vm))
            return;

        DetachOnboardingViewModel();
        _onboardingViewModel = vm;

        if (_onboardingViewModel != null)
        {
            _onboardingViewModel.PropertyChanged += OnOnboardingViewModelPropertyChanged;
        }
    }

    private void DetachOnboardingViewModel()
    {
        if (_onboardingViewModel == null)
            return;

        _onboardingViewModel.PropertyChanged -= OnOnboardingViewModelPropertyChanged;
        _onboardingViewModel = null;
    }

    private void OnOnboardingViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnboardingViewModel.ShowAuthSection) ||
            e.PropertyName == nameof(OnboardingViewModel.IsAuthenticated) ||
            e.PropertyName == nameof(OnboardingViewModel.ShowRegisterScene) ||
            e.PropertyName == nameof(OnboardingViewModel.ShowVerifyEmailScene))
        {
            UpdateWindowForAuthScene();
        }
    }

    private void UpdateWindowForAuthScene()
    {
        var showCompactAuthWindow = _mainViewModel?.IsOnboardingMode == true &&
                                    _onboardingViewModel?.ShowAuthSection == true;

        if (showCompactAuthWindow)
        {
            var compactHeight = _onboardingViewModel?.ShowRegisterScene == true
                ? 740
                : _onboardingViewModel?.ShowVerifyEmailScene == true
                    ? 560
                    : 620;

            Width = 520;
            Height = compactHeight;
            MinWidth = 520;
            MaxWidth = 520;
            MinHeight = compactHeight;
            MaxHeight = compactHeight;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Width = 1200;
        Height = 800;
        MinWidth = 900;
        MaxWidth = double.PositiveInfinity;
        MinHeight = 650;
        MaxHeight = double.PositiveInfinity;
        CanResize = true;
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
