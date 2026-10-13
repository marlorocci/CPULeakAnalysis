using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CPULeakAnalysis.Models;

namespace CPULeakAnalysis.Controls;

public sealed class CpuChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable<CpuSample>),
            typeof(CpuChart),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnItemsSourceChanged));

    public static readonly DependencyProperty ZoomPercentProperty =
        DependencyProperty.Register(
            nameof(ZoomPercent),
            typeof(int),
            typeof(CpuChart),
            new FrameworkPropertyMetadata(
                100,
                FrameworkPropertyMetadataOptions.AffectsRender,
                null,
                CoerceZoomPercent));

    public IEnumerable<CpuSample>? ItemsSource
    {
        get => (IEnumerable<CpuSample>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int ZoomPercent
    {
        get => (int)GetValue(ZoomPercentProperty);
        set => SetValue(ZoomPercentProperty, value);
    }

    private static readonly Color[] SeriesColors =
    {
        Colors.DeepSkyBlue,
        Colors.Orange,
        Colors.LimeGreen,
        Colors.Violet,
        Colors.Gold,
        Colors.Coral,
        Colors.Cyan,
        Colors.HotPink,
        Colors.LightGreen,
        Colors.Salmon
    };

    private static readonly Brush BackgroundBrush =
        new SolidColorBrush(Color.FromRgb(24, 28, 36));

    private static readonly Pen GridPen =
        new(
            new SolidColorBrush(Color.FromRgb(50, 56, 68)),
            1);

    private static readonly Brush AxisTextBrush =
        new SolidColorBrush(Color.FromRgb(170, 180, 198));

    private static object CoerceZoomPercent(
        DependencyObject dependencyObject,
        object baseValue)
    {
        if (baseValue is not int value)
            return 100;

        return Math.Clamp(value, 10, 100);
    }

    private static void OnItemsSourceChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var chart = (CpuChart)dependencyObject;

        if (eventArgs.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -=
                chart.OnCollectionChanged;
        }

        if (eventArgs.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged +=
                chart.OnCollectionChanged;
        }

        chart.InvalidateVisual();
    }

    private void OnCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs eventArgs)
    {
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        drawingContext.DrawRectangle(
            BackgroundBrush,
            null,
            new Rect(0, 0, ActualWidth, ActualHeight));

        const double leftMargin = 55;
        const double rightMargin = 15;
        const double topMargin = 15;
        const double bottomMargin = 30;

        double chartWidth =
            Math.Max(1, ActualWidth - leftMargin - rightMargin);

        double chartHeight =
            Math.Max(1, ActualHeight - topMargin - bottomMargin);

        DrawGrid(
            drawingContext,
            leftMargin,
            topMargin,
            chartWidth,
            chartHeight);

        if (ItemsSource is null)
        {
            DrawNoDataMessage(
                drawingContext,
                "No CPU data available.");

            return;
        }

        DateTime sevenDayCutoff = DateTime.UtcNow.AddDays(-7);

        var data = ItemsSource
            .Where(sample =>
                sample.TimestampUtc >= sevenDayCutoff)
            .OrderBy(sample => sample.TimestampUtc)
            .ToList();

        if (data.Count == 0)
        {
            DrawNoDataMessage(
                drawingContext,
                "No CPU data available for the last 7 days.");

            return;
        }

        long minimumTimestamp =
            data.Min(sample => sample.TimestampUtc.Ticks);

        long maximumTimestamp =
            data.Max(sample => sample.TimestampUtc.Ticks);

        if (maximumTimestamp == minimumTimestamp)
            maximumTimestamp++;

        double visibleCpuMax =
            Math.Clamp(ZoomPercent, 10, 100);

        int colorIndex = 0;

        foreach (var processGroup in data.GroupBy(
                     sample => new
                     {
                         sample.ProcessName,
                         sample.ProcessId
                     }))
        {
            Point[] points = processGroup
                .OrderBy(sample => sample.TimestampUtc)
                .Select(sample =>
                {
                    double xPosition =
                        leftMargin +
                        ((sample.TimestampUtc.Ticks - minimumTimestamp) /
                         (double)(maximumTimestamp - minimumTimestamp) *
                         chartWidth);

                    double boundedCpu =
                        Math.Clamp(
                            sample.CpuPercent,
                            0,
                            visibleCpuMax);

                    double yPosition =
                        topMargin +
                        chartHeight -
                        (boundedCpu /
                         visibleCpuMax *
                         chartHeight);

                    return new Point(
                        xPosition,
                        yPosition);
                })
                .ToArray();

            if (points.Length < 2)
                continue;

            Color seriesColor =
                SeriesColors[colorIndex % SeriesColors.Length];

            colorIndex++;

            var seriesPen = new Pen(
                new SolidColorBrush(seriesColor),
                1.7);

            seriesPen.Freeze();

            var geometry =
                CreateLineGeometry(points);

            drawingContext.DrawGeometry(
                null,
                seriesPen,
                geometry);
        }

        DrawAxisLabels(
            drawingContext,
            leftMargin,
            topMargin,
            chartWidth,
            chartHeight,
            visibleCpuMax,
            new DateTime(minimumTimestamp, DateTimeKind.Utc),
            new DateTime(maximumTimestamp, DateTimeKind.Utc));
    }

    private static void DrawGrid(
        DrawingContext drawingContext,
        double left,
        double top,
        double width,
        double height)
    {
        const int horizontalSections = 4;
        const int verticalSections = 7;

        for (int index = 0;
             index <= horizontalSections;
             index++)
        {
            double y =
                top +
                (index * height / horizontalSections);

            drawingContext.DrawLine(
                GridPen,
                new Point(left, y),
                new Point(left + width, y));
        }

        for (int index = 0;
             index <= verticalSections;
             index++)
        {
            double x =
                left +
                (index * width / verticalSections);

            drawingContext.DrawLine(
                GridPen,
                new Point(x, top),
                new Point(x, top + height));
        }
    }

    private void DrawAxisLabels(
        DrawingContext drawingContext,
        double left,
        double top,
        double width,
        double height,
        double visibleCpuMax,
        DateTime minimumTimestampUtc,
        DateTime maximumTimestampUtc)
    {
        const int horizontalSections = 4;
        const int verticalSections = 7;

        for (int index = 0;
             index <= horizontalSections;
             index++)
        {
            double cpuValue =
                visibleCpuMax -
                (visibleCpuMax *
                 index /
                 horizontalSections);

            double y =
                top +
                (index * height / horizontalSections);

            DrawText(
                drawingContext,
                $"{cpuValue:0}%",
                4,
                y - 8);
        }

        TimeSpan visiblePeriod =
            maximumTimestampUtc -
            minimumTimestampUtc;

        for (int index = 0;
             index <= verticalSections;
             index++)
        {
            DateTime timestampUtc =
                minimumTimestampUtc.AddTicks(
                    visiblePeriod.Ticks *
                    index /
                    verticalSections);

            DateTime localTimestamp =
                timestampUtc.ToLocalTime();

            double x =
                left +
                (index * width / verticalSections);

            string label =
                localTimestamp.ToString("MM/dd");

            DrawText(
                drawingContext,
                label,
                x - 16,
                top + height + 5);
        }
    }

    private void DrawNoDataMessage(
        DrawingContext drawingContext,
        string message)
    {
        var formattedText = new FormattedText(
            message,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            14,
            AxisTextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        drawingContext.DrawText(
            formattedText,
            new Point(
                Math.Max(
                    10,
                    (ActualWidth - formattedText.Width) / 2),
                Math.Max(
                    10,
                    (ActualHeight - formattedText.Height) / 2)));
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        double x,
        double y)
    {
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            11,
            AxisTextBrush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        drawingContext.DrawText(
            formattedText,
            new Point(x, y));
    }

    private static StreamGeometry CreateLineGeometry(
        IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();

        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(
                points[0],
                isFilled: false,
                isClosed: false);

            context.PolyLineTo(
                points.Skip(1).ToList(),
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();

        return geometry;
    }
}