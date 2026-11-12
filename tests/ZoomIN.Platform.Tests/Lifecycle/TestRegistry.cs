using Microsoft.Win32;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>
/// A private key under HKCU per test, standing in for HKCU and HKLM, so autostart tests need no
/// admin rights and never touch the real Run key or Ease of Access lists. Each test class has its
/// own parent key: other classes delete theirs while tests run in parallel.
/// </summary>
internal sealed class TestRegistry : IDisposable
{
    private readonly string _parent;

    public TestRegistry(string name)
    {
        _parent = @"Software\ZoomIN.Tests." + name;
        RootPath = _parent + @"\" + Guid.NewGuid().ToString("N");
        Root = Registry.CurrentUser.CreateSubKey(RootPath);
    }

    /// <summary>Below HKCU, for <c>RegistryAutostart</c>, which takes a path instead of a key.</summary>
    public string RootPath { get; }

    public RegistryKey Root { get; }

    public void Dispose()
    {
        Root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(RootPath, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(_parent);
        if (parent?.SubKeyCount == 0)
        {
            Registry.CurrentUser.DeleteSubKey(_parent, throwOnMissingSubKey: false);
        }
    }
}
