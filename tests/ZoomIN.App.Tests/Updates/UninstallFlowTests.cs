using Microsoft.Extensions.Logging.Abstractions;
using ZoomIN.App.Localization;
using ZoomIN.App.Updates;
using ZoomIN.Core.Shell;
using ZoomIN.Core.Updates;

namespace ZoomIN.App.Tests.Updates;

/// <summary>
/// "Remove" from Windows Apps (ADR-0014) with fake dialogs, processes and registry (M14 step 11):
/// what happens on Cancel, on a declined administrator prompt, and with or without the settings.
/// </summary>
public sealed class UninstallFlowTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ZoomIN-UninstallTests-").FullName;
    private readonly UninstallFakes _fakes = new();
    private readonly UninstallFakes.Folders _folders;

    public UninstallFlowTests()
    {
        _folders = new UninstallFakes.Folders(_root);
        Directory.CreateDirectory(_folders.Local);
        Directory.CreateDirectory(_folders.Roaming);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void NotInstalled_ChangesNothing()
    {
        _fakes.Layout = null;

        Assert.Equal((int)UpdateResult.NotInstalled, Flow().Run());
        Assert.Empty(_fakes.Calls);
    }

    [Fact]
    public void Cancel_StopsBeforeTheAdministratorStep()
    {
        _fakes.Answer = DialogAnswer.Cancel;

        Assert.Equal(0, Flow().Run());
        Assert.Equal(["ask"], _fakes.Calls);
        Assert.True(Directory.Exists(_folders.Roaming));
    }

    [Fact]
    public void DeclinedOrFailedRemoval_ReportsAnError_AndKeepsTheUsersTraces()
    {
        _fakes.Answer = DialogAnswer.No;
        _fakes.ElevatedSucceeds = false;

        Assert.Equal((int)UpdateResult.Failed, Flow().Run());
        Assert.Equal(["ask", "elevated", "inform-error"], _fakes.Calls);
        Assert.True(Directory.Exists(_folders.Roaming));
        Assert.True(Directory.Exists(_folders.Local));
    }

    [Fact]
    public void KeepSettings_RemovesAutostartAndShortcut_ButNotTheFolders()
    {
        _fakes.Answer = DialogAnswer.Yes;

        Assert.Equal(0, Flow().Run());
        Assert.Equal(["ask", "elevated", "autostart False", "shortcut-remove desktop", "inform"], _fakes.Calls);
        Assert.True(Directory.Exists(_folders.Roaming));
        Assert.True(Directory.Exists(_folders.Local));
    }

    [Fact]
    public void DropSettings_AlsoDeletesTheUsersFolders()
    {
        _fakes.Answer = DialogAnswer.No;
        File.WriteAllText(Path.Combine(_folders.Roaming, "settings.json"), "{}");

        Assert.Equal(0, Flow().Run());
        Assert.False(Directory.Exists(_folders.Roaming));
        Assert.False(Directory.Exists(_folders.Local));
        Assert.Equal("inform", _fakes.Calls[^1]);
    }

    [Fact]
    public void FailingUserCleanup_DoesNotStopTheUninstall()
    {
        _fakes.Answer = DialogAnswer.No;
        _fakes.UserCleanupFails = true;

        // The elevated removal has already happened; leftovers of the user must not turn it into an error.
        Assert.Equal(0, Flow().Run());
        Assert.Equal("inform", _fakes.Calls[^1]);
        Assert.False(Directory.Exists(_folders.Roaming));
    }

    [Fact]
    public void ElevatedStep_EndsZoomIn_UnregistersAndSchedulesTheFolder()
    {
        Assert.Equal(0, Flow().RemoveElevated(4711));

        Assert.Equal(
            ["end-others 4711", "unregister", "secure-remove", @"schedule-removal C:\Program Files\ZoomIN 4711"],
            _fakes.Calls);
    }

    [Fact]
    public void ElevatedStep_NotInstalled_ChangesNothing()
    {
        _fakes.Layout = null;

        Assert.Equal((int)UpdateResult.NotInstalled, Flow().RemoveElevated(4711));
        Assert.Empty(_fakes.Calls);
    }

    private UninstallFlow Flow() => new(
        new UninstallFakes.Dialog(_fakes), new UninstallFakes.Shortcuts(_fakes), new UninstallFakes.Registration(_fakes),
        new UninstallFakes.SecureDesktop(_fakes), new UninstallFakes.Install(_fakes), new UninstallFakes.Removal(_fakes),
        new UninstallFakes.Autostart(_fakes), _folders, AppTexts.CreateLocalizer(), NullLogger<UninstallFlow>.Instance);
}
