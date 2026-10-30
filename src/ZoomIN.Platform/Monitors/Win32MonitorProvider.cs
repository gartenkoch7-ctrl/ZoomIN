using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Monitors;

/// <summary>Monitors via EnumDisplayMonitors / MonitorFromPoint, identified by GDI device name.</summary>
public sealed unsafe class Win32MonitorProvider : IMonitorProvider
{
    // Kept in a static field so the GC never collects the delegate Windows calls back into.
    private static readonly MONITORENUMPROC s_collect = Collect;

    public IReadOnlyList<string> MonitorIds() => Enumerate().Select(m => m.Id).ToList();

    public string MonitorAt(PhysicalPoint point)
    {
        var monitor = PInvoke.MonitorFromPoint(new Point((int)point.X, (int)point.Y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return Describe(monitor).Id;
    }

    public PhysicalRect? BoundsOf(string monitorId) =>
        Enumerate().Where(m => m.Id == monitorId).Select(m => (PhysicalRect?)m.Bounds).FirstOrDefault();

    private static List<(string Id, PhysicalRect Bounds)> Enumerate()
    {
        var monitors = new List<(string Id, PhysicalRect Bounds)>();
        var handle = GCHandle.Alloc(monitors);
        try
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, null, s_collect, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return monitors;
    }

    private static BOOL Collect(HMONITOR monitor, HDC dc, RECT* bounds, LPARAM data)
    {
        var monitors = (List<(string Id, PhysicalRect Bounds)>)GCHandle.FromIntPtr(data).Target!;
        monitors.Add(Describe(monitor));
        return true;
    }

    private static (string Id, PhysicalRect Bounds) Describe(HMONITOR monitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (!PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
        {
            return (string.Empty, default);
        }

        var r = info.monitorInfo.rcMonitor;
        return (info.szDevice.ToString(), new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top));
    }
}
