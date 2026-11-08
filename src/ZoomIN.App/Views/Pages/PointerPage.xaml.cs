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

    // Brightness of the preview backgrounds #F3F3F3 and #202020 in PointerPage.xaml.
    private static readonly double s_lightLuma = AdaptiveColor.Luma(0xF3, 0xF3, 0xF3);
    private static readonly double s_darkLuma = AdaptiveColor.Luma(0x20, 0x20, 0x20);

    private PageServices? _services;
    private CursorShape? _arrow;

    public PointerPage()
    {
        InitializeComponent();
    }

    public PointerViewModel? Vm { get; private set; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _services = (PageServices)e.Parameter;
        var shapes = _services.Get<ICursorShapeSource>();
        _arrow = shapes.Load(shapes.ArrowHandle());
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

        Vm = _services!.Create<PointerViewModel>();
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

        // The adaptive pointer (M15) differs per background, the other modes do not.
        var style = Vm.Style;
        var (small, large) = (style.ScaleFor(1), style.ScaleFor(PreviewZoom));
        LightSmall.Source = Bitmap(style, small, s_lightLuma, s_lightLuma);
        LightLarge.Source = Bitmap(style, large, s_lightLuma, s_lightLuma);
        DarkSmall.Source = Bitmap(style, small, s_darkLuma, s_darkLuma);
        DarkLarge.Source = Bitmap(style, large, s_darkLuma, s_darkLuma);
        EdgeLarge.Source = style.ColorMode == PointerColorMode.Adaptive ? Bitmap(style, large, s_darkLuma, s_lightLuma) : null;
    }

    // Overriding Windows: the standard arrow, loaded sharp for exactly this size.
    private WriteableBitmap? Bitmap(PointerStyle style, double scale, double leftLuma, double rightLuma)
    {
        var shapes = _services!.Get<ICursorShapeSource>();
        var shape = style.OverrideWindows ? shapes.LoadStandard(shapes.ArrowHandle(), scale) : _arrow;
        if (shape is null)
        {
            return null;
        }

        var (pixels, width, height) = PointerPreview.Render(shape, style, scale, leftLuma, rightLuma);
        var bitmap = new WriteableBitmap(width, height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixels);
        }

        bitmap.Invalidate();
        return bitmap;
    }

    private void OnTestLocator(object sender, RoutedEventArgs e) => Vm?.TestLocator();

    private void OnSwapAdaptive(object sender, RoutedEventArgs e) => Vm?.SwapAdaptiveColors();

    private void OnReset(object sender, RoutedEventArgs e)
    {
        Vm?.Reset();
        CreateViewModel();
    }
}
