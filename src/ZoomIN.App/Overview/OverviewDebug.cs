using System.Globalization;
using ZoomIN.App.Settings;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Overview;

/// <summary>The overview (B2) for KI-Debug (ADR-0027): trigger it like the keys, read its state.</summary>
public sealed class OverviewDebug(OverviewController controller, SettingsService settings)
{
    private readonly OverviewModel _overview = settings.Models.Overview;

    /// <summary>Like tapping the shortcut: overview on, or zoom in at the frame.</summary>
    public string Tap()
    {
        // Only the overview this press started (time unknown = a tap), never a hold of the user.
        if (controller.Press() is { } started)
        {
            _overview.Release(started.Id, releasedAt: null);
        }

        return Describe();
    }

    /// <summary>Like Esc during the overview.</summary>
    public string Escape() => _overview.Escape() ? Describe() : "Nothing changed: no overview runs.";

    /// <summary>One line for the KI-Debug state: phase, monitor and the frame now.</summary>
    public string Describe()
    {
        if (_overview.Current is not { } session)
        {
            return "Overview: off";
        }

        var frame = controller.FrameNow(session) is { } rect ? RectText.Format(rect) : "(unknown)";
        return string.Create(CultureInfo.InvariantCulture,
            $"Overview: {_overview.Phase} on {session.MonitorId}, level before {session.LevelBefore:0.00}, frame {frame}");
    }
}
