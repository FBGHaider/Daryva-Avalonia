using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Daryva.MVVM.Views.Controls;

public partial class KpiCard : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<KpiCard, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<KpiCard, string>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string> SubTextProperty =
        AvaloniaProperty.Register<KpiCard, string>(nameof(SubText), string.Empty);

    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<KpiCard, string>(nameof(Icon), string.Empty);

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<KpiCard, IBrush?>(nameof(AccentBrush));

    static KpiCard()
    {
        SubTextProperty.Changed.AddClassHandler<KpiCard>((card, _) =>
            card.RaisePropertyChanged(HasSubTextProperty, false, card.HasSubText));

        IconProperty.Changed.AddClassHandler<KpiCard>((card, _) =>
            card.RaisePropertyChanged(HasIconProperty, false, card.HasIcon));
    }

    public KpiCard()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string SubText
    {
        get => GetValue(SubTextProperty);
        set => SetValue(SubTextProperty, value);
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    private static readonly DirectProperty<KpiCard, bool> HasSubTextProperty =
        AvaloniaProperty.RegisterDirect<KpiCard, bool>(nameof(HasSubText), card => card.HasSubText);

    public bool HasSubText => !string.IsNullOrWhiteSpace(SubText);

    private static readonly DirectProperty<KpiCard, bool> HasIconProperty =
        AvaloniaProperty.RegisterDirect<KpiCard, bool>(nameof(HasIcon), card => card.HasIcon);

    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);
}
