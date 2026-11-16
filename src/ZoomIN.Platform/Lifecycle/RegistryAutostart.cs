using Microsoft.Win32;
using ZoomIN.Core.Lifecycle;
using ZoomIN.Core.Updates;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Autostart via the per-user Run key (no admin rights). The watchdog is started by ZoomIN
/// itself, so only the main exe is registered. Since ADR-0030 only for the portable ZIP and
/// developer builds; an installed ZoomIN uses <see cref="EaseOfAccessAutostart"/>.
/// </summary>
public sealed class RegistryAutostart : IAutostart
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZoomIN";

    private readonly string _keyPath;
    private readonly string _command;

    public RegistryAutostart()
        : this(RunKey, $"\"{StableExecutablePath(Environment.ProcessPath)}\"")
    {
    }

    /// <summary>Custom key for tests, so they never touch the real Run key.</summary>
    public RegistryAutostart(string keyPath, string command)
    {
        _keyPath = keyPath;
        _command = command;
    }

    // An installed ZoomIN runs from a version folder that a later update removes (ADR-0015);
    // autostart must use the "current" link, which always points at the installed version.
    internal static string? StableExecutablePath(string? executable) =>
        InstallLayout.TryFromExecutable(executable, out var layout)
            ? layout.CurrentExecutable
            : executable;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return key?.GetValue(ValueName) is string value && value == _command;
        }
    }

    /// <summary>A "ZoomIN" Run value exists, whatever it points at (older versions, other folders).</summary>
    public bool Exists
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
        if (enabled)
        {
            key.SetValue(ValueName, _command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
