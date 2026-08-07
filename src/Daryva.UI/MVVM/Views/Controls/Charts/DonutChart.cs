using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Daryva.MVVM.Views.Controls.Charts;

/// <summary>
/// Minimal donut/ring chart (e.g. rent collection Paid/Pending/Overdue breakdown). Hand-rolled
/// for the same reason as LineAreaChart -- segment count/proportions are data-driven, so each
/// segment is stroked as an arc (StreamGeometryContext.ArcTo) with a wide Pen rather than filled
/// pie wedges, the standard technique for a ring (not pie) in immediate-mode 2D drawing. Small
/// gaps between segments give separation without needing borders. Legend text is rendered by the
/// consuming view (as real StatusBadge rows), not drawn inside this control.
/// </summary>
public class DonutChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<DonutChartSegment>> SegmentsProperty =
        AvaloniaProperty.Register<DonutChart, IReadOnlyList<DonutChartSegment>>(nameof(Segments), Array.Empty<DonutChartSegment>());

    public static readonly StyledProperty<string> CenterLabelProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(CenterLabel), string.Empty);

    public static readonly StyledProperty<string> CenterSubLabelProperty =
        AvaloniaProperty.Register<DonutChart, string>(nameof(CenterSubLabel), string.Empty);

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(LabelBrush));

    public static readonly StyledProperty<IBrush?> SubLabelBrushProperty =
        AvaloniaProperty.Register<DonutChart, IBrush?>(nameof(SubLabelBrush));

    private const double StrokeThickness = 15;
    private const double GapDegrees = 3;

    static DonutChart()
    {
        AffectsRender<DonutChart>(SegmentsProperty, CenterLabelProperty, CenterSubLabelProperty,
            TrackBrushProperty, LabelBrushProperty, SubLabelBrushProperty);
    }

    public IReadOnlyList<DonutChartSegment> Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public string CenterLabel
    {
        get => GetValue(CenterLabelProperty);
        set => SetValue(CenterLabelProperty, value);
    }

    public string CenterSubLabel
    {
        get => GetValue(CenterSubLabelProperty);
        set => SetValue(CenterSubLabelProperty, value);
    }

    public IBrush? TrackBrush
    {
        get => GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public IBrush? SubLabelBrush
    {
        get => GetValue(SubLabelBrushProperty);
        set => SetValue(SubLabelBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
            return;

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2 - StrokeThickness / 2 - 2;
        if (radius <= 0)
            return;

        var trackPen = new Pen(TrackBrush ?? Brushes.Gray, StrokeThickness);
        context.DrawEllipse(null, trackPen, center, radius, radius);

        var segments = Segments;
        var total = segments.Sum(s => s.Value);
        if (total > 0)
        {
            var angle = -90d; // Start at 12 o'clock, sweep clockwise.
            foreach (var segment in segments)
            {
                if (segment.Value <= 0)
                    continue;
                var sweep = (double)(segment.Value / total) * 360d;
                DrawArcSegment(context, center, radius, angle, Math.Max(0, sweep - GapDegrees), segment.Brush);
                angle += sweep;
            }
        }

        var labelBrush = LabelBrush ?? Brushes.White;
        var boldTypeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        if (!string.IsNullOrEmpty(CenterLabel))
        {
            var text = new FormattedText(CenterLabel, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, boldTypeface, 24, labelBrush);
            var y = string.IsNullOrEmpty(CenterSubLabel) ? center.Y - text.Height / 2 : center.Y - text.Height - 1;
            context.DrawText(text, new Point(center.X - text.Width / 2, y));
        }
        if (!string.IsNullOrEmpty(CenterSubLabel))
        {
            var subBrush = SubLabelBrush ?? labelBrush;
            var subText = new FormattedText(CenterSubLabel, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11.5, subBrush);
            var y = string.IsNullOrEmpty(CenterLabel) ? center.Y - subText.Height / 2 : center.Y + 1;
            context.DrawText(subText, new Point(center.X - subText.Width / 2, y));
        }
    }

    private static void DrawArcSegment(DrawingContext context, Point center, double radius,
        double startAngleDegrees, double sweepDegrees, IBrush brush)
    {
        if (sweepDegrees <= 0)
            return;

        var startPoint = PointOnCircle(center, radius, startAngleDegrees);
        var endPoint = PointOnCircle(center, radius, startAngleDegrees + sweepDegrees);
        var isLargeArc = sweepDegrees > 180;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(startPoint, false);
            ctx.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(brush, StrokeThickness), geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(center.X + radius * Math.Sin(radians), center.Y - radius * Math.Cos(radians));
    }
}
