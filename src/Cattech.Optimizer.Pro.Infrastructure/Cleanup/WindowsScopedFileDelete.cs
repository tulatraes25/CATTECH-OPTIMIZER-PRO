using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Cattech.Optimizer.Pro.Infrastructure.Cleanup;

/// <summary>
/// Windows-only, handle-verified deletion used by the development cleanup.
/// A candidate is opened with DELETE permission, verified using THAT handle,
/// then marked for disposition on the SAME handle.
/// CI tests cover isolated fixtures; independent review and release/hardware
/// qualification remain mandatory before use on customer computers.
/// </summary>
internal static class WindowsScopedFileDelete
{
    private const uint FileReadAttributes = 0x80;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeSystem = 0x00000004;
    private const uint FileAttributeHidden = 0x00000002;
    private const uint FileAttributeReadOnly = 0x00000001;
    private const int FileDispositionInfoClass = 4;
    private const uint MinFileAgeSeconds = 60;

    /// <summary>
    /// Deletes only a handle-verified, old, ordinary file at precisely its
    /// expected path under the handle-verified directory. Returns false if
    /// anything cannot be proven. All caller-supplied paths are untrusted.
    /// </summary>
    internal static bool TryDelete(
        string allowedDirectory, string candidatePath, out long deletedBytes)
    {
        deletedBytes = 0;
        if (!OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(allowedDirectory)
            || string.IsNullOrWhiteSpace(candidatePath)
            || !Path.IsPathFullyQualified(allowedDirectory)
            || !Path.IsPathFullyQualified(candidatePath)
            || allowedDirectory.StartsWith(@"\\", StringComparison.Ordinal)
            || candidatePath.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        string root;
        string candidate;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedDirectory));
            candidate = Path.GetFullPath(candidatePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }

        // Block alternate data streams, device paths, and root-adjacent files.
        if (candidate.Length < 3 || root.Length < 3
            || candidate.AsSpan(2).Contains(':')
            || root.AsSpan(2).Contains(':')
            || !candidate.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            return false;

        using var rootHandle = CreateFile(
            root, FileReadAttributes, FileShareRead | FileShareWrite,
            IntPtr.Zero, OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics, IntPtr.Zero);
        if (rootHandle.IsInvalid
            || !GetFileInformationByHandle(rootHandle, out var rootInfo)
            || (rootInfo.FileAttributes & FileAttributeDirectory) == 0
            || (rootInfo.FileAttributes & FileAttributeReparsePoint) != 0
            || !MatchesFinalPath(rootHandle, root))
            return false;

        // Do not follow a leaf symlink. FileShareRead denies concurrent
        // write/delete sharing, so a competing writer cannot replace an open
        // candidate before handle-based disposition.
        using var fileHandle = CreateFile(
            candidate, FileReadAttributes | DeleteAccess, FileShareRead,
            IntPtr.Zero, OpenExisting, FileFlagOpenReparsePoint, IntPtr.Zero);
        if (fileHandle.IsInvalid
            || !GetFileInformationByHandle(fileHandle, out var info))
            return false;

        const uint excluded = FileAttributeDirectory | FileAttributeReparsePoint
            | FileAttributeHidden | FileAttributeSystem | FileAttributeReadOnly;
        if ((info.FileAttributes & excluded) != 0 || info.NumberOfLinks != 1
            || !MatchesFinalPath(fileHandle, candidate))
            return false;

        // All decisions use metadata from the same opened identity.
        var lastWriteTicks = ((long)info.LastWriteTime.High << 32)
            | info.LastWriteTime.Low;
        DateTime lastWriteUtc;
        try { lastWriteUtc = DateTime.FromFileTimeUtc(lastWriteTicks); }
        catch (ArgumentOutOfRangeException) { return false; }
        if (lastWriteUtc > DateTime.UtcNow.AddSeconds(-MinFileAgeSeconds))
            return false;

        var length = ((ulong)info.FileSizeHigh << 32) | info.FileSizeLow;
        if (length > long.MaxValue)
            return false;

        var disposition = new FileDispositionInfo { DeleteFile = true };
        if (!SetFileInformationByHandle(fileHandle, FileDispositionInfoClass,
            ref disposition, (uint)Marshal.SizeOf<FileDispositionInfo>()))
            return false;

        // The kernel unlinks THIS handle's file when it closes. No second
        // pathname-based File.Delete() occurs after identity verification.
        deletedBytes = (long)length;
        return true;
    }

    private static bool MatchesFinalPath(SafeFileHandle handle, string expectedPath)
    {
        var buffer = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, buffer,
            (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity)
            return false;

        var actual = buffer.ToString();
        if (!actual.StartsWith(@"\\?\", StringComparison.Ordinal)
            || actual.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return false;

        actual = actual[4..];
        return string.Equals(
            Path.TrimEndingDirectorySeparator(actual),
            Path.TrimEndingDirectorySeparator(expectedPath),
            StringComparison.OrdinalIgnoreCase);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
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

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDispositionInfo
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool DeleteFile;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition,
        uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle, out ByHandleFileInformation information);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle handle, StringBuilder output, uint length, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle handle, int infoClass, ref FileDispositionInfo info, uint length);
}
