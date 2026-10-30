using Vortice.DXGI;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Capture;

/// <summary>A DXGI output (monitor) together with the adapter it is attached to.</summary>
internal sealed class MonitorOutput : IDisposable
{
    public MonitorOutput(IDXGIAdapter1 adapter, IDXGIOutput1 output)
    {
        Adapter = adapter;
        Output = output;
        var r = output.Description.DesktopCoordinates;
        Bounds = new PhysicalRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    public IDXGIAdapter1 Adapter { get; }

    public IDXGIOutput1 Output { get; }

    /// <summary>Monitor rectangle in physical desktop pixels.</summary>
    public PhysicalRect Bounds { get; }

    public int Width => (int)Bounds.Width;

    public int Height => (int)Bounds.Height;

    /// <summary>The monitor whose desktop rectangle starts at (0,0) is the primary one.</summary>
    public static MonitorOutput FindPrimary(IDXGIFactory1 factory)
    {
        for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
        {
            for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
            {
                var desc = output.Description;
                if (desc.AttachedToDesktop && desc.DesktopCoordinates.Left == 0 && desc.DesktopCoordinates.Top == 0)
                {
                    var output1 = output.QueryInterface<IDXGIOutput1>();
                    output.Dispose();
                    return new MonitorOutput(adapter, output1);
                }

                output.Dispose();
            }

            adapter.Dispose();
        }

        throw new InvalidOperationException("No primary monitor found.");
    }

    public void Dispose()
    {
        Output.Dispose();
        Adapter.Dispose();
    }
}
