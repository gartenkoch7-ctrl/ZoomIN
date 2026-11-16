using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Viewport;

namespace ZoomIN.App.Tests.SecureDesktop;

/// <summary>The Win32 answers of the secure checks, set by each test.</summary>
internal sealed class FakeProbe : ISecureDesktopProbe
{
    public string? Desktop { get; set; } = "Winlogon";

    public List<ProbedWindow> Overlays { get; } = [];

    /// <summary>Top window per point; points not listed have none.</summary>
    public Dictionary<PhysicalPoint, ProbedWindow> Top { get; } = [];

    public Dictionary<nint, uint> Affinities { get; } = [];

    public bool? InJob { get; set; } = true;

    public Dictionary<int, ProbedProcess> Processes { get; } = [];

    public bool Throw { get; set; }

    public string? DesktopName() => Throw ? throw new InvalidOperationException("probe broke") : Desktop;

    public IReadOnlyList<ProbedWindow> OwnOverlays() => Overlays;

    public ProbedWindow? TopWindowAt(PhysicalPoint point) => Top.TryGetValue(point, out var window) ? window : null;

    public uint? DisplayAffinity(nint window) => Affinities.TryGetValue(window, out var affinity) ? affinity : null;

    public bool? IsInJob() => InJob;

    public ProbedProcess Process(int processId) => Processes.TryGetValue(processId, out var process) ? process : new ProbedProcess(false, null);
}
