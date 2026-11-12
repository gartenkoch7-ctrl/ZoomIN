using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Folders;

namespace ZoomIN.App.SecureDesktop.Checks;

/// <summary>
/// Strg+Alt+Umschalt+D on the secure desktop only (M10, Q8): every renderer saves what it draws,
/// like the button on the diagnostics page, into the debug folder of the running account – SYSTEM's
/// profile when Windows started the instance, which only administrators can read.
/// </summary>
internal sealed class SecureSnapshotShortcut(DebugSnapshots snapshots, IAppFolders folders, TimeProvider time, ILogger<SecureSnapshotShortcut> log)
{
    // Renderers answer within a frame or two; a sleeping one (nothing zoomed or filtered) never does.
    private static readonly TimeSpan s_answerWait = TimeSpan.FromSeconds(2);

    // Reading back and encoding large monitors can take a while on slow graphics (as AgentSnapshot).
    private static readonly TimeSpan s_writeWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan s_writePoll = TimeSpan.FromMilliseconds(50);

    /// <summary>Snapshots kept, including the new one: anybody at the sign-in screen can press the shortcut.</summary>
    internal const int Kept = 3;

    private const string Prefix = "secure-snapshot-";

    private int _taking;

    /// <summary>Hotkey thread: starts one snapshot; presses while one is running are ignored.</summary>
    public void Take()
    {
        if (Interlocked.Exchange(ref _taking, 1) == 0)
        {
            _ = Task.Run(TakeAsync);
        }
    }

    internal async Task TakeAsync()
    {
        try
        {
            log.SnapshotRequested();
            var debug = folders.Debug();
            var name = Prefix + time.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            // Collected first and filtered afterwards: a renderer may answer before Request returns.
            var results = new ConcurrentBag<DebugSnapshotResult>();
            void OnCompleted(DebugSnapshotResult result) => results.Add(result);

            snapshots.Completed += OnCompleted;
            DebugSnapshotRequest request;
            try
            {
                request = snapshots.Request(Path.Combine(debug, name));
                await Task.Delay(s_answerWait, time).ConfigureAwait(false);

                // A sleeping renderer would otherwise take it whenever it wakes, into this old folder.
                snapshots.Withdraw(request);

                // Renderers that took it report once their PNGs are written; large monitors take a while.
                var waited = time.GetTimestamp();
                while (snapshots.Writing > 0 && time.GetElapsedTime(waited) < s_writeWait)
                {
                    await Task.Delay(s_writePoll, time).ConfigureAwait(false);
                }
            }
            finally
            {
                snapshots.Completed -= OnCompleted;
            }

            var saved = results.Count(r => r.Saved && r.Generation == request.Generation);
            if (saved > 0)
            {
                log.SnapshotSaved(saved, name);
                RemoveOld(debug);
            }
            else
            {
                log.SnapshotNothing();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Started from a hotkey with nobody waiting: the log is the only place this shows.
            log.SnapshotFailed(ex);
        }
        finally
        {
            Volatile.Write(ref _taking, 0);
        }
    }

    /// <summary>
    /// After a saved snapshot: keeps the newest <see cref="Kept"/> with images, the new one included, and
    /// removes everything else (also folders of failed writes), so a press that saves nothing loses
    /// nothing and the pictures of sign-in screens neither pile up nor fill the system drive.
    /// </summary>
    private static void RemoveOld(string debug)
    {
        if (!Directory.Exists(debug))
        {
            return;
        }

        List<string> old;
        try
        {
            var all = new DirectoryInfo(debug).EnumerateDirectories(Prefix + "*").OrderBy(d => d.CreationTimeUtc).ToList();
            var kept = all.Where(HasImages).TakeLast(Kept).Select(d => d.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            old = [.. all.Select(d => d.FullName).Where(path => !kept.Contains(path))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder list changed under us: tidied up with the next snapshot.
            return;
        }

        foreach (var folder in old)
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Still being written or open: removed with the next snapshot.
            }
        }
    }

    // A folder that vanished or cannot be read counts as one without images (removed if still there).
    private static bool HasImages(DirectoryInfo folder)
    {
        try
        {
            return folder.EnumerateFiles("*.png").Any();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
