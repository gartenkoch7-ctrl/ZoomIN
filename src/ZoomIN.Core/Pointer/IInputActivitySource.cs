namespace ZoomIN.Core.Pointer;

/// <summary>
/// Global mouse movement and typing (low-level hooks), for the locator triggers and for hiding
/// the pointer while typing. Nothing is swallowed or changed (the one exception, Esc during the
/// overview, belongs to <see cref="Overview.IOverviewKeys"/> on the same hook).
/// </summary>
public interface IInputActivitySource : IDisposable
{
    void Start(IInputActivityHandler handler);
}
