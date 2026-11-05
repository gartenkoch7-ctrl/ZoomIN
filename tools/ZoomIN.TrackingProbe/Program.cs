using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.Automation;
using ZoomIN.TrackingProbe;
using ZoomIN.TrackingProbe.Auto;

// Usage: ZoomIN.TrackingProbe [--no-activate]
// Click into each application, tab around and type a few characters; press Enter here to finish.
// ZoomIN.TrackingProbe --auto [--only Name]: starts the installed matrix apps itself and measures them (AutoRunner).
if (args.Contains("--auto", StringComparer.OrdinalIgnoreCase))
{
    var work = Directory.CreateTempSubdirectory("ZoomIN-Probe-").FullName;
    var only = args.SkipWhile(a => !a.Equals("--only", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
    var markdown = AutoResult.ToMarkdown(AutoRunner.Run(work, only));
    var autoReport = Path.Combine(Environment.CurrentDirectory, $"tracking-auto-{DateTime.Now:yyyyMMdd-HHmmss}.md");
    File.WriteAllText(autoReport, markdown);
    Console.WriteLine();
    Console.WriteLine(markdown);
    Console.WriteLine($"Gespeichert: {autoReport}");
    try
    {
        Directory.Delete(work, recursive: true);
    }
    catch (IOException)
    {
        // A browser may still hold its profile for a moment; the temp folder is cleaned up by Windows.
    }

    return;
}

var activate = !args.Contains("--no-activate", StringComparer.OrdinalIgnoreCase);
var automation = (IUIAutomation)new CUIAutomation();
var chromium = new ChromiumAccessibility();
var report = new ProbeReport();
var names = new Dictionary<uint, string>();
var ownProcess = (uint)Environment.ProcessId;
string? last = null;

Console.WriteLine($"ZoomIN Tracking-Probe – Chromium-Anstoß {(activate ? "an" : "aus")}. Enter beendet.");
while (!(Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter))
{
    Thread.Sleep(250);
    if (Sample() is { } sample)
    {
        report.Add(sample);
        var line = sample.Describe();
        if (line != last)
        {
            Console.WriteLine(line);
            last = line;
        }
    }
}

var path = Path.Combine(Environment.CurrentDirectory, $"tracking-probe-{DateTime.Now:yyyyMMdd-HHmmss}.md");
File.WriteAllText(path, report.ToMarkdown());
Console.WriteLine();
Console.WriteLine(report.ToMarkdown());
Console.WriteLine($"Gespeichert: {path}");
Marshal.ReleaseComObject(automation);

ProbeSample? Sample()
{
    var foreground = PInvoke.GetForegroundWindow();
    PInvoke.GetWindowThreadProcessId(foreground, out var processId);
    if (foreground.IsNull || processId == ownProcess)
    {
        return null;
    }

    var systemCaret = GuiThreadCaret.Read(out var focusWindow);
    var isChromium = activate ? chromium.Touch(focusWindow) : ChromiumAccessibility.IsChromium(focusWindow);
    var (focus, uiaCaret) = ReadAutomation();
    return new ProbeSample(NameOf(processId), isChromium, focus, systemCaret, uiaCaret, MsaaCaret.Read(focusWindow));
}

(PhysicalRect? Focus, PhysicalRect? Caret) ReadAutomation()
{
    try
    {
        var element = automation.GetFocusedElement();
        try
        {
            var r = element.CurrentBoundingRectangle;
            PhysicalRect? focus = r.right > r.left && r.bottom > r.top
                ? new PhysicalRect(r.left, r.top, r.right - r.left, r.bottom - r.top)
                : null;
            return (focus, UiaTextCaret.Read(element));
        }
        finally
        {
            Marshal.ReleaseComObject(element);
        }
    }
    catch (Exception ex) when (ex is COMException or InvalidCastException)
    {
        return (null, null);
    }
}

string NameOf(uint processId)
{
    if (!names.TryGetValue(processId, out var name))
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            name = process.ProcessName;
        }
        catch (ArgumentException)
        {
            name = $"pid {processId}";
        }

        names[processId] = name;
    }

    return name;
}
