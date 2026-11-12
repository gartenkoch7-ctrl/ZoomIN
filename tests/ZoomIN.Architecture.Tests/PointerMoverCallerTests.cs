namespace ZoomIN.Architecture.Tests;

/// <summary>
/// ADR-0028: the pointer is moved only as the direct result of a shortcut. IPointerMover may
/// therefore only be used by the App's controllers (which handle the shortcuts) and wired in the
/// composition root; checked on the source so the WinUI App project is covered too.
/// </summary>
public sealed class PointerMoverCallerTests
{
    [Fact]
    public void PointerMover_IsOnlyUsedByAppControllers()
    {
        var offenders = SourceFiles.Production()
            .Where(file => file.Text.Contains("IPointerMover", StringComparison.Ordinal))
            .Where(file => !IsAllowed(file))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        Assert.True(offenders.Count == 0, "Move the pointer only from a shortcut's controller (ADR-0028): " + string.Join(", ", offenders));
    }

    [Fact]
    public void ConcreteMover_IsOnlyWiredInTheCompositionRoot()
    {
        var offenders = SourceFiles.Production()
            .Where(file => file.Text.Contains("Win32PointerMover", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file.Path) != "Win32PointerMover.cs"
                && !(file.Project == "ZoomIN.App" && file.Path.Split(Path.DirectorySeparatorChar).Contains("Composition")))
            .Select(file => Path.GetFileName(file.Path))
            .ToList();

        Assert.True(offenders.Count == 0, "Use IPointerMover from DI (ADR-0028): " + string.Join(", ", offenders));
    }

    [Fact]
    public void SetCursorPos_IsOnlyCalledByTheMoverAndTheFenceHook()
    {
        // The fence hook only clamps a refused move to the allowed edge, as ClipCursor does.
        string[] allowed = ["Win32PointerMover.cs", "Win32FenceHook.cs"];
        var offenders = SourceFiles.Production()
            .Where(file => file.Text.Contains("SetCursorPos(", StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file.Path))
            .Where(name => !allowed.Contains(name))
            .ToList();

        Assert.True(offenders.Count == 0, "SetCursorPos outside the pointer mover (ADR-0028): " + string.Join(", ", offenders));
    }

    private static bool IsAllowed(SourceFile file)
    {
        var name = Path.GetFileName(file.Path);
        var parts = file.Path.Split(Path.DirectorySeparatorChar);
        return name is "IPointerMover.cs" or "Win32PointerMover.cs"
            || (file.Project == "ZoomIN.App" && (parts.Contains("Composition") || name.EndsWith("Controller.cs", StringComparison.Ordinal)));
    }
}
