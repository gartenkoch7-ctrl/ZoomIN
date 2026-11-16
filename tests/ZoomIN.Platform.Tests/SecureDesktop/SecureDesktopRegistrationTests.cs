using Microsoft.Win32;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Tests.SecureDesktop;

/// <summary>Works on a private key under HKCU instead of HKLM, so it needs no administrator rights.</summary>
public sealed class SecureDesktopRegistrationTests : IDisposable
{
    private const string Accessibility = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Accessibility";

    // Own parent key: RegistryAutostartTests deletes "Software\ZoomIN.Tests" while tests run in parallel.
    private const string Parent = @"Software\ZoomIN.Tests.SecureDesktop";

    private readonly string _path = Parent + @"\" + Guid.NewGuid().ToString("N");
    private readonly RegistryKey _root;
    private readonly InstallLayout _layout = new(@"C:\Program Files\ZoomIN");

    public SecureDesktopRegistrationTests()
    {
        _root = Registry.CurrentUser.CreateSubKey(_path);
    }

    private SecureDesktopRegistration Registration => new(_root);

    [Fact]
    public void Default_IsOn_AndPointsAtTheInstalledCopy()
    {
        Assert.True(Registration.Apply(_layout, choice: null));

        Assert.True(Registration.IsRegistered);
        using var at = _root.OpenSubKey(Accessibility + @"\ATs\ZoomIN")!;
        Assert.Equal(@"C:\Program Files\ZoomIN\current\ZoomIN.exe", at.GetValue("StartExe"));
        Assert.Equal("ZoomIN.exe", at.GetValue("ATExe"));
        Assert.Null(at.GetValue("StartParams"));
        Assert.Equal(1, at.GetValue("TerminateOnDesktopSwitch"));
        Assert.Null(Registration.Preference);
    }

    [Fact]
    public void OtherAssistiveTechnologies_AreKept()
    {
        using (var access = _root.CreateSubKey(Accessibility))
        {
            access.SetValue("Configuration", "magnifierpane");
        }

        Registration.Apply(_layout, choice: null);
        Assert.Equal("magnifierpane,ZoomIN", Configuration());

        Registration.Apply(_layout, choice: false);
        Assert.Equal("magnifierpane", Configuration());
    }

    [Fact]
    public void Off_IsKept_ByLaterUpdates()
    {
        Registration.Apply(_layout, choice: false);

        Assert.False(Registration.Apply(_layout, choice: null));
        Assert.False(Registration.IsRegistered);
        Assert.False(Registration.Preference);
    }

    [Fact]
    public void Off_KeepsTheAtEntry_ButStartsNothingOnSecureDesktops()
    {
        // The autostart list needs ATs\ZoomIN; "none" keeps Windows from starting it on lock and UAC (ADR-0030).
        Registration.Apply(_layout, choice: false);

        Assert.True(Registration.IsAtRegistered);
        Assert.False(Registration.IsRegistered);
        Assert.Equal(SecureDesktopRegistration.NoAccommodation, AtValue(SecureDesktopRegistration.AccommodationValue));
        Assert.Equal(string.Empty, Configuration() ?? string.Empty);
    }

    [Fact]
    public void OnAgain_ClearsTheAccommodation()
    {
        Registration.Apply(_layout, choice: false);

        Assert.True(Registration.Apply(_layout, choice: true));

        Assert.True(Registration.IsRegistered);
        Assert.Null(AtValue(SecureDesktopRegistration.AccommodationValue));
        Assert.Equal("ZoomIN", Configuration());
    }

    [Fact]
    public void AccommodationNone_CountsAsOff_EvenInTheList()
    {
        Registration.Apply(_layout, choice: true);
        using (var at = _root.CreateSubKey(Accessibility + @"\ATs\ZoomIN"))
        {
            at.SetValue(SecureDesktopRegistration.AccommodationValue, "NONE");
        }

        Assert.False(Registration.IsRegistered);
    }

    [Fact]
    public void RemoveAll_ForgetsTheChoice()
    {
        Registration.Apply(_layout, choice: false);

        Registration.RemoveAll();

        Assert.Null(Registration.Preference);
        Assert.False(Registration.IsRegistered);
        Assert.False(Registration.IsAtRegistered);
    }

    [Fact]
    public void RemoveAll_TakesZoomINOutOfTheList()
    {
        Registration.Apply(_layout, choice: true);

        Registration.RemoveAll();

        Assert.Equal(string.Empty, Configuration());
    }

    public void Dispose()
    {
        _root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(Parent);
        if (parent?.SubKeyCount == 0)
        {
            Registry.CurrentUser.DeleteSubKey(Parent, throwOnMissingSubKey: false);
        }
    }

    private object? AtValue(string name)
    {
        using var at = _root.OpenSubKey(Accessibility + @"\ATs\ZoomIN");
        return at?.GetValue(name);
    }

    private string? Configuration()
    {
        using var access = _root.OpenSubKey(Accessibility);
        return access?.GetValue("Configuration") as string;
    }
}
