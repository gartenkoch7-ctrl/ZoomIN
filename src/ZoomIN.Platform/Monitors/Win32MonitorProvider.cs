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

    public IReadOnlyList<string> MonitorIds()
    {
        var ids = new List<string>();
        var handle = GCHandle.Alloc(ids);
        try
        {
            PInvoke.EnumDisplayMonitors(HDC.Null, null, s_collect, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return ids;
    }

    public string MonitorAt(PhysicalPoint point)
    {
        var monitor = PInvoke.MonitorFromPoint(new Point((int)point.X, (int)point.Y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return DeviceName(monitor);
    }

    private static BOOL Collect(HMONITOR monitor, HDC dc, RECT* bounds, LPARAM data)
    {
        var ids = (List<string>)GCHandle.FromIntPtr(data).Target!;
        ids.Add(DeviceName(monitor));
        return true;
    }

    private static string DeviceName(HMONITOR monitor)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        return PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info) ? info.szDevice.ToString() : string.Empty;
    }
}
