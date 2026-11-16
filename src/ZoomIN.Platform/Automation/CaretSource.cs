namespace ZoomIN.Platform.Automation;

/// <summary>Where the caret of one reading came from (see <see cref="FocusReader"/>).</summary>
[Flags]
internal enum CaretSource
{
    None = 0,

    /// <summary>Win32 system caret (GetGUIThreadInfo).</summary>
    System = 1,

    /// <summary>MSAA caret object (OBJID_CARET), e.g. Chromium, Firefox.</summary>
    Msaa = 2,

    /// <summary>UI Automation text pattern, e.g. Word, terminals.</summary>
    Uia = 4,

    /// <summary>No caret: the focused cell or edit stands in for it.</summary>
    Estimated = 8,

    /// <summary>A system caret as large as a cell (Excel): followed, not drawn.</summary>
    Cell = 16,
}
