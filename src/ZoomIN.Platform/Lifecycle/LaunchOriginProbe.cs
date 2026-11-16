using System.ComponentModel;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Diagnostics.ToolHelp;
using Windows.Win32.System.JobObjects;
using ZoomIN.Core.Lifecycle;

namespace ZoomIN.Platform.Lifecycle;

/// <summary>
/// Reads how this process was started (ADR-0030): job membership, the job's limits and the parent's
/// process name. Runs before the single-instance check, so it only reads and never throws.
/// </summary>
public sealed unsafe class LaunchOriginProbe
{
    private readonly uint _self = (uint)Environment.ProcessId;

    public LaunchOrigin Read(bool leftJob)
    {
        BOOL result;
        var inJob = PInvoke.IsProcessInJob(PInvoke.GetCurrentProcess(), HANDLE.Null, &result) && result;
        return new LaunchOrigin(inJob, inJob ? JobLimits() : 0, ParentProcess(), leftJob);
    }

    // A null job handle asks for the immediate job of the calling process.
    private static uint JobLimits()
    {
        JOBOBJECT_BASIC_LIMIT_INFORMATION info = default;
        return PInvoke.QueryInformationJobObject(
            HANDLE.Null, JOBOBJECTINFOCLASS.JobObjectBasicLimitInformation, &info, (uint)sizeof(JOBOBJECT_BASIC_LIMIT_INFORMATION), null)
            ? (uint)info.LimitFlags
            : 0;
    }

    /// <summary>The parent's name, or null if it has exited (its id may already belong to another process).</summary>
    private string? ParentProcess()
    {
        if (ParentEntry() is not { } parent)
        {
            return null;
        }

        try
        {
            using var self = Process.GetCurrentProcess();
            using var process = Process.GetProcessById((int)parent.Id);

            // Windows reuses process ids: a "parent" younger than us is someone else.
            if (process.StartTime > self.StartTime)
            {
                return null;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // Gone, or not ours to inspect: then the id may already belong to another account's
            // process (a reused id), so the snapshot's name proves nothing.
            return null;
        }

        return Path.GetFileNameWithoutExtension(parent.Exe);
    }

    private (uint Id, string Exe)? ParentEntry()
    {
        var snapshot = PInvoke.CreateToolhelp32Snapshot(CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPPROCESS, 0);
        if (snapshot.IsNull || (nint)snapshot.Value == -1)
        {
            return null;
        }

        try
        {
            // Two passes over the same snapshot: first our entry, then our parent's.
            return Find(snapshot, _self) is { } self && Find(snapshot, self.th32ParentProcessID) is { } parent
                ? (parent.th32ProcessID, parent.szExeFile.ToString())
                : null;
        }
        finally
        {
            PInvoke.CloseHandle(snapshot);
        }
    }

    private static PROCESSENTRY32W? Find(HANDLE snapshot, uint processId)
    {
        var entry = new PROCESSENTRY32W { dwSize = (uint)sizeof(PROCESSENTRY32W) };
        for (var more = PInvoke.Process32FirstW(snapshot, &entry); more; more = PInvoke.Process32NextW(snapshot, &entry))
        {
            if (entry.th32ProcessID == processId)
            {
                return entry;
            }
        }

        return null;
    }
}
