using ZoomIN.Core.Islands;
using ZoomIN.Core.Viewport;

namespace ZoomIN.Platform.Tests.Input;

/// <summary>Records what the island edit mode's worker hands on; a press can wait for a gate (to queue more behind it).</summary>
internal sealed class RecordingIslandHandler : IIslandEditInputHandler
{
    private readonly List<string> _calls = [];

    public bool FailOnPress { get; init; }

    /// <summary>The press waits for it (to queue more input behind it).</summary>
    public ManualResetEventSlim? Gate { get; init; }

    public bool Waiting { get; private set; }

    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public void WaitFor(int count) =>
        Assert.True(SpinWait.SpinUntil(() => Calls.Count >= count, TimeSpan.FromSeconds(5)), "The worker handed on too little.");

    public void Pressed(PhysicalPoint point)
    {
        if (FailOnPress)
        {
            throw new InvalidOperationException("test");
        }

        if (Gate is not null)
        {
            Waiting = true;
            Gate.Wait(TimeSpan.FromSeconds(5));
        }

        Add($"press {point.X},{point.Y}");
    }

    public void Moved(PhysicalPoint point) => Add($"move {point.X},{point.Y}");

    public void Released(PhysicalPoint point) => Add($"release {point.X},{point.Y}");

    public void Wheel(double notches, PhysicalPoint point) => Add($"wheel {notches} {point.X},{point.Y}");

    public void Key(IslandEditKey key) => Add($"key {key}");

    private void Add(string call)
    {
        lock (_calls)
        {
            _calls.Add(call);
        }
    }
}
