using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Acquires an exclusive-identity, read-only lease over a minidump and its directory.
/// The handles remain open until DumpChk terminates, denying write/delete sharing.
/// </summary>
internal interface IDumpCheckFileGuard
{
    IDisposable? TryAcquire(string directory, string dumpPath);
}

internal sealed class WindowsDumpCheckFileGuard : IDumpCheckFileGuard
{
    private const uint GenericRead = 0x80000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint ShareRead = 0x00000001;
    private const uint ShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;

    public IDisposable? TryAcquire(string directory, string dumpPath)
    {
        if (!OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(directory)
            || string.IsNullOrWhiteSpace(dumpPath))
            return null;

        string expectedDirectory;
        string expectedFile;
        try
        {
            expectedDirectory = Path.GetFullPath(directory);
            expectedFile = Path.GetFullPath(dumpPath);

            if (!SamePath(Path.GetDirectoryName(expectedFile), expectedDirectory))
                return null;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }

        SafeFileHandle? directoryHandle = null;
        SafeFileHandle? dumpHandle = null;
        try
        {
            // OPEN_REPARSE_POINT opens a link itself, not its redirected target.
            directoryHandle = OpenHandle(expectedDirectory, FileReadAttributes,
                ShareRead | ShareWrite, BackupSemantics | OpenReparsePoint);
            if (!HasAttributes(directoryHandle, FileAttributes.Directory)
                || HasAttributes(directoryHandle, FileAttributes.ReparsePoint)
                || !SamePath(ReadFinalPath(directoryHandle), expectedDirectory))
                return null;

            // Deny concurrent write/delete while still allowing DumpChk to read.
            dumpHandle = OpenHandle(expectedFile, GenericRead,
                ShareRead, OpenReparsePoint);
            if (HasAttributes(dumpHandle, FileAttributes.Directory)
                || HasAttributes(dumpHandle, FileAttributes.ReparsePoint)
                || !SamePath(ReadFinalPath(dumpHandle), expectedFile))
                return null;

            var lease = new HandleLease(directoryHandle, dumpHandle);
            directoryHandle = null;
            dumpHandle = null;
            return lease;
        }
        finally
        {
            dumpHandle?.Dispose();
            directoryHandle?.Dispose();
        }
    }

    internal static bool SamePath(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left)
           && !string.IsNullOrWhiteSpace(right)
           && string.Equals(Normalize(left), Normalize(right),
               StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path)
    {
        var canonical = path;
        if (canonical.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            canonical = @"\\" + canonical[8..];
        else if (canonical.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            canonical = canonical[4..];

        return Path.TrimEndingDirectorySeparator(canonical);
    }

    private static SafeFileHandle OpenHandle(
        string path, uint desiredAccess, uint shareMode, uint flags)
    {
        var handle = CreateFile(path, desiredAccess, shareMode,
            IntPtr.Zero, OpenExisting, flags, IntPtr.Zero);
        if (!handle.IsInvalid)
            return handle;

        var code = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(code);
    }

    private static bool HasAttributes(SafeFileHandle handle, FileAttributes attributes)
    {
        if (!GetFileInformationByHandle(handle, out var information))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return (information.FileAttributes & (uint)attributes) != 0;
    }

    private static string ReadFinalPath(SafeFileHandle handle)
    {
        var buffer = new StringBuilder(512);
        var chars = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (chars == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        if (chars >= buffer.Capacity)
        {
            if (chars > 32768)
                throw new IOException("Windows returned an excessively long handle path.");

            buffer = new StringBuilder((int)chars + 1);
            chars = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (chars == 0 || chars >= buffer.Capacity)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return buffer.ToString();
    }

    private sealed class HandleLease : IDisposable
    {
        private readonly SafeFileHandle _directory;
        private readonly SafeFileHandle _dump;
        private bool _disposed;

        public HandleLease(SafeFileHandle directory, SafeFileHandle dump)
        {
            _directory = directory;
            _dump = dump;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _dump.Dispose();
            _directory.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileInformation
    {
        public uint FileAttributes;
        public NativeFileTime CreationTime;
        public NativeFileTime LastAccessTime;
        public NativeFileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string path, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle, out NativeFileInformation information);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle handle, StringBuilder buffer, uint bufferLength, uint flags);
}
