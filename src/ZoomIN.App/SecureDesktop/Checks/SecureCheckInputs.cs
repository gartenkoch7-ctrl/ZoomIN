using ZoomIN.Core.Hotkeys;

namespace ZoomIN.App.SecureDesktop.Checks;

/// <summary>What the secure host knows from its own start, for <see cref="SecureDesktopChecks"/>.</summary>
/// <param name="WatchdogId">Process id of the watchdog, or null if none was started.</param>
/// <param name="Unavailable">Shortcuts of the engine Windows refused (another program holds them).</param>
/// <param name="ExitShortcut">Strg+Alt+Umschalt+Z was registered.</param>
/// <param name="SnapshotShortcut">Strg+Alt+Umschalt+D was registered.</param>
internal sealed record SecureCheckInputs(int? WatchdogId, IReadOnlyList<HotkeyGesture> Unavailable, bool ExitShortcut, bool SnapshotShortcut);
