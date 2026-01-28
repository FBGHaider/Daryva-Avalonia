using Avalonia;
using Avalonia.Controls;

namespace Daryva.MVVM.Views;

public partial class DocumentsView : UserControl
{
    private const double HideUploadThreshold = 400;

    public static readonly StyledProperty<bool> IsUploadButtonVisibleProperty =
        AvaloniaProperty.Register<DocumentsView, bool>(nameof(IsUploadButtonVisible), true);

    public bool IsUploadButtonVisible
    {
        get => GetValue(IsUploadButtonVisibleProperty);
        set => SetValue(IsUploadButtonVisibleProperty, value);
    }

    public DocumentsView()
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
                IsUploadButtonVisible = w >= HideUploadThreshold;
        }
    }

    private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var w = e.NewSize.Width;
        IsUploadButtonVisible = w >= HideUploadThreshold;
    }
}
