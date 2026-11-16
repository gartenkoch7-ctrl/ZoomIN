namespace ZoomIN.Platform.Lifecycle;

/// <summary>Runs schtasks.exe; a fake in the tests, so they never register real tasks.</summary>
public interface ISchtasks
{
    /// <returns>The exit code; 0 is success, schtasks /Query returns 1 for a missing task.</returns>
    int Run(IReadOnlyList<string> arguments);
}
