using System.Globalization;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Gives the docked strip back after a crash: the shell keeps an AppBar's space reserved until
/// it is removed, even when the owning process is gone.
/// </summary>
public sealed unsafe class AppBarCleanup : ICleanupStep
{
    private const uint AbmRemove = 1;

    public string Name => "Release docked strip";

    public void Run()
    {
        var file = RecoveryFiles.AppBarWindow;
        if (!File.Exists(file))
        {
            return;
        }

        if (long.TryParse(File.ReadAllText(file), NumberStyles.Integer, CultureInfo.InvariantCulture, out var handle))
        {
            var data = new APPBARDATA { cbSize = (uint)sizeof(APPBARDATA), hWnd = new HWND((void*)(nint)handle) };
            PInvoke.SHAppBarMessage(AbmRemove, ref data);
        }

        File.Delete(file);
    }
}
