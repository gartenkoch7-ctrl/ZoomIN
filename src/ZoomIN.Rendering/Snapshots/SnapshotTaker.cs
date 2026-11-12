using Microsoft.Extensions.Logging;
using Vortice.Direct3D11;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Modes;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.RenderLoop;

namespace ZoomIN.Rendering.Snapshots;

/// <summary>
/// Takes a debug snapshot of one monitor when the diagnostics page asks for one (M13): the
/// original desktop, what ZoomIN drew and the smart inversion weights, plus a text with the
/// request. <see cref="TakeIfRequested"/> runs every frame; without a new request it only
/// compares a number. With one it allocates and waits for the GPU – once, on purpose.
/// </summary>
internal sealed class SnapshotTaker(DebugSnapshots snapshots, string deviceName, ILogger log)
{
    private long _taken;

    /// <summary>A snapshot is waiting: the next frame must be drawn even if nothing changed (M14 step 3).</summary>
    public bool IsRequested => snapshots.Pending is { } pending && pending.Generation != _taken;

    /// <param name="desktop">The captured desktop; null if nothing was captured this frame.</param>
    /// <param name="result">What ZoomIN drew, before Present (the back buffer is still intact).</param>
    /// <param name="weights">Smart inversion weights of this frame, or null.</param>
    public void TakeIfRequested(
        ID3D11Device device, ID3D11DeviceContext context, ID3D11ShaderResourceView? desktop, ID3D11RenderTargetView result,
        ID3D11ShaderResourceView? weights, PhysicalRect bounds, in ModeFrame frame, in FrameRequest request)
    {
        var pending = snapshots.Pending;
        if (pending is null || pending.Generation == _taken)
        {
            return;
        }

        // Before anything can fail: one try per request, never one per frame.
        _taken = pending.Generation;
        if (desktop is null)
        {
            snapshots.Report(new DebugSnapshotResult(pending.Generation, deviceName, false, "nothing_drawn"));
            return;
        }

        Take(pending, device, context, desktop, result, weights, bounds, frame, request);
    }

    // Separate method: the closure for Task.Run would otherwise be allocated on every frame,
    // because C# creates it where the captured variable is declared (checked by a 0 B test).
    private void Take(
        DebugSnapshotRequest pending, ID3D11Device device, ID3D11DeviceContext context, ID3D11ShaderResourceView desktop,
        ID3D11RenderTargetView result, ID3D11ShaderResourceView? weights, PhysicalRect bounds, in ModeFrame frame, in FrameRequest request)
    {
        // Counted until the files are complete, so the KI-Debug reader deletes the folder only after.
        snapshots.WriteStarted();
        try
        {
            using var desktopTexture = desktop.Resource;
            using var resultTexture = result.Resource;
            var original = TextureReader.ReadBgra(device, context, desktopTexture);
            var drawn = TextureReader.ReadBgra(device, context, resultTexture);
            SnapshotImage? weightImage = null;
            if (weights is not null)
            {
                using var weightTexture = weights.Resource;
                weightImage = TextureReader.ReadWeights(device, context, weightTexture);
            }

            var info = SnapshotInfo.Describe(deviceName, bounds, frame, request, weights is not null);
            _ = Task.Run(() => Write(pending, original, drawn, weightImage, info));
        }
        catch (Exception ex) when (ex is InvalidOperationException or SharpGen.Runtime.SharpGenException)
        {
            log.SnapshotFailed(ex, deviceName);
            snapshots.Report(new DebugSnapshotResult(pending.Generation, deviceName, false, ex.GetType().Name));
            snapshots.WriteEnded();
        }
    }

    private void Write(DebugSnapshotRequest request, SnapshotImage original, SnapshotImage drawn, SnapshotImage? weights, string info)
    {
        using var activity = Telemetry.Source.StartActivity("zoomin.debug.snapshot");
        activity?.SetTag("zoomin.monitor", deviceName);
        try
        {
            Directory.CreateDirectory(request.Folder);
            WritePng(SnapshotFiles.Image(request.Folder, deviceName, "original"), original);
            WritePng(SnapshotFiles.Image(request.Folder, deviceName, "result"), drawn);
            if (weights is not null)
            {
                WritePng(SnapshotFiles.Image(request.Folder, deviceName, "weights"), weights);
            }

            File.WriteAllText(SnapshotFiles.Info(request.Folder, deviceName), info);
            var folderName = Path.GetFileName(request.Folder);
            log.SnapshotSaved(deviceName, folderName);
            snapshots.Report(new DebugSnapshotResult(request.Generation, deviceName, true, "saved"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.SnapshotFailed(ex, deviceName);
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.GetType().Name);
            snapshots.Report(new DebugSnapshotResult(request.Generation, deviceName, false, ex.GetType().Name));
        }
        finally
        {
            snapshots.WriteEnded();
        }
    }

    private static void WritePng(string path, SnapshotImage image)
    {
        using var file = File.Create(path);
        PngWriter.Write(file, image);
    }
}
