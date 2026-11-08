using ZoomIN.Core.Hotkeys;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.WindowFilters;

namespace ZoomIN.App.Zoom;

/// <summary>The engine's input sources, started together by <see cref="ZoomController"/> (parameter object).</summary>
public sealed record InputSources(
    IMouseWheelHook Wheel, IInputActivitySource Activity, ITrackingSource Tracking, IWindowFilterSource WindowFilter);
