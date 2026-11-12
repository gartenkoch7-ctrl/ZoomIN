namespace ZoomIN.Platform.PointerFence;

/// <summary>Two retired watchers disposed together after the fence left its lock.</summary>
internal sealed class RetiredPair(IDisposable first, IDisposable second) : IDisposable
{
    public void Dispose()
    {
        first.Dispose();
        second.Dispose();
    }
}
