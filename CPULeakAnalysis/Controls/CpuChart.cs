using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using CPULeakAnalysis.Models;
namespace CPULeakAnalysis.Controls;
public sealed class CpuChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsSourceProperty=DependencyProperty.Register(nameof(ItemsSource),typeof(IEnumerable<CpuSample>),typeof(CpuChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender,Changed));
    public IEnumerable<CpuSample>? ItemsSource { get=> (IEnumerable<CpuSample>?)GetValue(ItemsSourceProperty); set=>SetValue(ItemsSourceProperty,value); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e) { var c=(CpuChart)d; if(e.OldValue is INotifyCollectionChanged o)o.CollectionChanged-=c.OnChanged; if(e.NewValue is INotifyCollectionChanged n)n.CollectionChanged+=c.OnChanged; c.InvalidateVisual(); }
    private void OnChanged(object? s, NotifyCollectionChangedEventArgs e)=>InvalidateVisual();
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); var r=new Rect(0,0,ActualWidth,ActualHeight); dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24,28,36)),null,r);
        var data = ItemsSource?
            .Where(x => x.TimestampUtc >= DateTime.UtcNow.AddDays(-7))
            .OrderBy(x => x.TimestampUtc)
            .ToList() ?? [];

        if (data.Count<2)return;


        for(int i=0;i<=4;i++){double y=i*ActualHeight/4;dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(50,56,68)),1),new(0,y),new(ActualWidth,y));}
       
        
        var min=data.Min(x=>x.TimestampUtc).Ticks; var max=data.Max(x=>x.TimestampUtc).Ticks; if(max==min)max++;
        var colors=new[]{Colors.DeepSkyBlue,Colors.Orange,Colors.LimeGreen,Colors.Violet,Colors.Gold,Colors.Coral,Colors.Cyan}; int ci=0;
        foreach(var g in data.GroupBy(x=>x.DisplayName))
        { var points=g.Select(x=>new Point((x.TimestampUtc.Ticks-min)/(double)(max-min)*ActualWidth,ActualHeight-(Math.Clamp(x.CpuPercent,0,100)/100d*ActualHeight))).ToArray(); if(points.Length>1)dc.DrawGeometry(null,new Pen(new SolidColorBrush(colors[ci++%colors.Length]),1.7),new StreamGeometryBuilder(points).Geometry); }
    }
    private sealed class StreamGeometryBuilder { public StreamGeometry Geometry{get;}=new(); public StreamGeometryBuilder(Point[] p){using var c=Geometry.Open();c.BeginFigure(p[0],false,false);c.PolyLineTo(p.Skip(1).ToList(),true,false);Geometry.Freeze();} }
}