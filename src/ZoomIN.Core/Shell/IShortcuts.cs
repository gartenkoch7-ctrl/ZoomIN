namespace ZoomIN.Core.Shell;

/// <summary>ZoomIN's .lnk shortcuts in the Start menu (all users) and on the user's desktop.</summary>
public interface IShortcuts
{
    /// <summary>Path of the Start menu shortcut for all users.</summary>
    string CommonStartMenu { get; }

    /// <summary>Path of the shortcut on the current user's desktop.</summary>
    string UserDesktop { get; }

    void Create(string shortcutPath, string targetPath);

    /// <summary>Removes the shortcut; nothing happens if there is none.</summary>
    void Remove(string shortcutPath);
}
