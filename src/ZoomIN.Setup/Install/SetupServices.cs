using ZoomIN.Core.Folders;
using ZoomIN.Core.SecureDesktop;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;
using ZoomIN.Platform.Diagnostics;
using ZoomIN.Platform.Folders;
using ZoomIN.Platform.SecureDesktop;
using ZoomIN.Platform.Shell;
using ZoomIN.Platform.Updates;

namespace ZoomIN.Setup.Install;

/// <summary>
/// The setup's composition root (ADR-0025): every service built once, here. The setup is small
/// and short-lived, so a hand-written list replaces a DI container.
/// </summary>
internal sealed class SetupServices : IDisposable
{
    public SetupServices(TelemetryHost telemetry)
    {
        Telemetry = telemetry;
        Registration = new WindowsInstallRegistration(Shortcuts);
        SecureDesktop = new SystemSecureDesktopInstall(telemetry.Logger<SystemSecureDesktopInstall>());
        Elevated = new ShellElevatedStep(telemetry.Logger<ShellElevatedStep>());
    }

    public TelemetryHost Telemetry { get; }

    /// <summary>Where ZoomIN is installed: Program Files\ZoomIN (ADR-0015).</summary>
    public InstallLayout Layout { get; } =
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ZoomIN"));

    public IAppFolders Folders { get; } = new WindowsAppFolders();

    /// <summary>One client for release list and download.</summary>
    public HttpClient Http { get; } = new UpdateHttpClient();

    public IShortcuts Shortcuts { get; } = new WindowsShortcuts();

    public IInstallRegistration Registration { get; }

    public ISecureDesktopInstall SecureDesktop { get; }

    public IElevatedStep Elevated { get; }

    public ISignatureVerifier Verifier { get; } = new AuthenticodeVerifier();

    public void Dispose() => Http.Dispose();
}
