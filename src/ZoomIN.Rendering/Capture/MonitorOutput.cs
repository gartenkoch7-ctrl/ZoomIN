using Vortice.DXGI;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Rendering.Capture;

/// <summary>
/// A DXGI output (monitor) together with the adapter it is attached to. Identified by its
/// device name ("\\.\DISPLAYn"), the same id the Platform layer uses.
/// </summary>
internal sealed class MonitorOutput : IDisposable
{
    private MonitorOutput(IDXGIAdapter1 adapter, IDXGIOutput1 output, OutputDescription description)
    {
        Adapter = adapter;
        Output = output;
        DeviceName = description.DeviceName;
        var r = description.DesktopCoordinates;
        Bounds = new PhysicalRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }

    public IDXGIAdapter1 Adapter { get; }

    public IDXGIOutput1 Output { get; }

    public string DeviceName { get; }

    /// <summary>Monitor rectangle in physical desktop pixels.</summary>
    public PhysicalRect Bounds { get; }

    public int Width => (int)Bounds.Width;

    public int Height => (int)Bounds.Height;

    /// <summary>Device names of all monitors attached to the desktop, on every adapter.</summary>
    public static IReadOnlyList<string> AttachedDeviceNames(IDXGIFactory1 factory)
    {
        var names = new List<string>();
        Visit(factory, (adapter, output, desc) =>
        {
            names.Add(desc.DeviceName);
            return false;
        });
        return names;
    }

    /// <summary>Opens the monitor with the given device name on the adapter that drives it.</summary>
    public static MonitorOutput Find(IDXGIFactory1 factory, string deviceName)
    {
        MonitorOutput? found = null;
        Visit(factory, (adapter, output, desc) =>
        {
            if (desc.DeviceName != deviceName)
            {
                return false;
            }

            found = new MonitorOutput(adapter, output.QueryInterface<IDXGIOutput1>(), desc);
            return true;
        });
        return found ?? throw new DisplayGoneException(deviceName);
    }

    public void Dispose()
    {
        Output.Dispose();
        Adapter.Dispose();
    }

    /// <summary>Calls <paramref name="visitor"/> for every attached output; it returns true to keep the adapter.</summary>
    private static void Visit(IDXGIFactory1 factory, Func<IDXGIAdapter1, IDXGIOutput, OutputDescription, bool> visitor)
    {
        for (uint a = 0; factory.EnumAdapters1(a, out var adapter).Success; a++)
        {
            var keepAdapter = false;
            for (uint o = 0; adapter.EnumOutputs(o, out var output).Success; o++)
            {
                var desc = output.Description;
                var stop = desc.AttachedToDesktop && visitor(adapter, output, desc);
                output.Dispose();
                if (stop)
                {
                    keepAdapter = true;
                    break;
                }
            }

            if (keepAdapter)
            {
                return;
            }

            adapter.Dispose();
        }
    }
}
