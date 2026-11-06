using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// App + window class of a window (ADR-0020): program file name and Win32 class, never the title.
/// Read once per window; process names are cached per process id while it has windows.
/// </summary>
internal sealed unsafe class WindowNames
{
    // Store apps: the frame belongs to ApplicationFrameHost, the app itself to the CoreWindow inside.
    private const string StoreFrameClass = "ApplicationFrameWindow";
    private const string StoreContentClass = "Windows.UI.Core.CoreWindow";

    private readonly Dictionary<uint, string> _processes = [];

    public WindowKind KindOf(nint handle, uint processId)
    {
        var hwnd = (HWND)handle;
        var windowClass = ClassOf(hwnd);
        if (windowClass == StoreFrameClass)
        {
            var content = PInvoke.FindWindowEx(hwnd, HWND.Null, StoreContentClass, null);
            if (content != HWND.Null)
            {
                uint contentProcess;
                _ = PInvoke.GetWindowThreadProcessId(content, &contentProcess);
                processId = contentProcess;
            }
        }

        return new WindowKind(ProcessName(processId), windowClass);
    }

    /// <summary>Forgets processes that have no windows left, so reused ids get a fresh name.</summary>
    public void KeepOnly(HashSet<uint> processIds)
    {
        // Removing while enumerating is allowed for Dictionary since .NET Core 3.0; no copy per update.
        foreach (var id in _processes.Keys)
        {
            if (!processIds.Contains(id))
            {
                _processes.Remove(id);
            }
        }
    }

    private static string ClassOf(HWND hwnd)
    {
        // Class names are at most 256 characters.
        Span<char> buffer = stackalloc char[257];
        fixed (char* chars = buffer)
        {
            var length = PInvoke.GetClassName(hwnd, chars, buffer.Length);
            return length > 0 ? new string(chars, 0, length) : string.Empty;
        }
    }

    private string ProcessName(uint processId)
    {
        if (_processes.TryGetValue(processId, out var known))
        {
            return known;
        }

        var name = ReadProcessName(processId);
        _processes[processId] = name;
        return name;
    }

    // The file name only (no path, ADR-0019). Limited access works for elevated processes too.
    private static string ReadProcessName(uint processId)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == HANDLE.Null)
        {
            return WindowKind.UnknownProcess;
        }

        try
        {
            Span<char> buffer = stackalloc char[1024];
            fixed (char* chars = buffer)
            {
                var size = (uint)buffer.Length;
                if (!PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, chars, &size))
                {
                    return WindowKind.UnknownProcess;
                }

                return Path.GetFileName(new string(chars, 0, (int)size)).ToLowerInvariant();
            }
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }
}
