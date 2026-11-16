using ZoomIN.Core.Settings;
using ZoomIN.Core.Zoom;

namespace ZoomIN.Core.WindowDrag;

/// <summary>Maps between the persisted <see cref="AppSettings"/> and the runtime <see cref="WindowDragZoom"/> (G1).</summary>
public static class WindowDragMapping
{
    public static WindowDragZoom Create(AppSettings settings, ZoomModel model) => new(model, Options(settings));

    /// <summary>The options of the settings; a missing or null entry (older files) gives the defaults.</summary>
    public static WindowDragOptions Options(AppSettings settings) => (settings.WindowDrag ?? WindowDragOptions.Default).Normalized();

    public static AppSettings Apply(AppSettings settings, WindowDragZoom drag) => settings with { WindowDrag = drag.Options };
}
