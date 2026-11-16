using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Tests.Folders;

namespace ZoomIN.Platform.Tests.Diagnostics;

/// <summary>
/// The elevated copy of the secure instance's logs (M10, Q7), run here without elevation: the new
/// folder gets an access list this test may write to. The root lies next to the test binaries, on
/// a local fixed disk with its long path (no short names, no redirected AppData).
/// </summary>
public sealed class SecureLogCopyTests : IDisposable
{
    private readonly string _root = Path.Combine(AppContext.BaseDirectory, "SecureLogCopyTests-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _junctions = [];
    private readonly TestFolders _folders;
    private readonly string _system;

    public SecureLogCopyTests()
    {
        _folders = new TestFolders(_root);
        _system = Path.Combine(_root, "system");
        Directory.CreateDirectory(_folders.Diagnostics());
        Write(Path.Combine(_system, "logs", "zoomin-20261112-09.log"), "secure log");
        Write(Path.Combine(_system, "logs", "zoomin-20261112-09.trace.jsonl"), "{}");
        Write(Path.Combine(_system, "logs", "notes.txt"), "not copied");
        Write(Path.Combine(_system, "debug", "secure-snapshot-1", "info.log"), "snapshot info");
        Write(Path.Combine(_system, "debug", "secure-snapshot-1", "result.png"), "not copied");
    }

    public void Dispose()
    {
        // The links first and on their own: a recursive delete refuses them.
        foreach (var junction in _junctions.Where(Directory.Exists))
        {
            Directory.Delete(junction);
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LogsAndOperations_AreCopiedFlat_WithTheirOrigin()
    {
        var target = Target();

        Assert.Equal(SecureLogCopy.Copied, Copy().Run(target));

        var names = Directory.GetFiles(target).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(["debug-secure-snapshot-1_info.log", "logs-zoomin-20261112-09.log", "logs-zoomin-20261112-09.trace.jsonl"], names);
        Assert.Equal("secure log", File.ReadAllText(Path.Combine(target, "logs-zoomin-20261112-09.log")));
    }

    [Fact]
    public void AnExistingTarget_IsRefused_AndLeftAlone()
    {
        var target = Target();
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "planted.log"), "x");

        Assert.Equal(SecureLogCopy.Refused, Copy().Run(target));
        Assert.Equal(["planted.log"], Directory.GetFiles(target).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData(@"\\localhost\C$\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"C:\Windows\Temp\secure-logs-0123456789abcdef0123456789abcdef")]
    public void TargetsOutsideTheDiagnosticsFolder_AreRefused(string target) =>
        Assert.Equal(SecureLogCopy.Refused, Copy().Run(target));

    [Fact]
    public void TargetBelowDotDot_IsRefused() =>
        Assert.Equal(SecureLogCopy.Refused, Copy().Run(_folders.Diagnostics() + @"\..\" + SecureLogCopyRule.NewFolderName()));

    [Fact]
    public void ADiagnosticsFolderThatIsAJunction_IsRefused()
    {
        var elsewhere = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;
        Directory.Delete(_folders.Diagnostics());
        if (!MakeJunction(_folders.Diagnostics(), elsewhere))
        {
            Assert.Skip("mklink /J is not available here.");
        }

        Assert.Equal(SecureLogCopy.Refused, Copy().Run(Target()));
        Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
    }

    [Fact]
    public void AParentFolderThatIsAJunction_IsRefused()
    {
        // ZoomIN\diagnostics reached through a junction one level up.
        var real = Directory.CreateDirectory(Path.Combine(_root, "real", "diagnostics")).FullName;
        var linkedRoot = Path.Combine(_root, "linked");
        if (!MakeJunction(linkedRoot, Path.GetDirectoryName(real)!))
        {
            Assert.Skip("mklink /J is not available here.");
        }

        var folders = new TestFolders(linkedRoot);
        var copy = new SecureLogCopy(folders, _system, UserMayWrite);

        Assert.Equal(SecureLogCopy.Refused, copy.Run(Path.Combine(folders.Diagnostics(), SecureLogCopyRule.NewFolderName())));
        Assert.Empty(Directory.EnumerateFileSystemEntries(real));
    }

    [Fact]
    public void AJunctionAtTheTargetName_IsRefused_AndNothingIsWrittenThrough()
    {
        var elsewhere = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;
        var target = Target();
        if (!MakeJunction(target, elsewhere))
        {
            Assert.Skip("mklink /J is not available here.");
        }

        Assert.Equal(SecureLogCopy.Refused, Copy().Run(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
    }

    [Fact]
    public void NoSecureLogsYet_CopiesNothing_ButSucceeds()
    {
        Directory.Delete(_system, recursive: true);
        var target = Target();

        Assert.Equal(SecureLogCopy.Copied, Copy().Run(target));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    private SecureLogCopy Copy() => new(_folders, _system, UserMayWrite);

    private string Target() => Path.Combine(_folders.Diagnostics(), SecureLogCopyRule.NewFolderName());

    // Production grants the user read and delete only; this unelevated test must write.
    private static DirectorySecurity UserMayWrite()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    // Junctions need no administrator rights (unlike symbolic links); .NET has no API for them.
    private bool MakeJunction(string link, string target)
    {
        _junctions.Add(link);
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        })!;
        process.WaitForExit();
        return process.ExitCode == 0 && Directory.Exists(link);
    }
}
