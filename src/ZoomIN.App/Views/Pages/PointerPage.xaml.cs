using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Views.Pages;

/// <summary>Pointer page (UI redesign): every pointer option with a live preview.</summary>
public sealed partial class PointerPage : Page
{
    // The large preview shows the pointer as it looks at this zoom level.
    private const double PreviewZoom = 3;

    private PageContext? _context;
    private CursorShape? _arrow;

    public PointerPage()
    {
        InitializeComponent();
    }

    public PointerViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _context = (PageContext)e.Parameter;
        _arrow = _context.CursorShapes.Load(_context.CursorShapes.ArrowHandle());
        CreateViewModel();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (Vm is not null)
        {
            Vm.StyleChanged -= RenderPreview;
        }
    }

    private void CreateViewModel()
    {
        if (Vm is not null)
        {
            Vm.StyleChanged -= RenderPreview;
        }

        Vm = new PointerViewModel(_context!);
        Vm.StyleChanged += RenderPreview;
        Bindings.Update();
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (Vm is null)
        {
            return;
        }

        var style = Vm.Style;
        var small = Bitmap(style, style.ScaleFor(1));
        var large = Bitmap(style, style.ScaleFor(PreviewZoom));
        (LightSmall.Source, DarkSmall.Source) = (small, small);
        (LightLarge.Source, DarkLarge.Source) = (large, large);
    }

    // Overriding Windows: the standard arrow, loaded sharp for exactly this size.
    private WriteableBitmap? Bitmap(PointerStyle style, double scale)
    {
        var shapes = _context!.CursorShapes;
        var shape = style.OverrideWindows ? shapes.LoadStandard(shapes.ArrowHandle(), scale) : _arrow;
        if (shape is null)
        {
            return null;
        }

        var (pixels, width, height) = PointerPreview.Render(shape, style, scale);
        var bitmap = new WriteableBitmap(width, height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels);
        }

        bitmap.Invalidate();
        return bitmap;
    }

    private void OnTestLocator(object sender, RoutedEventArgs e) => Vm?.TestLocator();

    private void OnReset(object sender, RoutedEventArgs e)
    {
        Vm?.Reset();
        CreateViewModel();
    }
}
