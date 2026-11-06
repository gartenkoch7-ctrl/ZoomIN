using System.Globalization;
using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;
using ZoomIN.Rendering.RenderLoop;
using ZoomIN.Rendering.Snapshots;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>
/// A video in a page, offline: inside ZOOMIN_OFFLINE_VIDEO = "x,y,width,height" of the captured
/// image (ZOOMIN_OFFLINE_INPUT) the picture pans 2 px per 60 Hz frame and gets new noise each
/// frame (like video compression); the page around it stands still. Reports per frame how many
/// video pixels are drawn inverted (should stay original) and how many flip between frames.
/// </summary>
public sealed class OfflineVideoTests
{
    private const int Frames = 60;
    private const double FrameSeconds = 1.0 / 60;

    // ZOOMIN_OFFLINE_PAN px per frame (default 2); 0 = still picture with fresh noise only.
    private static int Pan => int.TryParse(Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_PAN"), out var pan) ? pan : 2;

    [Fact]
    public void Video()
    {
        var input = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_INPUT");
        var area = Environment.GetEnvironmentVariable("ZOOMIN_OFFLINE_VIDEO");
        Assert.SkipWhen(string.IsNullOrEmpty(input) || string.IsNullOrEmpty(area), "Set ZOOMIN_OFFLINE_INPUT and ZOOMIN_OFFLINE_VIDEO.");
        var parts = area!.Split(',').Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
        var (vx, vy, vw, vh) = (parts[0], parts[1], parts[2], parts[3]);

        var page = OfflineImage.Load(input!);
        using var gpu = GpuDevice.CreateWarp();
        using var renderer = new OfflineRenderer(gpu, page.Width, page.Height);
        var monitor = new PhysicalRect(0, 0, page.Width, page.Height);
        var alpha = SmartInvertLayer.AlphaAfter(FrameSeconds, running: true);
        var report = new List<string> { $"video {vw} x {vh} at {vx},{vy}; {Frames} frames" };
        bool[]? lastInverted = null;
        double worstInverted = 0, worstFlips = 0;
        for (var frame = 0; frame < Frames; frame++)
        {
            var bgra = page.Pixels(frame, vx, vy, vw, vh, Pan);
            using var texture = new OfflineImage(page.Width, page.Height, bgra).Upload(gpu.Device);
            using var view = gpu.Device.CreateShaderResourceView(texture);
            renderer.Draw(view, monitor, monitor, protectPhotos: true, frame == 0 ? 1 : alpha);
            var result = renderer.ReadResult();

            var inverted = new bool[vw * vh];
            var invertedCount = 0;
            for (var y = 0; y < vh; y++)
            {
                for (var x = 0; x < vw; x++)
                {
                    var i = (((vy + y) * page.Width) + vx + x) * 4;
                    var source = (0.2126 * bgra[i + 2]) + (0.7152 * bgra[i + 1]) + (0.0722 * bgra[i]);
                    var drawn = (0.2126 * result.Rgba[i]) + (0.7152 * result.Rgba[i + 1]) + (0.0722 * result.Rgba[i + 2]);
                    inverted[(y * vw) + x] = Math.Abs(source - drawn) > 64;
                    invertedCount += inverted[(y * vw) + x] ? 1 : 0;
                }
            }

            var flips = lastInverted is null ? 0 : inverted.Where((v, k) => v != lastInverted[k]).Count();
            lastInverted = inverted;
            var share = invertedCount / (double)(vw * vh);
            var flipShare = flips / (double)(vw * vh);
            worstInverted = Math.Max(worstInverted, share);
            worstFlips = Math.Max(worstFlips, flipShare);
            report.Add($"frame {frame,2}: inverted {share * 100:0.00} %, flipped {flipShare * 100:0.00} %");
        }

        report.Insert(1, $"worst: inverted {worstInverted * 100:0.00} %, flipped {worstFlips * 100:0.00} %");
        var stem = Path.Combine(Path.GetDirectoryName(input!)!, Path.GetFileNameWithoutExtension(input!));
        File.WriteAllLines(stem + $"-video-pan{Pan}.txt", report);
        PngWriter.Write(stem + "-video-last.png", renderer.ReadResult());
    }
}
