using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Setup.Install;

namespace ZoomIN.Setup;

/// <summary>
/// Entry point. "ZoomIN-Setup.exe --apply-update …" is the elevated install step without UI
/// (ADR-0015); otherwise the wizard starts (same steps as the XAML-generated Main).
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (UpdateCommand.TryParse(args, out var command))
        {
            return (int)ElevatedInstall.Run(command, new FileLog());
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        return 0;
    }
}
