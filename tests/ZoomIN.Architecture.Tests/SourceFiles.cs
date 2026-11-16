namespace ZoomIN.Architecture.Tests;

/// <summary>The C# sources of the shipped projects under src/ (no tests, tools or generated code).</summary>
internal static class SourceFiles
{
    private static readonly Lazy<IReadOnlyList<SourceFile>> s_production = new(Load);

    public static IReadOnlyList<SourceFile> Production() => s_production.Value;

    private static IReadOnlyList<SourceFile> Load()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        return Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path) && !path.EndsWith(".g.cs", StringComparison.Ordinal))
            .Select(path => new SourceFile(path, ProjectOf(src, path), File.ReadAllText(path)))
            .ToList();
    }

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "ZoomIN.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("ZoomIN.slnx not found above " + AppContext.BaseDirectory);
    }

    private static string ProjectOf(string src, string path) =>
        Path.GetRelativePath(src, path).Split(Path.DirectorySeparatorChar)[0];

    private static bool IsBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        return parts.Contains("obj") || parts.Contains("bin");
    }
}

/// <param name="Project">Folder name under src/, e.g. "ZoomIN.Platform".</param>
internal sealed record SourceFile(string Path, string Project, string Text);
