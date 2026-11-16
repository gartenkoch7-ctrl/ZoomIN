namespace ZoomIN.Core.Tracking;

/// <summary>How the text caret is marked in the magnified image (E2). Stored by name in the settings.</summary>
public enum CaretMarkerShape
{
    /// <summary>A thick bar over the caret (as before E2).</summary>
    Bar,

    /// <summary>Two wedges above and below the caret, pointing at it.</summary>
    Wedges,

    /// <summary>A frame around the character after the caret.</summary>
    Frame,

    /// <summary>A ring around the caret.</summary>
    Circle,
}
