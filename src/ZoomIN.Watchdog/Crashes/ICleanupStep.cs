namespace ZoomIN.Watchdog.Crashes;

/// <summary>
/// Repairs system state a crashed ZoomIN may leave behind. M4 adds "release AppBar space",
/// M5 adds "show the system cursor again" (ADR-0007).
/// </summary>
public interface ICleanupStep
{
    string Name { get; }

    void Run();
}
