using System.Text.RegularExpressions;

namespace ZoomIN.Architecture.Tests;

/// <summary>
/// ADR-0025 / AGENTS.md 2.6: static classes only for code without side effects. Files, registry,
/// processes, environment and network belong in services behind interfaces (DI). Dialogs, clock
/// and language are left to the review: they cannot be told apart reliably from source text.
/// </summary>
public sealed partial class StaticClassTests
{
    // Known cases, each with the M14 step that turns it into a service. The list may only shrink:
    // StaleExceptions fails as soon as an entry no longer offends.
    private static readonly Dictionary<string, string> s_exceptions = new(StringComparer.Ordinal)
    {
        // Empty since M14 step 10; new entries need a step that removes them again.
    };

    [Fact]
    public void StaticClasses_HaveNoSideEffects()
    {
        var offenders = StaticClassesWithSideEffects()
            .Where(path => !s_exceptions.ContainsKey(path))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Static classes must not use files, registry, processes, environment or network (ADR-0025); "
            + "make a service with an interface: " + string.Join(", ", offenders));
    }

    [Fact]
    public void StaticHttpClients_AreNotUsed()
    {
        var offenders = SourceFiles.Production()
            .Where(file => StaticHttpClient().IsMatch(file.Text) && !IsAllowedPlace(file.Path))
            .Select(RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0, "Take the HttpClient from DI, not a static field (ADR-0025): " + string.Join(", ", offenders));
    }

    [Fact]
    public void Exceptions_AreNotStale()
    {
        var current = StaticClassesWithSideEffects().ToHashSet(StringComparer.Ordinal);
        var stale = s_exceptions.Keys.Where(path => !current.Contains(path)).ToList();

        Assert.True(stale.Count == 0, "Fixed – remove from the exception list in StaticClassTests: " + string.Join(", ", stale));
    }

    private static IEnumerable<string> StaticClassesWithSideEffects() =>
        SourceFiles.Production()
            .Where(file => StaticClass().IsMatch(file.Text) && SideEffect().IsMatch(file.Text) && !IsAllowedPlace(file.Path))
            .Select(RelativePath);

    // Interop (P/Invoke), entry points and the composition root may be static and touch the system.
    private static bool IsAllowedPlace(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        return parts.Contains("Interop") || parts.Contains("Composition") || parts[^1] == "Program.cs";
    }

    private static string RelativePath(SourceFile file)
    {
        var parts = file.Path.Split(Path.DirectorySeparatorChar);
        var start = Array.IndexOf(parts, file.Project);
        return string.Join('/', parts[start..]);
    }

    [GeneratedRegex(@"\bstatic\s+(partial\s+)?class\b")]
    private static partial Regex StaticClass();

    [GeneratedRegex(@"\b(File|Directory)\.[A-Z]|\bRegistry(Key)?\b|\bProcess\.(Start|GetCurrentProcess|GetProcess)|\bProcessStartInfo\b|Environment\.(GetFolderPath|GetEnvironmentVariable|ExpandEnvironmentVariables)|\b\w*HttpClient\b")]
    private static partial Regex SideEffect();

    // Subclasses count too (UpdateHttpClient).
    [GeneratedRegex(@"\bstatic\s+(readonly\s+)?\w*HttpClient\b")]
    private static partial Regex StaticHttpClient();
}
