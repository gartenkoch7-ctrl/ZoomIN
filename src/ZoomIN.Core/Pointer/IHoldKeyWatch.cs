namespace ZoomIN.Core.Pointer;

/// <summary>
/// Watches one key the user holds (axis lock, A6) without swallowing anything. Reports presses
/// and releases; a release it missed (e.g. while another desktop was active) is reported once it
/// notices.
/// </summary>
public interface IHoldKeyWatch : IDisposable
{
    /// <summary>
    /// Watches <paramref name="key"/> from now on, replacing an earlier watch. <paramref name="changed"/>
    /// gets true on press and false on release; it runs on a hook thread and must only set a flag.
    /// </summary>
    void Start(AxisLockKey key, Action<bool> changed);

    /// <summary>
    /// Reports a key still held as released and keeps watching (after a desktop switch Windows
    /// delivered no key events); a key still held engages again with its next repeat.
    /// </summary>
    void Reset();

    /// <summary>Stops watching; a key still held is reported as released.</summary>
    void Stop();
}
