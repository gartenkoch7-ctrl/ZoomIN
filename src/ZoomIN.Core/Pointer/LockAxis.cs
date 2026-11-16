namespace ZoomIN.Core.Pointer;

/// <summary>The axis the axis lock (A6) keeps the pointer on.</summary>
public enum LockAxis
{
    /// <summary>Not decided yet: the first few pixels move freely.</summary>
    None,

    /// <summary>Only left and right; the row stays.</summary>
    Horizontal,

    /// <summary>Only up and down; the column stays.</summary>
    Vertical,
}
