namespace ZoomIN.Core.Islands;

/// <summary>What one input did in the island edit mode; the controller announces and logs it.</summary>
public enum IslandEditOutcome
{
    /// <summary>Nothing to do (a click beside every island, the wheel over empty space).</summary>
    None,

    /// <summary>A drag moved on or a click ended a rubber band: shown, not announced or saved.</summary>
    Dragged,

    /// <summary>An island was picked (press on it); the selection is announced.</summary>
    Selected,

    /// <summary>A rubber band became a new island.</summary>
    Created,

    /// <summary>A rubber band was dropped because all places are taken (hint to the user).</summary>
    Full,

    /// <summary>A move or resize ended.</summary>
    Changed,

    /// <summary>The wheel changed the level of an island.</summary>
    LevelChanged,

    /// <summary>Del removed the selected island.</summary>
    Deleted,

    /// <summary>Esc during a drag: the drag is undone, the edit mode goes on.</summary>
    Cancelled,

    /// <summary>Esc with no drag: the edit mode ends.</summary>
    End,
}
