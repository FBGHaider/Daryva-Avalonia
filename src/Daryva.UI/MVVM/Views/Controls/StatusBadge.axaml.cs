using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Daryva.MVVM.Models;

namespace Daryva.MVVM.Views.Controls;

public partial class StatusBadge : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<StatusBadge, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<BadgeSeverity> SeverityProperty =
        AvaloniaProperty.Register<StatusBadge, BadgeSeverity>(nameof(Severity), BadgeSeverity.Neutral);

    static StatusBadge()
    {
        SeverityProperty.Changed.AddClassHandler<StatusBadge>((badge, _) => badge.ApplySeverityClass());
    }

    public StatusBadge()
    {
        AvaloniaXamlLoader.Load(this);
        ApplySeverityClass();
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeSeverity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    private void ApplySeverityClass()
    {
        Classes.Set("severity-success", Severity == BadgeSeverity.Success);
        Classes.Set("severity-warning", Severity == BadgeSeverity.Warning);
        Classes.Set("severity-danger", Severity == BadgeSeverity.Danger);
        Classes.Set("severity-info", Severity == BadgeSeverity.Info);
        Classes.Set("severity-purple", Severity == BadgeSeverity.Purple);
        Classes.Set("severity-neutral", Severity == BadgeSeverity.Neutral);
    }
}
