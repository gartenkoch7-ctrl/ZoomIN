using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using ZoomIN.Core.SecureDesktop;

namespace ZoomIN.Platform.SecureDesktop;

/// <summary>
/// Reads a small file that a user could have tampered with, from a process running as SYSTEM
/// (ADR-0016). Refuses links of every kind: the path Windows actually opened must equal the
/// requested one (catches junctions and symbolic links anywhere in the path, also when swapped
/// after a check), the file must be a regular disk file with a single name (no hard link to a
/// file the user could not read) and not larger than the limit. Only "X:\…" paths are touched at
/// all, so no network share is ever contacted.
/// </summary>
public static unsafe class GuardedFileReader
{
    public const int DefaultMaxBytes = 256 * 1024;

    /// <returns>The content, or null if the file is missing or fails a check (never throws).</returns>
    public static byte[]? TryRead(string path, int maxBytes = DefaultMaxBytes)
    {
        try
        {
            // Checked before the first file-system call: opening a UNC path would already sign in there.
            if (!LocalPathRule.IsDriveLetterPath(path))
            {
                return null;
            }

            var expected = Path.GetFullPath(path);
            if (!LocalPathRule.IsDriveLetterPath(expected) || !File.Exists(expected))
            {
                return null;
            }

            using var handle = File.OpenHandle(expected, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!IsPlainFile(handle) || !IsSamePath(handle, expected))
            {
                return null;
            }

            var length = RandomAccess.GetLength(handle);
            if (length > maxBytes)
            {
                return null;
            }

            var content = new byte[length];
            var read = RandomAccess.Read(handle, content, 0);
            return read == length ? content : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsPlainFile(SafeFileHandle handle)
    {
        if (PInvoke.GetFileType(handle) != FILE_TYPE.FILE_TYPE_DISK)
        {
            return false;
        }

        if (!PInvoke.GetFileInformationByHandle(handle, out var info))
        {
            return false;
        }

        const uint ReparsePoint = (uint)FileAttributes.ReparsePoint;
        const uint Directory = (uint)FileAttributes.Directory;
        return info.nNumberOfLinks == 1 && (info.dwFileAttributes & (ReparsePoint | Directory)) == 0;
    }

    private static bool IsSamePath(SafeFileHandle handle, string expected)
    {
        Span<char> buffer = stackalloc char[1024];
        var length = PInvoke.GetFinalPathNameByHandle(handle, buffer, GETFINALPATHNAMEBYHANDLE_FLAGS.FILE_NAME_NORMALIZED);
        if (length == 0 || length >= buffer.Length)
        {
            return false;
        }

        // Only a local "\\?\X:\…" result counts; "\\?\UNC\…" (network) is refused outright.
        var actual = buffer[..(int)length].ToString();
        if (!actual.StartsWith(@"\\?\", StringComparison.Ordinal) || actual.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        actual = actual[4..];

        // 8.3 short names in the requested path would differ legitimately; settings paths come
        // from the known-folder API and are always long names.
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
