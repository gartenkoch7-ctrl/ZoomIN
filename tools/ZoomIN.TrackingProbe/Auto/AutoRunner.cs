using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Accessibility;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Core.Tracking;
using ZoomIN.Core.Viewport;
using ZoomIN.Platform.Automation;
using ZoomIN.TrackingProbe.Interop;

namespace ZoomIN.TrackingProbe.Auto;

/// <summary>
/// Starts each installed matrix application itself, brings it to the front and measures every
/// caret source – first without, then with the Chromium nudge – plus what <see cref="FocusReader"/>
/// (i.e. ZoomIN) gets. Only processes it started itself are closed again; nothing is typed.
/// </summary>
internal static class AutoRunner
{
    private const int SamplesPerPhase = 8;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(250);

    public static List<AutoResult> Run(string work, string? only)
    {
        var results = new List<AutoResult>();
        foreach (var scenario in AutoScenarios.Create(work).Where(s => only is null || s.Name.StartsWith(only, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine($"→ {scenario.Name} …");
            var result = new AutoResult(scenario);
            results.Add(result);
            try
            {
                Measure(scenario, result);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or COMException)
            {
                result.Problem = ex.Message;
            }

            Console.WriteLine($"  {(result.Passed ? "OK" : "FEHLER")}  vorher {result.Before}  nachher {result.After}  {result.Problem}");
        }

        return results;
    }

    private static void Measure(AutoScenario scenario, AutoResult result)
    {
        var existing = Process.GetProcessesByName(scenario.ProcessName).Select(p => p.Id).ToHashSet();
        using var launcher = Process.Start(new ProcessStartInfo(scenario.FileName, scenario.Arguments) { UseShellExecute = true });
        var (process, window) = AppWindows.WaitFor(scenario.ProcessName, existing, TimeSpan.FromSeconds(30));
        try
        {
            if (process is null || window == 0)
            {
                result.Problem = "kein neues Fenster gefunden";
                return;
            }

            Thread.Sleep(TimeSpan.FromSeconds(scenario.SettleSeconds));

            // Some applications hand over to a second process of the same name while starting
            // (Excel). If one of those new processes is in front, that window is the one to measure.
            if (AppWindows.NewProcessInFront(scenario.ProcessName, existing) is { } front)
            {
                (process, window) = (front, ProbeNative.GetForegroundWindow());
            }
            else if (process.HasExited || process.MainWindowHandle == 0)
            {
                process.Dispose();
                (process, window) = AppWindows.WaitFor(scenario.ProcessName, existing, TimeSpan.FromSeconds(15));
                if (process is null || window == 0)
                {
                    result.Problem = "Fenster nach dem Start verschwunden";
                    return;
                }
            }

            if (!AppWindows.BringToFront(window))
            {
                result.Problem = $"Fenster nicht im Vordergrund, vorn: {AppWindows.Describe(ProbeNative.GetForegroundWindow())}";
                return;
            }

            // The process may show its work in another top-level window than the one Windows calls
            // its main window (Excel); measure against the window that is actually in front.
            window = ProbeNative.GetForegroundWindow();
            ProbeNative.GetWindowRect(window, out var w);
            result.Window = new PhysicalRect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
            Sample(result.Before, null, window);

            var chromium = new ChromiumAccessibility();
            Sample(result.After, chromium, window);
            ReadLikeZoomIn(result, window);
            result.CaretNews = CountCaretNews(window);
        }
        finally
        {
            AppWindows.Close(process);
            AppWindows.CloseNew(scenario.ProcessName, existing);
        }
    }

    private static void Sample(SourceHits hits, ChromiumAccessibility? chromium, nint window)
    {
        var automation = (IUIAutomation)new CUIAutomation();
        try
        {
            for (var i = 0; i < SamplesPerPhase; i++)
            {
                if (!AppWindows.BringToFront(window))
                {
                    continue;
                }

                var system = GuiThreadCaret.Read(out var focusWindow);
                chromium?.Touch(focusWindow);
                hits.Samples++;
                hits.System += system is null ? 0 : 1;
                var msaa = MsaaCaret.Read(focusWindow);
                hits.Msaa += msaa is null ? 0 : 1;
                var element = automation.GetFocusedElement();
                try
                {
                    var uia = UiaTextCaret.Read(element);
                    hits.Uia += uia is null ? 0 : 1;
                    hits.Spread = Math.Max(hits.Spread, Math.Max(Spread(system, msaa), Math.Max(Spread(system, uia), Spread(msaa, uia))));
                    Console.WriteLine($"    S {system}  M {msaa}  U {uia}");
                    var r = element.CurrentBoundingRectangle;
                    hits.Focus += r.right > r.left ? 1 : 0;
                    hits.FocusInfo = $"{element.CurrentControlType.ToString().Replace("UIA_", string.Empty, StringComparison.Ordinal).Replace("ControlTypeId", string.Empty, StringComparison.Ordinal)} „{element.CurrentName}“";
                }
                finally
                {
                    Marshal.ReleaseComObject(element);
                }

                // The nudge needs a moment until Chromium has built its tree.
                Thread.Sleep(chromium is not null && i == 0 ? TimeSpan.FromSeconds(1.5) : SampleInterval);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(automation);
        }
    }

    private static void ReadLikeZoomIn(AutoResult result, nint window)
    {
        using var reader = new FocusReader((IUIAutomation)new CUIAutomation());
        for (var i = 0; i < SamplesPerPhase; i++)
        {
            if (!AppWindows.BringToFront(window))
            {
                continue;
            }

            var reading = reader.Read();
            Console.WriteLine($"    ZoomIN {reading}");
            result.Readings++;
            result.Caret = reading.Caret ?? result.Caret;
            result.Estimated = reading.CaretEstimated;
            if (reading.Caret is { } c && result.Window is { } w && Plausible(c, w))
            {
                result.Valid++;
            }

            Thread.Sleep(SampleInterval);
        }
    }

    /// <summary>
    /// Runs ZoomIN's real tracking thread for 3 s without typing and counts how often it reports the
    /// caret as moved after the first report. A blinking caret must count 0 (CaretMotion).
    /// </summary>
    private static int CountCaretNews(nint window)
    {
        var model = new TrackingModel(TrackingOptions.Default);
        using var source = new UiaTrackingSource(NullLogger<UiaTrackingSource>.Instance);
        source.Start(model);
        var first = 0L;
        var news = 0;
        var last = 0L;
        var until = Stopwatch.GetTimestamp() + (3 * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < until)
        {
            AppWindows.BringToFront(window);
            var at = model.Snapshot.CaretChangedAt;
            if (at != last)
            {
                news += first == 0 ? 0 : 1;
                first = first == 0 ? at : first;
                last = at;
            }

            Thread.Sleep(20);
        }

        return news;
    }

    private static bool Plausible(PhysicalRect caret, PhysicalRect window) =>
        caret.X >= window.X && caret.Right <= window.Right && caret.Y >= window.Y && caret.Bottom <= window.Bottom
        && caret.Height >= 4 && caret.Height <= 200;

    // Largest distance between the top-left corners of two sources that both answered.
    private static double Spread(PhysicalRect? a, PhysicalRect? b) =>
        a is { } x && b is { } y ? Math.Max(Math.Abs(x.X - y.X), Math.Abs(x.Y - y.Y)) : 0;
}
