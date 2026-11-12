using ZoomIN.App.SecureDesktop;
using ZoomIN.App.Tests.Freeze;
using ZoomIN.App.ViewModels;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.App.Tests.Settings;

/// <summary>The autostart switch on "Allgemein" shows the system's state, not a flag of its own (ADR-0030).</summary>
public sealed class GeneralViewModelTests : IDisposable
{
    private readonly FreezeRig _rig = new();
    private readonly FakeAutostart _autostart = new();

    [Fact]
    public void OpeningThePage_ReadsTheAutostartAnew()
    {
        _autostart.Enabled = true;
        Assert.True(Page().Autostart);

        // Windows' Settings › Accessibility took ZoomIN out of the list meanwhile.
        _autostart.Enabled = false;
        Assert.False(Page().Autostart);
        Assert.Empty(_autostart.Calls);
    }

    [Fact]
    public void Switching_SetsTheAutostart()
    {
        var page = Page();

        page.Autostart = true;
        page.Autostart = false;

        Assert.Equal([true, false], _autostart.Calls);
    }

    public void Dispose() => _rig.Dispose();

    // The settings window builds a new view model each time the page is opened (PageServices.Create).
    private GeneralViewModel Page() => new(_rig.Settings, _autostart, new FakeSecureDesktop());

    private sealed class FakeAutostart : IAutostart
    {
        public bool Enabled { get; set; }

        public List<bool> Calls { get; } = [];

        public bool IsEnabled => Enabled;

        public void SetEnabled(bool enabled)
        {
            Calls.Add(enabled);
            Enabled = enabled;
        }
    }

    private sealed class FakeSecureDesktop : ISecureDesktopSwitch
    {
        public bool IsAvailable => false;

        public bool IsEnabled => false;

        public bool RequestChange(bool on) => false;

        public int ApplyElevated(bool on) => 1;
    }
}
