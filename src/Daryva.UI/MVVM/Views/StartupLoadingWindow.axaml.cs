using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Daryva.MVVM.Views;

public partial class StartupLoadingWindow : Window
{
    private DispatcherTimer? _spinnerTimer;
    private double _spinnerAngle;

    public StartupLoadingWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        _spinnerTimer = new DispatcherTimer
        {
            Interval = System.TimeSpan.FromMilliseconds(16)
        };

        _spinnerTimer.Tick += (_, _) =>
        {
            _spinnerAngle = (_spinnerAngle + 3.5) % 360;
            if (SpinnerArc.RenderTransform is RotateTransform rotate)
            {
                rotate.Angle = _spinnerAngle;
            }
        };

        _spinnerTimer.Start();
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (_spinnerTimer is null)
            return;

        _spinnerTimer.Stop();
        _spinnerTimer = null;
    }
}
