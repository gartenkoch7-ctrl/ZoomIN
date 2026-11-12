using Microsoft.Win32;
using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.SecureDesktop;

/// <summary>Works on a private key under HKCU and never injects real keys.</summary>
public sealed class EaseOfAccessNotifierTests : IDisposable
{
    private const string Parent = @"Software\ZoomIN.Tests.EaseOfAccess";

    private readonly string _path = Parent + @"\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    private int _winUCount;

    public EaseOfAccessNotifierTests()
    {
        _root = Registry.CurrentUser.CreateSubKey(_path);
    }

    private EaseOfAccessNotifier Notifier(bool registered, bool secureOn = true, bool byEaseOfAccess = false) =>
        new(_root, () => registered && secureOn, () => registered, () => _winUCount++, NullLogger.Instance, byEaseOfAccess);

    [Fact]
    public void Started_WritesStartingAndSendsWinU()
    {
        Notifier(registered: true).NotifyStarted();

        Assert.Equal(EaseOfAccessNotifier.Starting, Signal());
        Assert.Equal(1, _winUCount);
    }

    [Fact]
    public void Exiting_WritesExitingAndSendsWinU()
    {
        var notifier = Notifier(registered: true);
        notifier.NotifyStarted();
        notifier.NotifyExiting();

        Assert.Equal(EaseOfAccessNotifier.Exiting, Signal());
        Assert.Equal(2, _winUCount);
    }

    [Fact]
    public void NotRegistered_DoesNothing()
    {
        // Win+U without a registered AT would only open the Settings app.
        var notifier = Notifier(registered: false);
        notifier.NotifyStarted();
        notifier.NotifyExiting();

        Assert.Null(Signal());
        Assert.Equal(0, _winUCount);
    }

    [Fact]
    public void StartedByEaseOfAccess_SignalsOnlyTheExit()
    {
        // Windows started ZoomIN from its list and already knows it runs (ADR-0030) …
        // (the sign-in screen switch may be off: the AT entry stays for the autostart) …
        var notifier = Notifier(registered: true, secureOn: false, byEaseOfAccess: true);
        notifier.NotifyStarted();

        Assert.Null(Signal());
        Assert.Equal(0, _winUCount);

        // … but an AT that ends itself must still tell it.
        notifier.NotifyExiting();

        Assert.Equal(EaseOfAccessNotifier.Exiting, Signal());
        Assert.Equal(1, _winUCount);
    }

    [Fact]
    public void SecureDesktopsOff_ManualStart_SendsNothing()
    {
        // Nothing to announce, so nothing to take back: no Win+U at start or exit.
        var notifier = Notifier(registered: true, secureOn: false);
        notifier.NotifyStarted();
        notifier.NotifyExiting();

        Assert.Null(Signal());
        Assert.Equal(0, _winUCount);
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
    }

    private object? Signal()
    {
        using var key = _root.OpenSubKey(EaseOfAccessNotifier.TempPath);
        return key?.GetValue(SecureDesktopRegistration.AtName);
    }
}
