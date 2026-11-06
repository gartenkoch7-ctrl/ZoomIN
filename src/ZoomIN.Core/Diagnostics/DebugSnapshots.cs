namespace ZoomIN.Core.Diagnostics;

/// <summary>One request for a debug snapshot: where to save it, numbered so each renderer takes it once.</summary>
/// <param name="Generation">Increases with every request.</param>
/// <param name="Folder">Folder the renderers write their files into.</param>
public sealed record DebugSnapshotRequest(long Generation, string Folder);

/// <summary>What one monitor's renderer did with a request.</summary>
/// <param name="Monitor">Device name of the monitor.</param>
/// <param name="Saved">Files written; false if ZoomIN drew nothing there or writing failed.</param>
/// <param name="Reason">"saved", "nothing_drawn" or a short technical error name.</param>
public sealed record DebugSnapshotResult(long Generation, string Monitor, bool Saved, string Reason);

/// <summary>
/// Debug snapshots (M13): on an explicit request from the diagnostics page, every renderer saves
/// what it draws – original, result and smart inversion weights – so the look can be checked
/// although the overlay is hidden from screen captures. The UI writes the request, render
/// threads read <see cref="Pending"/> lock-free once per frame (no allocation).
/// </summary>
public sealed class DebugSnapshots
{
    private DebugSnapshotRequest? _pending;
    private long _generation;

    /// <summary>Raised on a background thread for each monitor that handled a request.</summary>
    public event Action<DebugSnapshotResult>? Completed;

    /// <summary>The latest request, or null if there never was one. Per frame, no allocation.</summary>
    public DebugSnapshotRequest? Pending => Volatile.Read(ref _pending);

    /// <summary>Asks every renderer to save its next frame into <paramref name="folder"/>.</summary>
    public DebugSnapshotRequest Request(string folder)
    {
        var request = new DebugSnapshotRequest(Interlocked.Increment(ref _generation), folder);
        Volatile.Write(ref _pending, request);
        return request;
    }

    /// <summary>Called by a renderer when it is done with a request.</summary>
    public void Report(DebugSnapshotResult result) => Completed?.Invoke(result);
}
