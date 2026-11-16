using System.Xml.Linq;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Core.Tests.Lifecycle;

/// <summary>The scheduled task that starts an installed ZoomIN at sign-in (ADR-0030, Q9).</summary>
public sealed class AutostartTaskTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";
    private const string Exe = @"C:\Program Files\ZoomIN\current\ZoomIN.exe";
    private static readonly XNamespace s_ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    [Fact]
    public void Name_IsPerUser()
    {
        Assert.Equal("ZoomIN Autostart " + Sid, AutostartTask.Name(Sid));
        Assert.StartsWith(AutostartTask.NamePrefix, AutostartTask.Name("S-1-5-21-9"), StringComparison.Ordinal);
    }

    [Fact]
    public void Trigger_AndPrincipal_AreThisUser_WithoutAdminRights()
    {
        var task = Parse();

        Assert.Equal(Sid, task.Descendants(s_ns + "LogonTrigger").Single().Element(s_ns + "UserId")!.Value);
        var principal = task.Descendants(s_ns + "Principal").Single();
        Assert.Equal(Sid, principal.Element(s_ns + "UserId")!.Value);
        Assert.Equal("InteractiveToken", principal.Element(s_ns + "LogonType")!.Value);
        Assert.Equal("LeastPrivilege", principal.Element(s_ns + "RunLevel")!.Value);
    }

    [Fact]
    public void Action_StartsTheExeThroughShellExecute_Quoted()
    {
        var exec = Parse().Descendants(s_ns + "Exec").Single();

        Assert.Equal(@"%SystemRoot%\System32\rundll32.exe", exec.Element(s_ns + "Command")!.Value);
        Assert.Equal($"shell32.dll,ShellExec_RunDLL \"{Exe}\"", exec.Element(s_ns + "Arguments")!.Value);
    }

    [Fact]
    public void Settings_NormalPriority_NoTimeLimit_OnBattery_NeverKilled()
    {
        var settings = Parse().Element(s_ns + "Settings")!;

        Assert.Equal("4", settings.Element(s_ns + "Priority")!.Value);
        Assert.Equal("PT0S", settings.Element(s_ns + "ExecutionTimeLimit")!.Value);
        Assert.Equal("false", settings.Element(s_ns + "DisallowStartIfOnBatteries")!.Value);
        Assert.Equal("false", settings.Element(s_ns + "StopIfGoingOnBatteries")!.Value);
        Assert.Equal("false", settings.Element(s_ns + "AllowHardTerminate")!.Value);
    }

    [Fact]
    public void Xml_DeclaresUtf16_AndEscapesThePath()
    {
        var xml = AutostartTask.Xml(Sid, @"C:\A&B\ZoomIN.exe");

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-16\"?>", xml, StringComparison.Ordinal);
        Assert.Contains("A&amp;B", xml, StringComparison.Ordinal);
        Assert.Contains(@"C:\A&B\ZoomIN.exe", XDocument.Parse(xml).Descendants(s_ns + "Arguments").Single().Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyInput_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => AutostartTask.Name(value));
        Assert.Throws<ArgumentException>(() => AutostartTask.Xml(value, Exe));
        Assert.Throws<ArgumentException>(() => AutostartTask.Xml(Sid, value));
    }

    private static XElement Parse() => XDocument.Parse(AutostartTask.Xml(Sid, Exe)).Root!;
}
