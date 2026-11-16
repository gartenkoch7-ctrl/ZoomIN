namespace ZoomIN.Rendering.Snapshots;

/// <summary>
/// File names inside a debug snapshot folder, shared by the writer (<see cref="SnapshotTaker"/>)
/// and the readers (KI-Debug, ADR-0027): "DISPLAY1-result.png", "DISPLAY1-info.txt", …
/// </summary>
public static class SnapshotFiles
{
    /// <summary>"\\.\DISPLAY1" → "DISPLAY1".</summary>
    public static string MonitorName(string device) => string.Concat(device.Where(char.IsLetterOrDigit));

    /// <summary>Image of one kind: "original", "result" or "weights".</summary>
    public static string Image(string folder, string device, string kind) =>
        Path.Combine(folder, $"{MonitorName(device)}-{kind}.png");

    public static string Info(string folder, string device) =>
        Path.Combine(folder, MonitorName(device) + "-info.txt");
}
