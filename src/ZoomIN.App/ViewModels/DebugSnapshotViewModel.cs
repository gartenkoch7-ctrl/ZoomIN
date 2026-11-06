using System.Collections.Concurrent;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ZoomIN.App.Localization;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Filters;
using ZoomIN.Platform.Shell;

namespace ZoomIN.App.ViewModels;

/// <summary>
/// "Debug snapshot" on the diagnostics section (M13): after a countdown – time to bring the
/// window to the front – every monitor saves original, result and smart inversion weights to
/// %LocalAppData%\ZoomIN\debug. Contains screen content, so only ever on this button.
/// </summary>
public sealed partial class DebugSnapshotViewModel(DebugSnapshots snapshots, SmartInvertHealth smartInvert) : ObservableObject
{
    private const int CountdownSeconds = 5;

    // Renderers answer within a frame or two; a paused one (nothing filtered) never answers.
    private static readonly TimeSpan s_answerWait = TimeSpan.FromSeconds(2);

    /// <summary>Root folder of all debug snapshots.</summary>
    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoomIN", "debug");

    /// <summary>Whether the smart inversion runs, or why a monitor fell back to "keep hue" (ADR-0024).</summary>
    public string SmartInvertStatus { get; } = Describe(smartInvert.Fallbacks);

    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanTake { get; set; } = true;

    public async void Take()
    {
        CanTake = false;
        try
        {
            for (var left = CountdownSeconds; left > 0; left--)
            {
                Status = Text.Format("Snapshot.Countdown", left);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            var folder = Path.Combine(Folder, "snapshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            // Collected first and filtered afterwards: a renderer may answer before Request returns.
            var results = new ConcurrentBag<DebugSnapshotResult>();
            void OnCompleted(DebugSnapshotResult result) => results.Add(result);

            snapshots.Completed += OnCompleted;
            DebugSnapshotRequest request;
            try
            {
                request = snapshots.Request(folder);
                await Task.Delay(s_answerWait);
            }
            finally
            {
                snapshots.Completed -= OnCompleted;
            }

            var saved = results.Count(r => r.Saved && r.Generation == request.Generation);
            Status = saved > 0 ? Text.Format("Snapshot.Saved", saved, Path.GetFileName(folder)) : Text.Get("Snapshot.Nothing");
            if (saved > 0)
            {
                FileReveal.Reveal(folder);
            }
        }
        finally
        {
            CanTake = true;
        }
    }

    private static string Describe(IReadOnlyList<SmartInvertFallback> fallbacks) =>
        fallbacks.Count == 0
            ? Text.Get("SmartInvert.Active")
            : string.Join(" ", fallbacks.Select(f => f.Reason == "budget"
                ? Text.Format("SmartInvert.Budget", MonitorLabel(f.Monitor), f.Milliseconds.ToString("0.0", CultureInfo.CurrentCulture))
                : Text.Format("SmartInvert.Feature", MonitorLabel(f.Monitor))));

    // "\\.\DISPLAY2" -> "DISPLAY2"
    private static string MonitorLabel(string device) => device.TrimStart('\\', '.');
}