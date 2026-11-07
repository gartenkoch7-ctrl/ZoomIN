using System.Runtime.InteropServices;
using ZoomIN.Core.Shell;

namespace ZoomIN.Platform.Shell;

/// <summary>Creates and removes .lnk shortcuts (Start menu, desktop) through the Windows Script Host.</summary>
public sealed class WindowsShortcuts : IShortcuts
{
    public const string FileName = "ZoomIN.lnk";

    public string CommonStartMenu => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), FileName);

    public string UserDesktop => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), FileName);

    public void Create(string shortcutPath, string targetPath)
    {
        // WScript.Shell is part of every Windows; IShellLink through COM interop would need far
        // more code for the same .lnk file.
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new PlatformNotSupportedException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            var link = shell.CreateShortcut(shortcutPath);
            link.TargetPath = targetPath;
            link.WorkingDirectory = Path.GetDirectoryName(targetPath);
            link.IconLocation = targetPath + ",0";
            link.Save();
            Marshal.FinalReleaseComObject(link);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    public void Remove(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }
}
