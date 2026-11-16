using Windows.Win32;
using Windows.Win32.UI.Shell.Common;

namespace ZoomIN.Platform.Shell;

/// <summary>
/// Shows a file in Explorer with the file selected. Asks the running shell
/// (SHOpenFolderAndSelectItems) instead of starting "explorer.exe /select,…": no quoting pitfalls,
/// and it reuses an open Explorer window (observed 2026-11-05: the /select command line reported
/// an existing AppData folder as "not available").
/// </summary>
public static unsafe class FileReveal
{
    /// <summary>False if the path could not be resolved or the shell refused.</summary>
    public static bool Reveal(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (PInvoke.SHParseDisplayName(path, null, out ITEMIDLIST* item, 0).Failed || item is null)
        {
            return false;
        }

        try
        {
            // No child items: the item itself is selected in its parent folder.
            return PInvoke.SHOpenFolderAndSelectItems(item, 0, null, 0).Succeeded;
        }
        finally
        {
            PInvoke.CoTaskMemFree(item);
        }
    }
}
