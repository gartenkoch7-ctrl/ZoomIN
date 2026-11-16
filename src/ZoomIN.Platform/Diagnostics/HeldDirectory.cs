using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using ZoomIN.Platform.SecureDesktop;

namespace ZoomIN.Platform.Diagnostics;

/// <summary>
/// A directory opened by an elevated step that writes into a user's folder (M10, Q7): checked once
/// (no reparse point, Windows opened exactly the expected path, so no link anywhere above it) and
/// then only used through its handle. Everything inside is created relative to <see cref="Handle"/>
/// (Interop.NtFiles), so it does not matter what the user does to the path afterwards – share modes
/// alone would not stop the user from turning the folder into a mount point. So it shares everything:
/// another program holding the folder (indexer, sync client, backup) must not make the copy fail.
/// </summary>
internal sealed unsafe class HeldDirectory : IDisposable
{
    private const uint FileListDirectory = 0x0001;
    private const uint FileTraverse = 0x0020;
    private const uint FileReadAttributes = 0x0080;

    private readonly SafeFileHandle _handle;

    private HeldDirectory(SafeFileHandle handle) => _handle = handle;

    /// <summary>The checked directory itself, for creating entries relative to it.</summary>
    public SafeFileHandle Handle => _handle;

    /// <returns>The held directory, or null if it is missing, a link of any kind or not where the path says.</returns>
    public static HeldDirectory? TryOpen(string path)
    {
        var handle = PInvoke.CreateFile(
            path,
            FileListDirectory | FileTraverse | FileReadAttributes,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT,
            null);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        if (!IsPlainDirectory(handle) || !GuardedFileReader.IsSamePath(handle, path))
        {
            handle.Dispose();
            return null;
        }

        return new HeldDirectory(handle);
    }

    public void Dispose() => _handle.Dispose();

    private static bool IsPlainDirectory(SafeFileHandle handle)
    {
        if (!PInvoke.GetFileInformationByHandle(handle, out var info))
        {
            return false;
        }

        const uint ReparsePoint = (uint)FileAttributes.ReparsePoint;
        const uint Directory = (uint)FileAttributes.Directory;
        return (info.dwFileAttributes & (ReparsePoint | Directory)) == Directory;
    }
}
