namespace ZoomIN.App.Lifecycle;

/// <summary>What settings pages may ask of the tray app's shell (implemented by <see cref="AppShell"/>).</summary>
public interface IShellActions
{
    /// <summary>Handle of the settings window (file pickers need an owner); 0 while it is closed.</summary>
    nint SettingsWindowHandle { get; }

    /// <summary>The first-run assistant is done: the settings window switches to the overview.</summary>
    void ShowOverview();

    /// <summary>Starts the freshly installed version and ends this one.</summary>
    void ExitForUpdate();
}
