using System.Runtime.InteropServices;

namespace ZoomIN.Rendering.Interop;

/// <summary>
/// Raw COM vtable calls for the per-frame capture path. Vortice's managed overloads create a
/// wrapper object per call, which would break "0 B per frame" (AGENTS.md 2.3, docs/kb/performance.md).
/// Slot numbers follow the interface declarations in dxgi1_2.h and d3d11.h.
/// </summary>
internal static unsafe class DxgiInterop
{
    public const int WaitTimeout = unchecked((int)0x887A0027); // DXGI_ERROR_WAIT_TIMEOUT
    public const int AccessLost = unchecked((int)0x887A0026); // DXGI_ERROR_ACCESS_LOST

    // IUnknown (0–2), IDXGIObject (3–6), IDXGIOutputDuplication: GetDesc 7, AcquireNextFrame 8 … ReleaseFrame 14.
    private const int AcquireNextFrameSlot = 8;
    private const int ReleaseFrameSlot = 14;

    // IUnknown (0–2), IDXGIObject (3–6), IDXGIOutput: GetDesc 7, GetDisplayModeList 8,
    // FindClosestMatchingMode 9, WaitForVBlank 10 (also in IDXGIOutput1 and later).
    private const int WaitForVBlankSlot = 10;

    // IUnknown (0–2), ID3D11DeviceChild (3–6), ID3D11DeviceContext methods from 7;
    // CopySubresourceRegion is 46, CopyResource 47.
    private const int CopySubresourceRegionSlot = 46;
    private const int CopyResourceSlot = 47;

    /// <summary>IID of ID3D11Resource.</summary>
    public static readonly Guid D3D11ResourceId = new("dc8e63f3-d12b-4952-b47b-5e45026a862d");

    public static int AcquireNextFrame(nint duplication, uint timeoutMilliseconds, out FrameInfo info, out nint resource)
    {
        info = default;
        resource = 0;
        fixed (FrameInfo* pInfo = &info)
        fixed (nint* pResource = &resource)
        {
            var fn = (delegate* unmanaged[Stdcall]<nint, uint, FrameInfo*, nint*, int>)VTable(duplication)[AcquireNextFrameSlot];
            return fn(duplication, timeoutMilliseconds, pInfo, pResource);
        }
    }

    public static int ReleaseFrame(nint duplication)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)VTable(duplication)[ReleaseFrameSlot];
        return fn(duplication);
    }

    /// <summary>IDXGIOutput::WaitForVBlank as HRESULT: Vortice throws on failure, which would allocate per idle pass.</summary>
    public static int WaitForVBlank(nint output)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, int>)VTable(output)[WaitForVBlankSlot];
        return fn(output);
    }

    public static void CopyResource(nint context, nint destination, nint source)
    {
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, nint, void>)VTable(context)[CopyResourceSlot];
        fn(context, destination, source);
    }

    /// <summary>Copies the source pixels [left, right) × [top, bottom) to (0, 0) of the destination (subresource 0 of both).</summary>
    public static void CopySubresourceRegion(nint context, nint destination, nint source, uint left, uint top, uint right, uint bottom)
    {
        var box = new Box { Left = left, Top = top, Front = 0, Right = right, Bottom = bottom, Back = 1 };
        var fn = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, uint, nint, uint, Box*, void>)VTable(context)[CopySubresourceRegionSlot];
        fn(context, destination, 0, 0, 0, 0, source, 0, &box);
    }

    private static void** VTable(nint comObject) => *(void***)comObject;

    /// <summary>Native layout of D3D11_BOX.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        public uint Left;
        public uint Top;
        public uint Front;
        public uint Right;
        public uint Bottom;
        public uint Back;
    }

    /// <summary>Native layout of DXGI_OUTDUPL_FRAME_INFO.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FrameInfo
    {
        public long LastPresentTime;
        public long LastMouseUpdateTime;
        public uint AccumulatedFrames;
        public int RectsCoalesced;
        public int ProtectedContentMaskedOut;
        public int PointerX;
        public int PointerY;
        public int PointerVisible;
        public uint TotalMetadataBufferSize;
        public uint PointerShapeBufferSize;
    }
}
