using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ZoomIN.Platform.Interop;

/// <summary>
/// NtCreateFile relative to an open directory handle, for an elevated step that writes into a
/// folder the user controls (M10, Q7). The name is resolved inside that directory only, with
/// OBJ_DONT_REPARSE: no junction, mount point or object-manager link anywhere can redirect it,
/// and FILE_CREATE fails instead of opening anything that already exists. CreateFile cannot do this.
/// </summary>
internal static unsafe partial class NtFiles
{
    /// <summary>STATUS_OBJECT_NAME_COLLISION: the name exists (in any form, also as a link).</summary>
    public const int NameCollision = unchecked((int)0xC0000035);

    private const uint ObjCaseInsensitive = 0x40;
    private const uint ObjDontReparse = 0x1000;
    private const uint FileCreate = 2;
    private const uint FileDirectoryFile = 0x1;
    private const uint FileSynchronousIoNonAlert = 0x20;
    private const uint FileNonDirectoryFile = 0x40;
    private const uint FileOpenReparsePoint = 0x200000;
    private const uint FileAttributeNormal = 0x80;
    private const uint FileShareRead = 0x1;

    // FILE_LIST_DIRECTORY | FILE_ADD_FILE | FILE_TRAVERSE | FILE_READ_ATTRIBUTES | READ_CONTROL | SYNCHRONIZE.
    private const uint DirectoryAccess = 0x1 | 0x2 | 0x20 | 0x80 | 0x20000 | 0x100000;

    // FILE_WRITE_DATA | FILE_READ_ATTRIBUTES | FILE_WRITE_ATTRIBUTES | SYNCHRONIZE.
    private const uint FileAccess = 0x2 | 0x80 | 0x100 | 0x100000;

    /// <summary>
    /// Creates the subdirectory <paramref name="name"/> with <paramref name="securityDescriptor"/>,
    /// held open with read sharing only (it can be neither renamed nor deleted meanwhile).
    /// </summary>
    /// <returns>The NTSTATUS; negative on failure.</returns>
    public static int CreateDirectory(SafeHandle parent, string name, byte[] securityDescriptor, out SafeFileHandle directory)
    {
        fixed (byte* descriptor = securityDescriptor)
        {
            return Create(parent, name, descriptor, DirectoryAccess, FileShareRead, FileDirectoryFile, out directory);
        }
    }

    /// <summary>Creates the new file <paramref name="name"/> for writing; it inherits the directory's access list.</summary>
    /// <returns>The NTSTATUS; negative on failure.</returns>
    public static int CreateFile(SafeHandle parent, string name, out SafeFileHandle file) =>
        Create(parent, name, null, FileAccess, 0, FileNonDirectoryFile, out file);

    private static int Create(SafeHandle parent, string name, void* descriptor, uint access, uint share, uint kind, out SafeFileHandle result)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrEmpty(name);
        var added = false;
        try
        {
            parent.DangerousAddRef(ref added);
            fixed (char* chars = name)
            {
                var objectName = new UnicodeString
                {
                    Length = (ushort)(name.Length * sizeof(char)),
                    MaximumLength = (ushort)(name.Length * sizeof(char)),
                    Buffer = chars,
                };
                var attributes = new ObjectAttributes
                {
                    Length = (uint)sizeof(ObjectAttributes),
                    RootDirectory = parent.DangerousGetHandle(),
                    ObjectName = &objectName,
                    Attributes = ObjCaseInsensitive | ObjDontReparse,
                    SecurityDescriptor = descriptor,
                };
                IoStatusBlock io;
                var status = NtCreateFile(out var handle, access, &attributes, &io, null, FileAttributeNormal, share, FileCreate,
                    kind | FileSynchronousIoNonAlert | FileOpenReparsePoint, null, 0);
                result = new SafeFileHandle(status >= 0 ? handle : 0, ownsHandle: status >= 0);
                return status;
            }
        }
        finally
        {
            if (added)
            {
                parent.DangerousRelease();
            }
        }
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtCreateFile(
        out nint handle, uint desiredAccess, ObjectAttributes* objectAttributes, IoStatusBlock* ioStatus, long* allocationSize,
        uint fileAttributes, uint shareAccess, uint createDisposition, uint createOptions, void* eaBuffer, uint eaLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public char* Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public uint Length;
        public nint RootDirectory;
        public UnicodeString* ObjectName;
        public uint Attributes;
        public void* SecurityDescriptor;
        public void* SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public nint Status;
        public nuint Information;
    }
}
