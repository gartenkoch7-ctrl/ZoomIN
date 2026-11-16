using System.Globalization;
using System.Xml.Linq;

namespace ZoomIN.Core.Lifecycle;

/// <summary>
/// The scheduled task that starts an installed ZoomIN at sign-in (ADR-0030, Q9): Ease of Access
/// starts its ATs in a job that ends at the next lock screen, so the tray app cannot run from there.
/// A task with a logon trigger for one user needs no admin rights, starts before the Run key, and
/// is not ended by a desktop switch.
/// </summary>
public static class AutostartTask
{
    /// <summary>All ZoomIN autostart tasks start with this name; the user's SID follows.</summary>
    public const string NamePrefix = "ZoomIN Autostart ";

    // The task scheduler cannot start a uiAccess exe itself (ERROR_ELEVATION_REQUIRED, 740); only
    // ShellExecute lets AppInfo grant UIAccess (ADR-0012). rundll32 runs ShellExecute without a window.
    private const string Launcher = @"%SystemRoot%\System32\rundll32.exe";
    private const string LauncherArguments = "shell32.dll,ShellExec_RunDLL ";

    private static readonly XNamespace s_ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>One task per user: tasks live in the machine-wide root folder.</summary>
    public static string Name(string userSid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        return NamePrefix + userSid;
    }

    /// <summary>Task XML for schtasks /Create /XML (save as UTF-16).</summary>
    /// <param name="userSid">The signed-in user: trigger and principal, so no admin rights are needed.</param>
    /// <param name="executable">The stable "current" path of the installed exe.</param>
    public static string Xml(string userSid, string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        var task = new XElement(
            s_ns + "Task",
            new XAttribute("version", "1.2"),
            new XElement(s_ns + "RegistrationInfo", new XElement(s_ns + "Description", "Starts ZoomIN after sign-in.")),
            new XElement(s_ns + "Triggers", new XElement(
                s_ns + "LogonTrigger",
                new XElement(s_ns + "Enabled", "true"),
                new XElement(s_ns + "UserId", userSid))),
            new XElement(s_ns + "Principals", new XElement(
                s_ns + "Principal",
                new XAttribute("id", "Author"),
                new XElement(s_ns + "UserId", userSid),
                new XElement(s_ns + "LogonType", "InteractiveToken"),
                new XElement(s_ns + "RunLevel", "LeastPrivilege"))),
            Settings(),
            new XElement(s_ns + "Actions", new XAttribute("Context", "Author"), new XElement(
                s_ns + "Exec",
                new XElement(s_ns + "Command", Launcher),
                new XElement(s_ns + "Arguments", LauncherArguments + "\"" + executable + "\""))));
        return new XDeclaration("1.0", "UTF-16", null) + Environment.NewLine + task;
    }

    private static XElement Settings() => new(
        s_ns + "Settings",
        new XElement(s_ns + "MultipleInstancesPolicy", "IgnoreNew"),

        // A laptop on battery still needs its magnifier.
        new XElement(s_ns + "DisallowStartIfOnBatteries", "false"),
        new XElement(s_ns + "StopIfGoingOnBatteries", "false"),

        // Stopping the task must never kill ZoomIN; the task itself ends with rundll32 at once.
        new XElement(s_ns + "AllowHardTerminate", "false"),
        new XElement(s_ns + "ExecutionTimeLimit", "PT0S"),

        // Tasks run below normal priority by default (7); ZoomIN renders every frame (4 = normal).
        new XElement(s_ns + "Priority", 4.ToString(CultureInfo.InvariantCulture)));
}
