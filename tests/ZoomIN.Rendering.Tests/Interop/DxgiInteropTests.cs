using System.Diagnostics;
using Vortice.DXGI;
using ZoomIN.Rendering.Interop;

namespace ZoomIN.Rendering.Tests.Interop;

/// <summary>The raw vtable slot of IDXGIOutput::WaitForVBlank (idle clock, M14 step 3) on a real output.</summary>
public sealed class DxgiInteropTests
{
    [Fact]
    public void WaitForVBlank_WaitsForTheNextRefresh()
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        Assert.SkipWhen(factory.EnumAdapters1(0, out var adapter).Failure, "No graphics adapter.");
        using var _ = adapter;
        Assert.SkipWhen(adapter.EnumOutputs(0, out var output).Failure, "No monitor on the first adapter.");
        using var __ = output;

        // A wrong slot would call another method (or crash); a right one blocks up to one refresh.
        var start = Stopwatch.GetTimestamp();
        var waits = 0;
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(0, DxgiInterop.WaitForVBlank(output.NativePointer));
            waits++;
        }

        var milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Assert.Equal(5, waits);
        Assert.InRange(milliseconds, 5 * 2.0, 5 * 60.0);
    }
}
