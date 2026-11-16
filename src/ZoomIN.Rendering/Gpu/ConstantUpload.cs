using System.Runtime.InteropServices;
using Vortice.Direct3D11;

namespace ZoomIN.Rendering.Gpu;

/// <summary>
/// Remembers the last value written to one constant buffer and skips the upload when it is the
/// same again (M14 step 2). A plain byte compare, so it does not allocate and needs no Equals.
/// Only correct while nothing else writes to that buffer.
/// </summary>
internal struct ConstantUpload<T>
    where T : unmanaged
{
    private T _last;
    private bool _uploaded;

    /// <summary>True if <paramref name="value"/> differs from the last one; it is then remembered.</summary>
    public bool Changed(in T value)
    {
        if (_uploaded && MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in _last)).SequenceEqual(
            MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in value))))
        {
            return false;
        }

        _last = value;
        _uploaded = true;
        return true;
    }

    /// <summary>Uploads <paramref name="value"/> into <paramref name="buffer"/> unless it is already there.</summary>
    public void Update(ID3D11DeviceContext context, ID3D11Buffer buffer, in T value)
    {
        if (Changed(value))
        {
            context.UpdateSubresource(value, buffer);
        }
    }
}
