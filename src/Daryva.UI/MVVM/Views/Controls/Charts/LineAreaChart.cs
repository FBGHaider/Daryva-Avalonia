using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Daryva.MVVM.Views.Controls.Charts;

/// <summary>
/// Minimal income-vs-expense line/area chart. Hand-rolled (no charting library) since geometry
/// is data-driven and rebuilt on every reload anyway -- a raw Control owning Render() directly is
/// less code than mutating a Path.Data StreamGeometry from code-behind, and gives free access to
/// DrawingContext for gridlines/labels in the same pass. Straight-line segments, no bezier
/// smoothing -- calm, not decorative, per the redesign's "functional not decorative" charts rule.
/// </summary>
public class LineAreaChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>> IncomeValuesProperty =
        AvaloniaProperty.Register<LineAreaChart, IReadOnlyList<double>>(nameof(IncomeValues), Array.Empty<double>());

    public static readonly StyledProperty<IReadOnlyList<double>> ExpenseValuesProperty =
        AvaloniaProperty.Register<LineAreaChart, IReadOnlyList<double>>(nameof(ExpenseValues), Array.Empty<double>());

    public static readonly StyledProperty<IReadOnlyList<string>> LabelsProperty =
        AvaloniaProperty.Register<LineAreaChart, IReadOnlyList<string>>(nameof(Labels), Array.Empty<string>());

    public static readonly StyledProperty<IBrush?> IncomeBrushProperty =
        AvaloniaProperty.Register<LineAreaChart, IBrush?>(nameof(IncomeBrush));

    public static readonly StyledProperty<IBrush?> ExpenseBrushProperty =
        AvaloniaProperty.Register<LineAreaChart, IBrush?>(nameof(ExpenseBrush));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<LineAreaChart, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<LineAreaChart, IBrush?>(nameof(LabelBrush));

    private const double LeftAxisWidth = 44;
    private const double BottomAxisHeight = 20;
    private const double TopPadding = 12;
    private const double RightPadding = 8;
    private const int GridLineCount = 4;

    private Point? _pointerPosition;

    static LineAreaChart()
    {
        AffectsRender<LineAreaChart>(IncomeValuesProperty, ExpenseValuesProperty, LabelsProperty,
            IncomeBrushProperty, ExpenseBrushProperty, GridBrushProperty, LabelBrushProperty);
    }

    public LineAreaChart()
    {
        ClipToBounds = true;
    }

    public IReadOnlyList<double> IncomeValues
    {
        get => GetValue(IncomeValuesProperty);
        set => SetValue(IncomeValuesProperty, value);
    }

    public IReadOnlyList<double> ExpenseValues
    {
        get => GetValue(ExpenseValuesProperty);
        set => SetValue(ExpenseValuesProperty, value);
    }

    public IReadOnlyList<string> Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public IBrush? IncomeBrush
    {
        get => GetValue(IncomeBrushProperty);
        set => SetValue(IncomeBrushProperty, value);
    }

    public IBrush? ExpenseBrush
    {
        get => GetValue(ExpenseBrushProperty);
        set => SetValue(ExpenseBrushProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerPosition = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerPosition = null;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var income = IncomeValues;
        var expenses = ExpenseValues;
        var labels = Labels;
        var count = Math.Min(labels.Count, Math.Min(income.Count, expenses.Count));
        if (count < 2 || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var gridBrush = GridBrush ?? Brushes.Gray;
        var labelBrush = LabelBrush ?? Brushes.Gray;
        var incomeBrush = IncomeBrush ?? Brushes.Green;
        var expenseBrush = ExpenseBrush ?? Brushes.Purple;

        var plotLeft = LeftAxisWidth;
        var plotTop = TopPadding;
        var plotWidth = Math.Max(1, Bounds.Width - LeftAxisWidth - RightPadding);
        var plotHeight = Math.Max(1, Bounds.Height - TopPadding - BottomAxisHeight);

        var max = 0d;
        for (var i = 0; i < count; i++)
        {
            max = Math.Max(max, income[i]);
            max = Math.Max(max, expenses[i]);
        }
        // 10% headroom above the tallest point so the line/area never touches the top edge.
        max = max <= 0 ? 1 : max * 1.1;

        double YFor(double value) => plotTop + plotHeight - (value / max * plotHeight);
        double XFor(int index) => plotLeft + (count == 1 ? 0 : index * (plotWidth / (count - 1)));

        // Gridlines + value labels.
        var gridPen = new Pen(gridBrush, 1);
        for (var g = 0; g <= GridLineCount; g++)
        {
            var value = max / GridLineCount * g;
            var y = YFor(value);
            context.DrawLine(gridPen, new Point(plotLeft, y), new Point(plotLeft + plotWidth, y));

            var text = new FormattedText(FormatCompactCurrency(value), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10.5, labelBrush);
            context.DrawText(text, new Point(0, y - text.Height / 2));
        }

        // Month labels along the bottom.
        for (var i = 0; i < count; i++)
        {
            var text = new FormattedText(labels[i], CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 10.5, labelBrush);
            context.DrawText(text, new Point(XFor(i) - text.Width / 2, plotTop + plotHeight + 4));
        }

        // Income area fill (gradient down to baseline), then both series as straight-segment lines.
        var areaGeometry = new StreamGeometry();
        using (var ctx = areaGeometry.Open())
        {
            ctx.BeginFigure(new Point(XFor(0), plotTop + plotHeight), true);
            for (var i = 0; i < count; i++)
                ctx.LineTo(new Point(XFor(i), YFor(income[i])));
            ctx.LineTo(new Point(XFor(count - 1), plotTop + plotHeight));
            ctx.EndFigure(true);
        }
        var areaFill = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        };
        var incomeColor = (incomeBrush as ISolidColorBrush)?.Color ?? Colors.Green;
        areaFill.GradientStops.Add(new GradientStop(Color.FromArgb(46, incomeColor.R, incomeColor.G, incomeColor.B), 0));
        areaFill.GradientStops.Add(new GradientStop(Color.FromArgb(0, incomeColor.R, incomeColor.G, incomeColor.B), 1));
        context.DrawGeometry(areaFill, null, areaGeometry);

        DrawSeriesLine(context, income, incomeBrush, XFor, YFor, count);
        DrawSeriesLine(context, expenses, expenseBrush, XFor, YFor, count);

        // Hover guideline + nearest-point readout -- the one purposeful interaction.
        if (_pointerPosition.HasValue && _pointerPosition.Value.X >= plotLeft && _pointerPosition.Value.X <= plotLeft + plotWidth)
        {
            var nearest = count == 1 ? 0 : (int)Math.Round((_pointerPosition.Value.X - plotLeft) / (plotWidth / (count - 1)));
            nearest = Math.Clamp(nearest, 0, count - 1);
            var guideX = XFor(nearest);
            var guidePen = new Pen(gridBrush, 1);
            context.DrawLine(guidePen, new Point(guideX, plotTop), new Point(guideX, plotTop + plotHeight));

            var incomeDot = new Point(guideX, YFor(income[nearest]));
            var expenseDot = new Point(guideX, YFor(expenses[nearest]));
            context.DrawEllipse(incomeBrush, null, incomeDot, 3.5, 3.5);
            context.DrawEllipse(expenseBrush, null, expenseDot, 3.5, 3.5);

            var readout = new FormattedText(
                $"{labels[nearest]}  Income {FormatCompactCurrency(income[nearest])}  Expenses {FormatCompactCurrency(expenses[nearest])}",
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, labelBrush);
            var readoutX = Math.Min(Math.Max(guideX - readout.Width / 2, plotLeft), plotLeft + plotWidth - readout.Width);
            context.DrawText(readout, new Point(readoutX, plotTop - 2));
        }
    }

    private static void DrawSeriesLine(DrawingContext context, IReadOnlyList<double> values, IBrush brush,
        Func<int, double> xFor, Func<double, double> yFor, int count)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(xFor(0), yFor(values[0])), false);
            for (var i = 1; i < count; i++)
                ctx.LineTo(new Point(xFor(i), yFor(values[i])));
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(brush, 2), geometry);
    }

    private static string FormatCompactCurrency(double value)
    {
        if (value >= 1000)
            return $"£{value / 1000:0.#}k";
        return $"£{value:0}";
    }
}
