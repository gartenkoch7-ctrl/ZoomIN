namespace ZoomIN.Core.Tracking;

/// <summary>
/// Global key events for the hold key (E1, low-level keyboard hook). Only observes: every key is
/// passed on unchanged. Runs only while some highlight uses <see cref="ShowCondition.HoldKey"/>.
/// </summary>
public interface IHoldKeySource : IDisposable
{
    /// <summary>Starts watching; a second call while running does nothing.</summary>
    /// <returns>False if Windows refused the hook; a later call tries again.</returns>
    bool Start(IHoldKeyHandler handler);

    /// <summary>Stops watching; nothing happens if it is not running.</summary>
    void Stop();
}
