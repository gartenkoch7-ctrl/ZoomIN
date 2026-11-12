using ZoomIN.Core.Islands;

namespace ZoomIN.App.Tests.Islands;

/// <summary>Records whether the edit mode takes the input; tests call the handler directly.</summary>
internal sealed class FakeIslandInput : IIslandEditInput
{
    public IIslandEditInputHandler? Handler { get; private set; }

    public int Starts { get; private set; }

    public int Stops { get; private set; }

    public bool Capturing => Handler is not null;

    public void Start(IIslandEditInputHandler handler)
    {
        Handler = handler;
        Starts++;
    }

    public void Stop()
    {
        Handler = null;
        Stops++;
    }
}
