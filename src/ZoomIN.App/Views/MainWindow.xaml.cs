using Microsoft.UI.Xaml;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Placeholder until the settings UI exists (M7): proves the Rendering layer is wired up.
        GpuStatusText.Text = GpuSupport.IsHardwareDeviceAvailable()
            ? "GPU bereit (Direct3D 11)"
            : "Keine passende GPU gefunden";
    }
}
