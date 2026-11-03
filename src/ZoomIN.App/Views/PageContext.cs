using ZoomIN.App.Settings;
using ZoomIN.App.Updates;
using ZoomIN.App.Zoom;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Tracking;

namespace ZoomIN.App.Views;

/// <summary>Everything the settings pages need; passed as navigation parameter.</summary>
/// <param name="Settings">Shared settings and models.</param>
/// <param name="Profiles">Profile commands.</param>
/// <param name="Controller">Zoom commands, hotkey (re)registration, status.</param>
/// <param name="Autostart">Start with Windows.</param>
/// <param name="CursorShapes">Pointer images for the preview on the pointer page.</param>
/// <param name="Updates">Release list and one-click install (ADR-0014).</param>
/// <param name="UiAccess">Whether the overlay can lie above quick settings and Start (ADR-0012).</param>
/// <param name="Setup">Installs the signed build when UIAccess is missing (ADR-0017).</param>
/// <param name="WindowHandle">Settings window handle, needed by file pickers.</param>
/// <param name="WelcomeFinished">Called when the first-run assistant is done.</param>
/// <param name="ExitForUpdate">Starts the freshly installed version and ends this one.</param>
public sealed record PageContext(
    SettingsService Settings,
    ProfileService Profiles,
    ZoomController Controller,
    IAutostart Autostart,
    ICursorShapeSource CursorShapes,
    UpdateService Updates,
    IUiAccessProbe UiAccess,
    SetupLauncher Setup,
    Func<nint> WindowHandle,
    Action WelcomeFinished,
    Action ExitForUpdate);
