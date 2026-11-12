using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Core.Tests.SecureDesktop;

public sealed class SecureLogCopyRuleTests
{
    private const string Diagnostics = @"C:\Users\anna\AppData\Local\ZoomIN\diagnostics";
    private const string Name = "secure-logs-0123456789abcdef0123456789abcdef";

    [Fact]
    public void NewFolderName_IsAllowedDirectlyInTheDiagnosticsFolder() =>
        Assert.True(SecureLogCopyRule.IsAllowedTarget(Path.Combine(Diagnostics, SecureLogCopyRule.NewFolderName()), Diagnostics));

    [Fact]
    public void TheDiagnosticsFolderCase_DoesNotMatter() =>
        Assert.True(SecureLogCopyRule.IsAllowedTarget(Diagnostics.ToUpperInvariant() + @"\" + Name, Diagnostics));

    [Theory]
    [InlineData(@"\\attacker\share\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"\\?\C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"\\.\C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\..\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\..\..\..\..\..\..\Windows\System32")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcdef\..")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcdef\x")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcde.")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789ABCDEF0123456789ABCDEF")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcdef:stream")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics\other-0123456789abcdef0123456789abcdef")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics2\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"C:\Users\bob\AppData\Local\ZoomIN\diagnostics\secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"C:/Users/anna/AppData/Local/ZoomIN/diagnostics/secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData(@"secure-logs-0123456789abcdef0123456789abcdef")]
    [InlineData("")]
    public void OtherTargets_AreRefused(string target) =>
        Assert.False(SecureLogCopyRule.IsAllowedTarget(target, Diagnostics));

    [Theory]
    [InlineData(@"\\server\home\anna\ZoomIN\diagnostics")]
    [InlineData(@"C:\Users\anna\..\bob\AppData\Local\ZoomIN\diagnostics")]
    [InlineData(@"C:\Users\anna\AppData\Local\ZoomIN\diagnostics.")]
    [InlineData(null)]
    public void AnOddDiagnosticsFolder_RefusesEverything(string? diagnostics) =>
        Assert.False(SecureLogCopyRule.IsAllowedTarget(diagnostics + @"\" + Name, diagnostics));

    [Fact]
    public void TryParse_TakesTheSwitchWithOneFolderOnly()
    {
        Assert.True(SecureLogCopyRule.TryParse([SecureLogCopyRule.Switch, @"C:\x"], out var target));
        Assert.Equal(@"C:\x", target);
        Assert.False(SecureLogCopyRule.TryParse([SecureLogCopyRule.Switch], out _));
        Assert.False(SecureLogCopyRule.TryParse([SecureLogCopyRule.Switch, @"C:\x", "more"], out _));
        Assert.False(SecureLogCopyRule.TryParse([SecureLogCopyRule.Switch, ""], out _));
    }

    [Theory]
    [InlineData("zoomin-20261112-09.log", true)]
    [InlineData("zoomin-20261112-09.trace.jsonl", true)]
    [InlineData("ZOOMIN.LOG", true)]
    [InlineData("result.png", false)]
    [InlineData("snapshot.txt", false)]
    [InlineData("settings.json", false)]
    public void OnlyLogAndOperationFiles_AreCopied(string name, bool copied) =>
        Assert.Equal(copied, SecureLogCopyRule.IsCopiedFile(name));

    [Fact]
    public void Select_TakesTheNewestThatFit()
    {
        var day = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
        (string Name, long Size, DateTimeOffset Written)[] files =
        [
            ("old", 4, day), ("big", 30, day.AddHours(2)), ("new", 5, day.AddHours(3)), ("mid", 6, day.AddHours(1)),
        ];

        var chosen = SecureLogCopyRule.Select(files, f => f.Size, f => f.Written, budget: 15);

        Assert.Equal(["new", "mid", "old"], chosen.Select(f => f.Name));
    }

    [Fact]
    public void TheLimit_Is20Megabytes() => Assert.Equal(20L * 1024 * 1024, SecureLogCopyRule.MaxBytes);
}
