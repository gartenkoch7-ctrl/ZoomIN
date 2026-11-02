using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ZoomIN.Platform.Cursor;

/// <summary>
/// The standard Windows pointers from %SystemRoot%\Cursors, loaded at any size. Used when ZoomIN
/// overrides the Windows pointer settings: the files are the untouched white originals, while the
/// pointers Windows hands out are already enlarged or recoloured by the accessibility settings
/// (those live in %LocalAppData%\Microsoft\Windows\Cursors).
/// </summary>
internal static unsafe class StandardPointers
{
    // Windows standard pointer size at 96 DPI.
    private const int BaseSizeAt96Dpi = 32;

    // Largest frame Windows ships is 128 px; LoadImage resamples, more pixels only cost memory.
    private const int MaxPixels = 256;

    // IDC_* resource ids with the file of the Windows default scheme. I-beam and crosshair have no
    // aero file; the "_r" files are the plain standard ones.
    private static readonly (int Id, string File)[] Files =
    [
        (32512, "aero_arrow.cur"), // IDC_ARROW
        (32513, "beam_r.cur"), // IDC_IBEAM
        (32514, "aero_busy.ani"), // IDC_WAIT
        (32515, "cross_r.cur"), // IDC_CROSS
        (32516, "aero_up.cur"), // IDC_UPARROW
        (32642, "aero_nwse.cur"), // IDC_SIZENWSE
        (32643, "aero_nesw.cur"), // IDC_SIZENESW
        (32644, "aero_ew.cur"), // IDC_SIZEWE
        (32645, "aero_ns.cur"), // IDC_SIZENS
        (32646, "aero_move.cur"), // IDC_SIZEALL
        (32648, "aero_unavail.cur"), // IDC_NO
        (32649, "aero_link.cur"), // IDC_HAND
        (32650, "aero_working.ani"), // IDC_APPSTARTING
        (32651, "aero_helpsel.cur"), // IDC_HELP
        (32671, "aero_pin.cur"), // IDC_PIN
        (32672, "aero_person.cur"), // IDC_PERSON
    ];

    /// <summary>Standard pointer width in screen pixels at size 1× (32 px at 100 % scaling).</summary>
    public static int BaseSize() => (int)(BaseSizeAt96Dpi * PInvoke.GetDpiForSystem() / 96);

    /// <summary>Pixel size to load a standard pointer at for drawing it at <paramref name="scale"/>.</summary>
    public static int PixelsFor(double scale) => Math.Clamp((int)Math.Ceiling(BaseSize() * scale), BaseSizeAt96Dpi, MaxPixels);

    /// <summary>
    /// Loads the standard file for a system pointer handle. The caller destroys the returned
    /// cursor. 0 if the handle is no system pointer (application pointers) or the file is missing.
    /// </summary>
    public static nint TryLoad(nint handle, int pixels)
    {
        var file = FileFor(handle);
        if (file is null)
        {
            return 0;
        }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Cursors", file);
        if (!File.Exists(path))
        {
            return 0;
        }

        fixed (char* p = path)
        {
            // Without LR_SHARED the cursor is a private copy at exactly this size.
            return (nint)PInvoke.LoadImage(HINSTANCE.Null, p, GDI_IMAGE_TYPE.IMAGE_CURSOR, pixels, pixels, IMAGE_FLAGS.LR_LOADFROMFILE).Value;
        }
    }

    public static void Destroy(nint cursor) => PInvoke.DestroyCursor(new HCURSOR((void*)cursor));

    // System pointers are shared handles: GetCursorInfo returns the same handle as LoadCursor.
    // Compared on every call because Windows may swap them when the pointer settings change.
    private static string? FileFor(nint handle)
    {
        foreach (var (id, file) in Files)
        {
            if ((nint)PInvoke.LoadCursor(HINSTANCE.Null, (PCWSTR)(char*)id).Value == handle)
            {
                return file;
            }
        }

        return null;
    }
}
