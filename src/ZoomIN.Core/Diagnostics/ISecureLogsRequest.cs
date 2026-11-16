namespace ZoomIN.Core.Diagnostics;

/// <summary>
/// Asks for administrator rights to copy the secure instance's logs out of SYSTEM's profile
/// (M10, Q7), for the diagnostics package. Blocks until done; call off the UI thread.
/// </summary>
public interface ISecureLogsRequest
{
    /// <param name="target">A new folder "secure-logs-…" inside the diagnostics folder (SecureLogCopyRule).</param>
    SecureLogsResult Copy(string target);
}
