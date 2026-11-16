using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.StationsAndDesktops;

namespace ZoomIN.Platform.Input;

/// <summary>
/// Whether the calling thread's desktop is the one that receives input right now. Read-only queries,
/// no state (Interop, ADR-0025).
/// </summary>
internal static unsafe class InputDesktop
{
    private const int ErrorAccessDenied = 5;

    /// <returns>
    /// True when the input desktop is this thread's desktop, false when another desktop has it, null
    /// when that cannot be told (the caller keeps its state). A normal user may not open the secure
    /// desktop (Winlogon) at all: "access denied" therefore means another desktop has the input.
    /// </returns>
    public static bool? IsCurrent()
    {
        var input = PInvoke.OpenInputDesktop(0, false, DESKTOP_ACCESS_FLAGS.DESKTOP_READOBJECTS);
        if (input.IsNull)
        {
            return Marshal.GetLastPInvokeError() == ErrorAccessDenied ? false : null;
        }

        string? name;
        try
        {
            name = NameOf((HANDLE)input.Value);
        }
        finally
        {
            PInvoke.CloseDesktop(input);
        }

        var own = NameOf((HANDLE)PInvoke.GetThreadDesktop(PInvoke.GetCurrentThreadId()).Value);
        return own is null || name is null ? null : string.Equals(own, name, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NameOf(HANDLE desktop)
    {
        if (desktop.IsNull)
        {
            return null;
        }

        // Desktop names are short ("Winlogon", "Default"); 256 characters is plenty.
        var name = stackalloc char[256];
        uint needed;
        return PInvoke.GetUserObjectInformation(desktop, USER_OBJECT_INFORMATION_INDEX.UOI_NAME, name, 256 * sizeof(char), &needed)
            ? new string(name)
            : null;
    }
}
