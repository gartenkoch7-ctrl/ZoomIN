using ZoomIN.Core.Viewport;
using ZoomIN.Rendering.Gpu;

namespace ZoomIN.Rendering.Tests.Gpu;

/// <summary>Skipping unchanged constant uploads (M14 step 2): pure logic, no GPU.</summary>
public sealed class ConstantUploadTests
{
    private static readonly PhysicalRect s_monitor = new(0, 0, 64, 64);

    [Fact]
    public void FirstValue_IsAlwaysUploaded()
    {
        var upload = default(ConstantUpload<MagnifyConstants>);

        Assert.True(upload.Changed(default));
    }

    [Fact]
    public void SameValueAgain_IsSkipped()
    {
        var upload = default(ConstantUpload<MagnifyConstants>);
        var value = MagnifyConstants.FromSourceRect(new PhysicalRect(0, 0, 32, 32), s_monitor);

        upload.Changed(value);

        Assert.False(upload.Changed(value));
    }

    [Fact]
    public void ChangedValue_IsUploadedAndRemembered()
    {
        var upload = default(ConstantUpload<MagnifyConstants>);
        var a = MagnifyConstants.FromSourceRect(new PhysicalRect(0, 0, 32, 32), s_monitor);
        var b = MagnifyConstants.FromSourceRect(new PhysicalRect(1, 0, 32, 32), s_monitor);

        upload.Changed(a);

        Assert.True(upload.Changed(b));
        Assert.False(upload.Changed(b));
        Assert.True(upload.Changed(a));
    }
}
