using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ZoomIN.Core.Diagnostics;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Folders;
using ZoomIN.Setup.Install;

namespace ZoomIN.Setup;

/// <summary>
/// Entry point. "ZoomIN-Setup.exe --apply-update …" is the elevated install step without UI
/// (ADR-0015); otherwise the wizard starts (same steps as the XAML-generated Main).
/// Telemetry starts like in ZoomIN.exe (ADR-0019); "--traceparent" is taken out first.
/// </summary>
public static class Program
{
    /// <summary>Services of the wizard (composition root); set before the WinUI app starts.</summary>
    internal static SetupServices? Services { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        args = TraceParentArgument.Extract(args, out var traceParent);
        using var telemetry = TelemetryHost.Start(TelemetryRole.Setup, new WindowsAppFolders(), traceParent);
        Core.Diagnostics.Telemetry.OnFlush(telemetry.Flush);
        using var services = new SetupServices(telemetry);
        if (UpdateCommand.TryParse(args, out var command))
        {
            using var operation = telemetry.StartOperation("zoomin.setup.apply");
            var install = new ElevatedInstall(
                services.Layout, services.Verifier, services.Registration, services.SecureDesktop, telemetry.Logger<SetupProgramCategory>());
            return (int)install.Run(command);
        }

        Services = services;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
        return 0;
    }

    /// <summary>Log category of the static entry point.</summary>
    private sealed class SetupProgramCategory;
}
