using Windows.Win32;
using Windows.Win32.Foundation;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.Platform.WindowFilters;

/// <summary>
/// What the tracker knows per window handle, from its first appearance until it closes: app +
/// class (read once), role and measurement. Tracker thread only.
/// </summary>
internal sealed class WindowStates
{
    private readonly WindowNames _names = new();
    private readonly Dictionary<nint, WindowState> _states = [];
    private readonly HashSet<uint> _processes = [];

    public Dictionary<nint, WindowState>.ValueCollection All => _states.Values;

    public bool TryGet(nint handle, out WindowState state) => _states.TryGetValue(handle, out state!);

    /// <summary>The state of a listed window, created on its first appearance.</summary>
    public WindowState Of(in RawWindow raw)
    {
        _processes.Add(raw.ProcessId);
        var known = _states.TryGetValue(raw.Handle, out var state);
        if (known && !state!.Kind.IsStoreFrame)
        {
            return state;
        }

        // A store app frame shows up before its app window moves in; until then it is asked again,
        // so the choice is remembered for the app and not for all store apps at once.
        var kind = _names.KindOf(raw.Handle, raw.ProcessId);
        if (!known || !kind.IsStoreFrame)
        {
            state = new WindowState(kind, WindowClasses.RoleOf(kind, raw.PopupStyle, raw.Owner != 0, raw.ToolWindow), WindowClasses.IsIgnored(kind.WindowClass));
            _states[raw.Handle] = state;
        }

        return state!;
    }

    /// <summary>After a listing of <paramref name="listed"/> windows: drops closed windows and their process names.</summary>
    public void EndListing(int listed)
    {
        // Hidden windows keep their state (no new looks when shown again); closed ones are dropped.
        // Removing while enumerating is allowed for Dictionary since .NET Core 3.0.
        if (_states.Count > listed)
        {
            foreach (var handle in _states.Keys)
            {
                if (!PInvoke.IsWindow((HWND)handle))
                {
                    _states.Remove(handle);
                }
            }
        }

        _names.KeepOnly(_processes);
        _processes.Clear();
    }
}
