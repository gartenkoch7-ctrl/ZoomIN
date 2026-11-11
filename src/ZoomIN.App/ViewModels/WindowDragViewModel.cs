using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.Core.WindowDrag;

namespace ZoomIN.App.ViewModels;

/// <summary>Zoom while moving windows (G1) on the page "Zoom &amp; Modi". Applies live.</summary>
public sealed partial class WindowDragViewModel : ObservableObject
{
    private readonly WindowDragZoom _drag;
    private readonly bool _ready;

    public WindowDragViewModel(WindowDragZoom drag)
    {
        _drag = drag;
        var options = drag.Options;
        ActionIndex = (int)options.Action;
        LowerLevel = options.LowerLevel;
        OnMove = options.OnMove;
        OnResize = options.OnResize;
        _ready = true;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLower), nameof(Acts))]
    public partial int ActionIndex { get; set; }

    [ObservableProperty]
    public partial double LowerLevel { get; set; }

    [ObservableProperty]
    public partial bool OnMove { get; set; }

    [ObservableProperty]
    public partial bool OnResize { get; set; }

    /// <summary>The level only matters for "lower".</summary>
    public bool IsLower => ActionIndex == (int)WindowDragAction.Lower;

    /// <summary>The two switches only matter if the zoom changes at all.</summary>
    public bool Acts => ActionIndex != (int)WindowDragAction.Unchanged;

    partial void OnActionIndexChanged(int value)
    {
        if (Enum.IsDefined((WindowDragAction)value))
        {
            Apply(options => options with { Action = (WindowDragAction)value });
        }
        else if (_ready)
        {
            // The combo box reports -1 while its items are replaced: show the stored action again.
            ActionIndex = (int)_drag.Options.Action;
        }
    }

    partial void OnLowerLevelChanged(double value) => Apply(options => options with { LowerLevel = value });

    partial void OnOnMoveChanged(bool value) => Apply(options => options with { OnMove = value });

    partial void OnOnResizeChanged(bool value) => Apply(options => options with { OnResize = value });

    // Only the changed field: the page may show older values after a profile switch, and those
    // must not overwrite the new profile's other settings.
    private void Apply(Func<WindowDragOptions, WindowDragOptions> change)
    {
        if (_ready)
        {
            _drag.Update(change);
        }
    }
}
