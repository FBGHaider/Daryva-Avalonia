using Avalonia;
using Avalonia.Controls;

namespace Daryva.MVVM.Views;

public partial class RentPaymentsView : UserControl
{
    private const double HideHeaderButtonsThreshold = 400;

    public static readonly StyledProperty<bool> IsHeaderButtonsVisibleProperty =
        AvaloniaProperty.Register<RentPaymentsView, bool>(nameof(IsHeaderButtonsVisible), true);

    public bool IsHeaderButtonsVisible
    {
        get => GetValue(IsHeaderButtonsVisibleProperty);
        set => SetValue(IsHeaderButtonsVisibleProperty, value);
    }

    public RentPaymentsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var header = this.FindControl<Border>("HeaderBorder");
        if (header != null)
        {
            header.SizeChanged += OnHeaderSizeChanged;
            var w = header.Bounds.Width;
            if (w > 0)
                IsHeaderButtonsVisible = w >= HideHeaderButtonsThreshold;
        }
    }

    private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var w = e.NewSize.Width;
        IsHeaderButtonsVisible = w >= HideHeaderButtonsThreshold;
    }
}
