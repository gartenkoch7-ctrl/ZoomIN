using System.Globalization;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// Scrolling with the time smoothing, offline: the content of a captured image moves up
/// ZOOMIN_OFFLINE_SCROLL px per 60 Hz frame. Each frame is compared with the settled render of the
/// same content (fresh weights, no memory); what differs is lag or flicker. Writes a report and the
/// worst frame next to the input (ZOOMIN_OFFLINE_INPUT).
/// </summary>
public sealed class OfflineScrollTests
{
    private const int Frames = 40;
    private const double FrameSeconds = 1.0 / 60;

    [Fact]
    public void Scroll()
    {
        var input = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_INPUT");
        var stepText = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_SCROLL");
        Assert.SkipWhen(string.IsNullOrEmpty(input) || string.IsNullOrEmpty(stepText), "Set ZOOMIN_OFFLINE_INPUT and ZOOMIN_OFFLINE_SCROLL (px per frame).");
        var step = int.Parse(stepText!, CultureInfo.InvariantCulture);

        var image = OfflineImage.Load(input!);
        var height = image.Height - (Frames * step);
        using var gpu = GpuDevice.CreateWarp();
        using var smoothed = new OfflineRenderer(gpu, image.Width, height);
        using var settled = new OfflineRenderer(gpu, image.Width, height);
        var monitor = new PhysicalRect(0, 0, image.Width, height);
        var alpha = SmartInvertLayer.AlphaAfter(FrameSeconds, running: true);

        var report = new List<string> { $"step {step} px/frame, {Frames} frames, alpha {alpha:0.000}" };
        var worst = -1.0;
        SnapshotImage? worstFrame = null;
        for (var frame = 0; frame < Frames; frame++)
        {
            using var texture = image.Rows(frame * step, height).Upload(gpu.Device);
            using var view = gpu.Device.CreateShaderResourceView(texture);
            smoothed.Draw(view, monitor, monitor, protectPhotos: true, frame == 0 ? 1 : alpha);
            settled.Draw(view, monitor, monitor, protectPhotos: true, alpha: 1);

            var moving = smoothed.ReadResult();
            var share = DifferingShare(moving, settled.ReadResult());
            report.Add($"frame {frame,2}: {share * 100:0.00} % of pixels differ by > 25 % luma");
            if (share > worst)
            {
                worst = share;
                worstFrame = moving;
            }
        }

        var stem = Path.Combine(Path.GetDirectoryName(input!)!, Path.GetFileNameWithoutExtension(input!));
        File.WriteAllLines(stem + $"-scroll{step}.txt", report);
        PngFile.Write(stem + $"-scroll{step}-worst.png", worstFrame!);
    }

    private static double DifferingShare(SnapshotImage a, SnapshotImage b)
    {
        var differing = 0;
        for (var i = 0; i < a.Rgba.Length; i += 4)
        {
            var la = (0.2126 * a.Rgba[i]) + (0.7152 * a.Rgba[i + 1]) + (0.0722 * a.Rgba[i + 2]);
            var lb = (0.2126 * b.Rgba[i]) + (0.7152 * b.Rgba[i + 1]) + (0.0722 * b.Rgba[i + 2]);
            differing += Math.Abs(la - lb) > 64 ? 1 : 0;
        }

        return differing / (double)(a.Rgba.Length / 4);
    }
}
