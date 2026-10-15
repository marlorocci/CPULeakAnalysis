using CPULeakAnalysis.Models;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Collections;

namespace CPULeakAnalysis.Controls;

public sealed class CpuChart : FrameworkElement
{
    private const double PlotLeftMargin = 58;
    private const double PlotTopMargin = 12;
    private const double PlotRightMargin = 12;
    private const double PlotBottomMargin = 32;

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
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

    public static readonly DependencyProperty TimeRangeProperty =
        DependencyProperty.Register(
            nameof(TimeRange),
            typeof(TimeSpan),
            typeof(CpuChart),
            new FrameworkPropertyMetadata(
                TimeSpan.FromHours(1),
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnTimeRangeChanged,
                CoerceTimeRange));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public int ZoomPercent
    {
        get =>
            (int)GetValue(ZoomPercentProperty);

        set =>
            SetValue(ZoomPercentProperty, value);
    }

    public TimeSpan TimeRange
    {
        get =>
            (TimeSpan)GetValue(TimeRangeProperty);

        set =>
            SetValue(TimeRangeProperty, value);
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
        Colors.YellowGreen,
        Colors.Salmon
    };

    private static readonly Brush BackgroundBrush =
        new SolidColorBrush(
            Color.FromRgb(24, 28, 36));

    private static readonly Brush AxisTextBrush =
        new SolidColorBrush(
            Color.FromRgb(170, 180, 198));

    private static readonly Pen GridPen =
        new(
            new SolidColorBrush(
                Color.FromRgb(50, 56, 68)),
            1);

    private static readonly Pen AxisPen =
        new(
            new SolidColorBrush(
                Color.FromRgb(105, 116, 136)),
            1);

    private static object CoerceZoomPercent(
        DependencyObject dependencyObject,
        object baseValue)
    {
        if (baseValue is not int value)
        {
            return 100;
        }

        return Math.Clamp(value, 10, 100);
    }

    private static object CoerceTimeRange(
        DependencyObject dependencyObject,
        object baseValue)
    {
        if (baseValue is not TimeSpan value ||
            value <= TimeSpan.Zero)
        {
            return TimeSpan.FromHours(1);
        }

        return value;
    }

    private static void OnTimeRangeChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is CpuChart chart)
        {
            chart.InvalidateVisual();
        }
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

        if (ActualWidth <= 0 ||
            ActualHeight <= 0)
        {
            return;
        }

        drawingContext.DrawRectangle(
            BackgroundBrush,
            null,
            new Rect(
                0,
                0,
                ActualWidth,
                ActualHeight));

        double plotLeft =
            PlotLeftMargin;

        double plotTop =
            PlotTopMargin;

        double plotWidth =
            Math.Max(
                0,
                ActualWidth -
                PlotLeftMargin -
                PlotRightMargin);

        double plotHeight =
            Math.Max(
                0,
                ActualHeight -
                PlotTopMargin -
                PlotBottomMargin);

        if (plotWidth <= 0 ||
            plotHeight <= 0)
        {
            return;
        }

        double visibleCpuMax =
            Math.Clamp(
                ZoomPercent,
                10,
                100);

        TimeSpan effectiveTimeRange =
            TimeRange > TimeSpan.Zero
                ? TimeRange
                : TimeSpan.FromHours(1);

        DateTime maximumTimestampUtc =
            DateTime.UtcNow;

        DateTime minimumTimestampUtc =
            maximumTimestampUtc -
            effectiveTimeRange;

        // These methods are now part of the render pipeline.
        DrawGrid(
            drawingContext,
            plotLeft,
            plotTop,
            plotWidth,
            plotHeight);

        DrawAxisLines(
            drawingContext,
            plotLeft,
            plotTop,
            plotWidth,
            plotHeight);

        DrawAxisLabels(
            drawingContext,
            plotLeft,
            plotTop,
            plotWidth,
            plotHeight,
            visibleCpuMax,
            minimumTimestampUtc,
            maximumTimestampUtc);
        System.Diagnostics.Debug.WriteLine(
    $"ItemsSource Type = {ItemsSource?.GetType().FullName ?? "NULL"}");

        var allSamples = ItemsSource?
    .Cast<CpuSample>()
    .ToList() ?? [];

        System.Diagnostics.Debug.WriteLine(
            $"Samples Received: {allSamples.Count}");

        if (allSamples.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Oldest Sample     : {allSamples.Min(x => x.TimestampUtc):O}");

            System.Diagnostics.Debug.WriteLine(
                $"Newest Sample     : {allSamples.Max(x => x.TimestampUtc):O}");

            System.Diagnostics.Debug.WriteLine(
                $"Minimum Timestamp : {minimumTimestampUtc:O}");

            System.Diagnostics.Debug.WriteLine(
                $"Maximum Timestamp : {maximumTimestampUtc:O}");
        }

        TimeSpan effectiveRange =
    TimeRange == TimeSpan.Zero
        ? TimeSpan.FromDays(7)
        : TimeRange;

        DateTime cutoffUtc =
            DateTime.UtcNow - effectiveRange;



        var data = ItemsSource?
            .Cast<CpuSample>()
            .Where(x => x.TimestampUtc >= cutoffUtc)
            .OrderBy(x => x.TimestampUtc)
            .ToList()
            ?? [];
        //debug
        System.Diagnostics.Debug.WriteLine(
    $"ItemsSource Count = {ItemsSource?.Cast<CpuSample>().Count() ?? 0}");

        System.Diagnostics.Debug.WriteLine(
            $"Chart Data Count = {data.Count}");

        if (data.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine(
                $"First Sample = {data.Min(x => x.TimestampUtc):O}");

            System.Diagnostics.Debug.WriteLine(
                $"Last Sample = {data.Max(x => x.TimestampUtc):O}");
        }

        System.Diagnostics.Debug.WriteLine(
            $"Minimum Timestamp = {minimumTimestampUtc:O}");

        System.Diagnostics.Debug.WriteLine(
            $"Maximum Timestamp = {maximumTimestampUtc:O}");

        //edn debug
        if (data.Count == 0)
        {
            DrawNoDataMessage(
                drawingContext,
                "No CPU data available for the selected time range.",
                plotLeft,
                plotTop,
                plotWidth,
                plotHeight);

            return;
        }

        long minimumTicks =
            minimumTimestampUtc.Ticks;

        long maximumTicks =
            maximumTimestampUtc.Ticks;

        long tickRange =
            Math.Max(
                1,
                maximumTicks - minimumTicks);

        int colorIndex = 0;

        foreach (var group in data.GroupBy(
                     sample => sample.DisplayName))
        {
            var orderedSamples =
                group
                    .OrderBy(sample =>
                        sample.TimestampUtc)
                    .ToList();

            var points =
                orderedSamples
                    .Select(sample =>
                    {
                        double xPosition =
                            plotLeft +
                            (
                                sample.TimestampUtc.Ticks -
                                minimumTicks
                            ) /
                            (double)tickRange *
                            plotWidth;

                        double displayedCpu =
                            Math.Clamp(
                                sample.CpuPercent,
                                0,
                                visibleCpuMax);

                        double yPosition =
                            plotTop +
                            plotHeight -
                            (
                                displayedCpu /
                                visibleCpuMax *
                                plotHeight
                            );

                        return new Point(
                            xPosition,
                            yPosition);
                    })
                    .ToArray();

            Color seriesColor =
                SeriesColors[
                    colorIndex %
                    SeriesColors.Length];

            colorIndex++;

            var seriesBrush =
                new SolidColorBrush(seriesColor);

            if (points.Length == 1)
            {
                drawingContext.DrawEllipse(
                    seriesBrush,
                    null,
                    points[0],
                    2.5,
                    2.5);

                continue;
            }

            var geometry = new StreamGeometry();

            using (var context = geometry.Open())
            {
                context.BeginFigure(
                    points[0],
                    false,
                    false);

                context.PolyLineTo(
                    points.Skip(1).ToList(),
                    true,
                    false);
            }

            geometry.Freeze();

            var seriesPen =
                new Pen(seriesBrush, 1.7);

            drawingContext.DrawGeometry(
                null,
                seriesPen,
                geometry);
        }
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
                index *
                height /
                horizontalSections;

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
                index *
                width /
                verticalSections;

            drawingContext.DrawLine(
                GridPen,
                new Point(x, top),
                new Point(x, top + height));
        }
    }

    private static void DrawAxisLines(
        DrawingContext drawingContext,
        double left,
        double top,
        double width,
        double height)
    {
        drawingContext.DrawLine(
            AxisPen,
            new Point(left, top),
            new Point(left, top + height));

        drawingContext.DrawLine(
            AxisPen,
            new Point(left, top + height),
            new Point(left + width, top + height));
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
                (
                    visibleCpuMax *
                    index /
                    horizontalSections
                );

            double y =
                top +
                index *
                height /
                horizontalSections;

            DrawRightAlignedText(
                drawingContext,
                $"{cpuValue:0}%",
                left - 7,
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
                index *
                width /
                verticalSections;

            string label =
                GetTimeAxisLabel(
                    localTimestamp,
                    visiblePeriod);

            FormattedText formattedText =
                CreateFormattedText(
                    label,
                    10);

            double labelX =
                x -
                formattedText.Width /
                2;

            labelX =
                Math.Clamp(
                    labelX,
                    left,
                    Math.Max(
                        left,
                        left +
                        width -
                        formattedText.Width));

            drawingContext.DrawText(
                formattedText,
                new Point(
                    labelX,
                    top + height + 5));
        }
    }

    private static string GetTimeAxisLabel(
        DateTime localTimestamp,
        TimeSpan visiblePeriod)
    {
        if (visiblePeriod <= TimeSpan.FromHours(24))
        {
            return localTimestamp.ToString(
                "HH:mm",
                CultureInfo.CurrentCulture);
        }

        return localTimestamp.ToString(
            "MM/dd",
            CultureInfo.CurrentCulture);
    }

    //private void DrawNoDataMessage(
    //    DrawingContext drawingContext,
    //    string message,
    //    double plotLeft,
    //    double plotTop,
    //    double plotWidth,
    //    double plotHeight)
    //{
    //    FormattedText formattedText =
    //        CreateFormattedText(
    //            message,
    //            14);

    //    double x =
    //        plotLeft +
    //        Math.Max(
    //            0,
    //            (
    //                plotWidth -
    //                formattedText.Width
    //            ) /
    //            2);

    //    double y =
    //        plotTop +
    //        Math.Max(
    //            0,
    //            (
    //                plotHeight -
    //                formattedText.Height
    //            ) /
    //            2);

    //    drawingContext.DrawText(
    //        formattedText,
    //        new Point(x, y));
    //}

    private void DrawRightAlignedText(
        DrawingContext drawingContext,
              string text,
        double right,
        double y)
    {
        FormattedText formattedText =
            CreateFormattedText(
                text,
                11);

        drawingContext.DrawText(
            formattedText,
            new Point(
                right - formattedText.Width,
                y));
    }

    private FormattedText CreateFormattedText(
        string text,
        double fontSize)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            AxisTextBrush,
            VisualTreeHelper
                .GetDpi(this)
                .PixelsPerDip);
    }

    private void DrawNoDataMessage(
        DrawingContext drawingContext,
        string message,
        double plotLeft,
        double plotTop,
        double plotWidth,
        double plotHeight)
    {
        FormattedText formattedText =
            CreateFormattedText(
                message,
                14);

        drawingContext.DrawText(
            formattedText,
            new Point(
                plotLeft +
                (plotWidth - formattedText.Width) / 2,
                plotTop +
                (plotHeight - formattedText.Height) / 2));
    }

    private static StreamGeometry CreateLineGeometry(
        IReadOnlyList<Point> points)
    {
        var geometry =
            new StreamGeometry();

        using (StreamGeometryContext context =
               geometry.Open())
        {
            context.BeginFigure(
                points[0],
                false,
                false);

            context.PolyLineTo(
                points.Skip(1).ToList(),
                true,
                false);
        }

        geometry.Freeze();

        return geometry;
    }
}