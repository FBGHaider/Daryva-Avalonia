using Avalonia.Media;

namespace Daryva.MVVM.Views.Controls.Charts;

/// <summary>One ring segment of a DonutChart. Label isn't drawn by the control itself -- the
/// consuming view renders the legend separately (as real StatusBadge rows), this is just data.</summary>
public sealed record DonutChartSegment(string Label, decimal Value, IBrush Brush);
