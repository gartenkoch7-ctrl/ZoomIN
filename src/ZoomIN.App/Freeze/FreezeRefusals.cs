using ZoomIN.Core.Islands;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Overview;
using ZoomIN.Core.Zoom;

namespace ZoomIN.App.Freeze;

/// <summary>
/// Why a monitor must not be frozen right now (G2): another view holds it, so a frozen view would not
/// match the screen. Reads the models only; the controller announces and logs the refusal.
/// </summary>
internal sealed class FreezeRefusals(ZoomModel zoom, OverviewModel overview, IslandModel islands)
{
    /// <summary>The log reason and text key of the refusal, or null if <paramref name="id"/> may be frozen.</summary>
    public (string Reason, string Text)? For(string id)
    {
        // The island edit mode (B1) confines a drag to the monitor it began on: frozen, the pointer would
        // be held between the drag's edge and the forbidden area. The user ends editing first.
        if (islands.Scene.Editing)
        {
            return ("island_edit", "Freeze.IslandEdit");
        }

        // The docked strip reserves screen space and takes every zoom command (ZoomModel.Step): frozen,
        // the reservation would follow the live mode and zooming elsewhere would go nowhere.
        if (zoom.Mode == ZoomMode.Docked && zoom.IsOn && zoom.DockedMonitor == id)
        {
            return ("docked", "Freeze.Docked");
        }

        // The overview shows 1× for a moment; frozen, its frame would stay and the view before it
        // would be lost. The user ends it first (tap again or Esc).
        if (overview.Current is { } session && session.MonitorId == id)
        {
            return ("overview", "Freeze.Overview");
        }

        // A window drag or resize (M17 G1) with its own level: the pointer jump would take the held window
        // along, and a lowered view would jump to the user's level. A drag without a level is not seen
        // (accepted, M19 plan).
        if (zoom.Temporary.LevelFor(TemporaryZoomOwner.WindowDrag, id) is not null)
        {
            return ("window_drag", "Freeze.WindowDrag");
        }

        // Another temporary level that lowers the monitor (an overview or edit mode just ending): frozen at the
        // user's level, the view would jump. The rule of ZoomModel.EffectiveTargetFor with the target read once,
        // so a zoom step meanwhile is no refusal.
        return zoom.Temporary.TryGetCeiling(id, out var ceiling) && ceiling < zoom.TargetFor(id)
            ? ("temporary_level", "Freeze.Temporary")
            : null;
    }
}
