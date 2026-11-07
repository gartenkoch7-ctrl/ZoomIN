using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Monitors;
using ZoomIN.Core.Sharing;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Sharing;

/// <summary>
/// Looks for the Teams share frame about twice a second (ADR-0018). A shared window moves, so
/// once found, its frame and a window marked by hotkey are re-read 30 times a second: one cheap
/// call per frame, and the filter keeps up with dragging.
/// </summary>
public sealed unsafe class TeamsShareSource : IShareSource
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(33);
    private const int TicksPerDetection = 15; // ≈ 2× per second
    private const int TicksPerProcessScan = 150; // ≈ every 5 s; enumerating processes is the costly part

    // Desktop and taskbar are never "a window under the mouse" for the hotkey.
    private static readonly string[] s_shellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private readonly IMonitorProvider _monitors;
    private readonly ILog _log;
    private readonly ManualResetEventSlim _stop = new();
    private Thread? _thread;

    public TeamsShareSource(IMonitorProvider monitors, ILog log)
    {
        _monitors = monitors;
        _log = log;
    }

    public void Start(ShareModel model)
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(() => Run(model)) { IsBackground = true, Name = "ZoomIN Share Detection" };
        _thread.Start();
    }

    public bool TryGetWindowAt(PhysicalPoint point, out long windowId, out PhysicalRect bounds)
    {
        windowId = 0;
        bounds = default;

        // The overlay is click-through, so WindowFromPoint already looks past it.
        var hit = PInvoke.WindowFromPoint(new Point((int)point.X, (int)point.Y));
        var root = hit.IsNull ? HWND.Null : PInvoke.GetAncestor(hit, GET_ANCESTOR_FLAGS.GA_ROOT);
        if (root.IsNull || IsShell(root) || MeetingWindowReader.BoundsOf(root) is not { } found)
        {
            return false;
        }

        windowId = (nint)root.Value;
        bounds = found;
        return true;
    }

    public void Dispose()
    {
        _stop.Set();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _stop.Dispose();
    }

    private void Run(ShareModel model)
    {
        HashSet<uint> processIds = [];
        long frame = 0;
        SharedArea? detected = null;
        PhysicalRect? manualBounds = null;
        var tick = 0;

        while (!_stop.Wait(Interval))
        {
            try
            {
                if (tick % TicksPerProcessScan == 0)
                {
                    processIds = MeetingWindowReader.MeetingProcessIds();
                }

                if (tick % TicksPerDetection == 0)
                {
                    (frame, detected) = Detect(model, processIds, detected);
                }
                else if (frame != 0 && detected?.Kind == ShareKind.Window)
                {
                    detected = Follow(model, frame, detected);
                }

                manualBounds = FollowManual(model, manualBounds);
                tick++;
            }
            catch (Exception ex)
            {
                // A window can vanish between enumerating and reading it; never let that end detection.
                _log.Error("Share detection failed", ex);
            }
        }
    }

    private (long Frame, SharedArea? Detected) Detect(ShareModel model, HashSet<uint> processIds, SharedArea? last)
    {
        var options = model.Options;
        var frame = options.Enabled && options.AutoDetect && processIds.Count > 0
            ? ShareDetector.FindFrame(MeetingWindowReader.WindowsOf(processIds))
            : null;
        var detected = frame is { } f ? ShareDetector.Classify(f.Bounds, MonitorBounds()) : (SharedArea?)null;
        if (detected != last)
        {
            _log.Info(detected is { } d ? $"Share detected: {d.Kind} {d.Bounds}" : "Share ended");
            model.PublishDetected(detected);
        }

        return (frame?.Id ?? 0, detected);
    }

    private static SharedArea? Follow(ShareModel model, long frame, SharedArea? last)
    {
        var bounds = MeetingWindowReader.BoundsOf((HWND)(nint)frame);
        var next = bounds is { } b ? new SharedArea(ShareKind.Window, b) : (SharedArea?)null;
        if (next != last)
        {
            model.PublishDetected(next);
        }

        return next;
    }

    private static PhysicalRect? FollowManual(ShareModel model, PhysicalRect? last)
    {
        var window = model.Current.ManualWindow;
        if (window == 0)
        {
            return null;
        }

        var bounds = MeetingWindowReader.BoundsOf((HWND)(nint)window);
        if (bounds != last)
        {
            model.PublishManualWindow(window, bounds);
        }

        return bounds;
    }

    private List<PhysicalRect> MonitorBounds()
    {
        var bounds = new List<PhysicalRect>();
        foreach (var id in _monitors.MonitorIds())
        {
            if (_monitors.BoundsOf(id) is { } b)
            {
                bounds.Add(b);
            }
        }

        return bounds;
    }

    private static bool IsShell(HWND window)
    {
        Span<char> name = stackalloc char[64];
        int length;
        fixed (char* p = name)
        {
            length = PInvoke.GetClassName(window, p, name.Length);
        }

        var className = name[..Math.Max(0, length)];
        foreach (var shell in s_shellClasses)
        {
            if (className.SequenceEqual(shell))
            {
                return true;
            }
        }

        return false;
    }
}
