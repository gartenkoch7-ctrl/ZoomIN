namespace ZoomIN.Core.Tracking;

/// <summary>Focus and caret tracking (M6 decisions).</summary>
/// <param name="Enabled">Strg+Alt+T switches focus and caret tracking on/off; the mouse is always tracked.</param>
/// <param name="Focus">Follow the keyboard focus.</param>
/// <param name="Caret">Follow the text caret while typing.</param>
/// <param name="PanSeconds">Glide time when the view moves to focus/caret/mouse; 0 = jump.</param>
/// <param name="CaretMargin">The view only moves when the caret comes this close to its edge (0.2 = 20 %).</param>
/// <param name="FocusFrame">Draw a frame around the focused element.</param>
/// <param name="CaretMarker">Draw a thick bar over the text caret.</param>
public sealed record TrackingOptions(
    bool Enabled, bool Focus, bool Caret, double PanSeconds, double CaretMargin, bool FocusFrame, bool CaretMarker)
{
    public static TrackingOptions Default { get; } = new(true, true, true, 0.2, 0.2, true, true);
}
