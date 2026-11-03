using System.Diagnostics;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.SecureDesktop;

/// <summary>Links are created with mklink, which needs no administrator rights for /J and /H.</summary>
public sealed class GuardedFileReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ZoomIN.Tests.").FullName;

    [Fact]
    public void PlainFile_IsRead()
    {
        var path = Write("settings.json", "{}");

        Assert.Equal("{}"u8.ToArray(), GuardedFileReader.TryRead(path));
    }

    [Fact]
    public void MissingFile_ReturnsNull() =>
        Assert.Null(GuardedFileReader.TryRead(Path.Combine(_root, "missing.json")));

    [Fact]
    public void TooLargeFile_ReturnsNull()
    {
        var path = Write("big.json", new string(' ', 2048));

        Assert.Null(GuardedFileReader.TryRead(path, maxBytes: 1024));
    }

    [Fact]
    public void FileBehindJunction_ReturnsNull()
    {
        var target = Directory.CreateDirectory(Path.Combine(_root, "target")).FullName;
        File.WriteAllText(Path.Combine(target, "settings.json"), "{}");
        var junction = Path.Combine(_root, "ZoomIN");
        MkLink($"/J \"{junction}\" \"{target}\"");

        Assert.Null(GuardedFileReader.TryRead(Path.Combine(junction, "settings.json")));
    }

    [Fact]
    public void HardLink_ReturnsNull()
    {
        var original = Write("original.json", "{}");
        var link = Path.Combine(_root, "settings.json");
        MkLink($"/H \"{link}\" \"{original}\"");

        Assert.Null(GuardedFileReader.TryRead(link));
    }

    [Fact]
    public void Directory_ReturnsNull() => Assert.Null(GuardedFileReader.TryRead(_root));

    // Must be refused before any file-system call; the host name is never resolved.
    [Theory]
    [InlineData(@"\\zoomin-test.invalid\share\settings.json")]
    [InlineData(@"\\?\UNC\zoomin-test.invalid\share\settings.json")]
    [InlineData(@"\\localhost\C$\Windows\win.ini")]
    public void NetworkPaths_AreRefused(string path) => Assert.Null(GuardedFileReader.TryRead(path));

    public void Dispose()
    {
        // A recursive delete refuses junctions; removing the link alone leaves its target intact.
        foreach (var link in new DirectoryInfo(_root).EnumerateDirectories().Where(d => d.Attributes.HasFlag(FileAttributes.ReparsePoint)))
        {
            link.Delete();
        }

        Directory.Delete(_root, recursive: true);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static void MkLink(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink {arguments}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
