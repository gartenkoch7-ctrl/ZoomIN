using ZoomIN.App.Tests.Overview;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Pointer;
using ZoomIN.Core.Settings;

namespace ZoomIN.App.Tests.Pointer;

/// <summary>The card "Bewegung begrenzen" (A6) writes the global settings field by field.</summary>
public sealed class PointerConstraintViewModelTests : IDisposable
{
    private readonly OverviewRig _rig = new(new AppSettings
    {
        PointerConstraint = new PointerConstraintOptions { Buffer = EdgeBufferLevel.Strong, AxisLockKey = AxisLockKey.RightShift },
    });

    public void Dispose() => _rig.Dispose();

    [Fact]
    public void Shows_TheStoredValues()
    {
        var vm = new PointerConstraintViewModel(_rig.Settings);

        Assert.Equal((int)EdgeBufferLevel.Strong, vm.BufferIndex);
        Assert.False(vm.AxisLock);
        Assert.Equal((int)AxisLockKey.RightShift, vm.KeyIndex);
    }

    [Fact]
    public void Changes_AreSavedFieldByField()
    {
        var vm = new PointerConstraintViewModel(_rig.Settings)
        {
            AxisLock = true,
            KeyIndex = (int)AxisLockKey.LeftAlt,
            BufferIndex = (int)EdgeBufferLevel.Light,
        };

        var stored = _rig.Settings.Current.PointerConstraint;
        Assert.Equal(new PointerConstraintOptions { Buffer = EdgeBufferLevel.Light, AxisLock = true, AxisLockKey = AxisLockKey.LeftAlt }, stored);
        Assert.True(vm.AxisLock);
    }

    [Fact]
    public void ComboBoxReset_ShowsTheStoredValueAgain()
    {
        var vm = new PointerConstraintViewModel(_rig.Settings) { BufferIndex = -1, KeyIndex = -1 };

        Assert.Equal((int)EdgeBufferLevel.Strong, vm.BufferIndex);
        Assert.Equal((int)AxisLockKey.RightShift, vm.KeyIndex);
        Assert.Equal(EdgeBufferLevel.Strong, _rig.Settings.Current.PointerConstraint.Buffer);
    }
}
