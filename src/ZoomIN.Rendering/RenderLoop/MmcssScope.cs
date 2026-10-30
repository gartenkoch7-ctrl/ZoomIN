using Windows.Win32;

namespace ZoomIN.Rendering.RenderLoop;

/// <summary>
/// Registers the current thread with the Multimedia Class Scheduler ("Games" task), which gives
/// it a scheduling boost so frames are not delayed by background work.
/// </summary>
internal sealed class MmcssScope : IDisposable
{
    private readonly IDisposable? _registration;

    private MmcssScope(IDisposable? registration)
    {
        _registration = registration;
    }

    public static MmcssScope Enter()
    {
        uint taskIndex = 0;
        var handle = PInvoke.AvSetMmThreadCharacteristics("Games", ref taskIndex);

        // Failure is not fatal: rendering still works, just without the boost.
        return new MmcssScope(handle.IsInvalid ? null : handle);
    }

    public void Dispose() => _registration?.Dispose();
}
