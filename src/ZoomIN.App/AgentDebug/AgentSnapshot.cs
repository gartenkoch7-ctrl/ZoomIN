using System.Collections.Concurrent;
using System.Text;
using ZoomIN.Core.AgentDebug;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.App.AgentDebug;

/// <summary>
/// <see cref="AgentTools.TakeSnapshot"/> (ADR-0027): asks the renderers for a debug snapshot like
/// the button on the diagnostics page, then sends the images over the pipe and deletes the
/// folder, so no screen content stays on disk.
/// </summary>
public sealed class AgentSnapshot(DebugSnapshots snapshots, IAppFolders folders)
{
    // Renderers take a request within a frame or two; a paused one (nothing filtered, no zoom) never does.
    private static readonly TimeSpan s_takeWait = TimeSpan.FromSeconds(1.5);

    // Reading back and encoding large monitors can take a while on slow graphics.
    private static readonly TimeSpan s_writeWait = TimeSpan.FromSeconds(10);

    public async Task<AgentResponse> TakeAsync(AgentSnapshotOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var folder = Path.Combine(folders.Debug(), "agent-" + Guid.NewGuid().ToString("N"));
        var results = new ConcurrentBag<DebugSnapshotResult>();
        void OnCompleted(DebugSnapshotResult result) => results.Add(result);

        snapshots.Completed += OnCompleted;
        var request = snapshots.Request(folder);
        try
        {
            try
            {
                await Task.Delay(s_takeWait, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Also when cancelled (KI-Debug turned off): no renderer may take it later, and the
                // ones that took it must finish their files before the folder is deleted below.
                snapshots.Withdraw(request);
                await WaitForWritersAsync().ConfigureAwait(false);
                snapshots.Completed -= OnCompleted;
            }

            return Collect(options, folder, results.Where(r => r.Generation == request.Generation).OrderBy(r => r.Monitor, StringComparer.Ordinal));
        }
        finally
        {
            Delete(folder);
        }
    }

    // Not cancellable on purpose, but bounded: a stuck renderer must not hold the pipe forever.
    private async Task WaitForWritersAsync()
    {
        var waited = TimeSpan.Zero;
        while (snapshots.Writing > 0 && waited < s_writeWait)
        {
            await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
            waited += TimeSpan.FromMilliseconds(50);
        }
    }

    private static AgentResponse Collect(AgentSnapshotOptions options, string folder, IEnumerable<DebugSnapshotResult> results)
    {
        var text = new StringBuilder();
        var images = new List<AgentImage>();
        foreach (var result in results)
        {
            var monitor = result.Monitor.TrimStart('\\', '.');
            if (options.Monitor is { } wanted && !string.Equals(wanted, monitor, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!result.Saved)
            {
                text.Append(monitor).Append(": no image (").Append(result.Reason).Append(")\n");
                continue;
            }

            var info = SnapshotFiles.Info(folder, result.Monitor);
            if (File.Exists(info))
            {
                text.Append(File.ReadAllText(info)).Append('\n');
            }

            foreach (var kind in options.Kinds)
            {
                var file = SnapshotFiles.Image(folder, result.Monitor, kind);
                if (!File.Exists(file))
                {
                    text.Append(monitor).Append(": no ").Append(kind).Append(" image in this frame\n");
                    continue;
                }

                try
                {
                    images.Add(new AgentImage($"{monitor}-{kind}", SnapshotPng.Reframe(File.ReadAllBytes(file), options.Crop, options.MaxWidth)));
                }
                catch (InvalidDataException ex)
                {
                    return AgentResponse.Fail($"{monitor}-{kind}: {ex.Message}");
                }
            }
        }

        if (images.Count == 0 && text.Length == 0)
        {
            return AgentResponse.Fail(options.Monitor is null
                ? "No image: ZoomIN draws nothing right now (no zoom and nothing filtered on any monitor). Turn on zoom or a filter, or show a light window."
                : $"No image for {options.Monitor}: ZoomIN draws nothing there right now, or the monitor does not exist (see get_state).");
        }

        return new AgentResponse(images.Count > 0, text.ToString(), images);
    }

    private static void Delete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Only after a renderer overran the write wait; the folder holds this one snapshot only.
        }
    }
}
