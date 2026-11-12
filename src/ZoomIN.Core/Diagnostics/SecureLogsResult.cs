namespace ZoomIN.Core.Diagnostics;

/// <summary>How the request for the secure instance's logs ended (M10, Q7).</summary>
public enum SecureLogsResult
{
    /// <summary>The elevated step copied them (possibly none, if the secure instance never ran).</summary>
    Copied,

    /// <summary>The user said "No" at the administrator prompt.</summary>
    Declined,

    /// <summary>The step could not start, refused the folder or failed while copying.</summary>
    Failed,
}
