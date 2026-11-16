using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.Shell;

/// <summary>The tray icon's image (M7); split out of <see cref="Win32TrayIcon"/>.</summary>
internal sealed unsafe class TrayIconImage(string path)
{
    /// <summary>ZoomIN icon next to the exe; Windows' application icon if it is missing.</summary>
    public nint Load()
    {
        var size = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSMICON);
        if (File.Exists(path))
        {
            fixed (char* file = path)
            {
                var icon = PInvoke.LoadImage(HINSTANCE.Null, file, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE);
                if (!icon.IsNull)
                {
                    return (nint)icon.Value;
                }
            }
        }

        return (nint)PInvoke.LoadIcon(HINSTANCE.Null, (PCWSTR)(char*)32512).Value; // IDI_APPLICATION
    }
}
