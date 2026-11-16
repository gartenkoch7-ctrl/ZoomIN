using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Views.Pages;

/// <summary>Page "Fokus &amp; Textcursor": tracking options and the caret marker look with a live preview (E2).</summary>
public sealed partial class TrackingPage : Page
{
    // The preview shows the marker as it looks at this zoom level, with the overlay's geometry.
    private const double PreviewZoom = 2;

    // Sample caret: the 2 × 40 cell between the sample letters (TrackingPage.xaml). The preview is
    // in XAML units; the Core geometry is reused as if they were screen pixels, which is only a
    // picture of the shape, not a position on a monitor.
    private static readonly PhysicalRect s_caret = new(0, 0, 2, 40);
    private static readonly PhysicalRect s_room = new(-200, -200, 400, 440);

    private readonly SolidColorBrush _brush = new();
    private CaretMarkerStyle? _shown;
    private bool _shownOn;

    public TrackingPage()
    {
        InitializeComponent();
    }

    public TrackingViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Vm = ((PageServices)e.Parameter).Create<TrackingViewModel>();
        Vm.CaretStyleChanged += RenderPreview;
        Bindings.Update();
        RenderPreview();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (Vm is not null)
        {
            Vm.CaretStyleChanged -= RenderPreview;
        }

        base.OnNavigatedFrom(e);
    }

    private void RenderPreview()
    {
        if (Vm is null)
        {
            return;
        }

        // Marker off: the sample caret stays, the marker goes, as in the magnified view.
        CaretCanvas.Visibility = Vm.CaretMarker ? Visibility.Visible : Visibility.Collapsed;
        var style = Vm.CaretStyle;
        var alpha = (byte)Math.Round(CaretMarkerStyle.Opacity * 255);
        _brush.Color = Windows.UI.Color.FromArgb(alpha, style.Color.R, style.Color.G, style.Color.B);

        // A colour change only recolours the shared brush; dragging the colour picker rebuilds nothing.
        if (_shown is not { } drawn || drawn.Shape != style.Shape || drawn.Thickness != style.Thickness)
        {
            DrawMarker(style);
        }

        // Screen readers hear the new description for a chosen shape, thickness or preset colour;
        // not on opening the page and not for every step of dragging the free colour picker.
        var announce = _shown is { } before && (before.Shape != style.Shape || before.Thickness != style.Thickness
            || (before.Color != style.Color && RgbColor.PresetName(style.Color) is not null) || _shownOn != Vm.CaretMarker);
        _shown = style;
        _shownOn = Vm.CaretMarker;
        if (announce)
        {
            FrameworkElementAutomationPeer.FromElement(CaretPreviewText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    private void DrawMarker(CaretMarkerStyle style)
    {
        CaretCanvas.Children.Clear();
        var parts = CaretMarkerGeometry.Compute(s_caret, PreviewZoom, style.Shape, style.Thickness, s_room);
        var brush = _brush;
        var a = parts.First;
        switch (parts.Shape)
        {
            case CaretMarkerShape.Wedges:
                CaretCanvas.Children.Add(Wedge(a, pointsUp: false, brush));
                CaretCanvas.Children.Add(Wedge(parts.Second, pointsUp: true, brush));
                break;
            case CaretMarkerShape.Frame:
                CaretCanvas.Children.Add(Place(new Rectangle { Stroke = brush, StrokeThickness = parts.Line }, a));
                break;
            case CaretMarkerShape.Circle:
                CaretCanvas.Children.Add(Place(new Ellipse { Stroke = brush, StrokeThickness = parts.Line }, a));
                break;
            default:
                CaretCanvas.Children.Add(Place(new Rectangle { Fill = brush }, a));
                break;
        }
    }

    private static Polygon Wedge(PhysicalRect r, bool pointsUp, Brush brush)
    {
        var tipY = pointsUp ? r.Y : r.Bottom;
        var baseY = pointsUp ? r.Bottom : r.Y;
        return new Polygon
        {
            Fill = brush,
            Points = [new Point(r.X, baseY), new Point(r.Right, baseY), new Point(r.X + (r.Width / 2), tipY)],
        };
    }

    private static Shape Place(Shape shape, PhysicalRect r)
    {
        shape.Width = r.Width;
        shape.Height = r.Height;
        Canvas.SetLeft(shape, r.X);
        Canvas.SetTop(shape, r.Y);
        return shape;
    }
}
