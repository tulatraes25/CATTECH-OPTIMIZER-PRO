using Cattech.Optimizer.Pro.Infrastructure.Cleanup;
using System.Runtime.InteropServices;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// These tests create/delete ONLY dedicated synthetic Windows temp fixtures.
/// They never scan or invoke CATTECH's real cleanup destinations.
/// </summary>
public sealed class WindowsScopedFileDeleteTests
{
    [Fact]
    public void VerifiedOldSyntheticFile_IsDeletedByOpenedHandle()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "verified-old.txt");
            const string payload = "SYNTHETIC-WINDOWS-HANDLE-TEST";
            File.WriteAllText(file, payload);
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-5));
            var expectedBytes = new FileInfo(file).Length;

            var deleted = WindowsScopedFileDelete.TryDelete(root, file, out var bytes);

            Assert.True(deleted);
            Assert.Equal(expectedBytes, bytes);
            Assert.False(File.Exists(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void RecentSyntheticFile_IsNotDeleted()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "recent.txt");
            File.WriteAllText(file, "PRESERVE");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("PRESERVE", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void FileOutsideAllowedRoot_IsNeverDeleted()
    {
        var root = NewFixture();
        var outside = NewFixture();
        try
        {
            var file = Path.Combine(outside, "important.txt");
            File.WriteAllText(file, "KEEP-THIS");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-6));

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("KEEP-THIS", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void OpenFileWithNoDeleteSharing_IsNotRemoved()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "locked.txt");
            File.WriteAllText(file, "KEEP-LOCKED");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-7));
            using (var held = new FileStream(file, FileMode.Open, FileAccess.Read,
                FileShare.Read))
            {
                Assert.False(WindowsScopedFileDelete.TryDelete(
                    root, file, out var bytes));
                Assert.Equal(0L, bytes);
            }
            Assert.Equal("KEEP-LOCKED", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void FileSymlink_NeverDeletesTarget_WhenSupportedByRunner()
    {
        var root = NewFixture();
        var outside = NewFixture();
        try
        {
            var target = Path.Combine(outside, "target.txt");
            var symlink = Path.Combine(root, "link.txt");
            File.WriteAllText(target, "SYNTHETIC-KEEP");
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddMinutes(-9));

            try { File.CreateSymbolicLink(symlink, target); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or PlatformNotSupportedException)
            {
                // Some CI policies prohibit symlink creation; no false claim
                // about symlink coverage in that environment.
                return;
            }

            Assert.False(WindowsScopedFileDelete.TryDelete(root, symlink, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("SYNTHETIC-KEEP", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void NestedDirectorySymlink_DoesNotRedirectDeletion_WhenSupported()
    {
        var root = NewFixture();
        var outside = NewFixture();
        try
        {
            var target = Path.Combine(outside, "target.txt");
            var link = Path.Combine(root, "linked-folder");
            File.WriteAllText(target, "SAFE-KEEP");
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddMinutes(-9));

            try { Directory.CreateSymbolicLink(link, outside); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or PlatformNotSupportedException)
            {
                return;
            }

            Assert.False(WindowsScopedFileDelete.TryDelete(
                root, Path.Combine(link, "target.txt"), out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("SAFE-KEEP", File.ReadAllText(target));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }


    [Fact]
    public void NestedOrdinaryFile_IsNeverDeletedEvenWhenOld()
    {
        var root = NewFixture();
        try
        {
            var nested = Path.Combine(root, "synthetic-subdirectory");
            Directory.CreateDirectory(nested);
            var file = Path.Combine(nested, "old-nested.txt");
            File.WriteAllText(file, "NESTED-SYNTHETIC-KEEP");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-8));

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("NESTED-SYNTHETIC-KEEP", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void HardLinkedFile_IsNeverDeletedThroughEitherName()
    {
        var root = NewFixture();
        try
        {
            Assert.Equal("NTFS", new DriveInfo(root).DriveFormat.ToUpperInvariant());
            var original = Path.Combine(root, "original.txt");
            var alternate = Path.Combine(root, "linked.txt");
            File.WriteAllText(original, "HARDLINK-SYNTHETIC-KEEP");
            File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddMinutes(-9));

            Assert.True(CreateHardLink(alternate, original, IntPtr.Zero),
                $"NTFS synthetic hard-link setup failed: {Marshal.GetLastWin32Error()}");
            Assert.False(WindowsScopedFileDelete.TryDelete(root, alternate, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.True(File.Exists(original));
            Assert.True(File.Exists(alternate));
            Assert.Equal("HARDLINK-SYNTHETIC-KEEP", File.ReadAllText(original));
            Assert.Equal("HARDLINK-SYNTHETIC-KEEP", File.ReadAllText(alternate));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ReadOnlyOldFile_IsNotDeleted()
    {
        var root = NewFixture();
        var file = Path.Combine(root, "readonly-old.txt");
        try
        {
            File.WriteAllText(file, "READONLY-SYNTHETIC-KEEP");
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(-9));
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("READONLY-SYNTHETIC-KEEP", File.ReadAllText(file));
        }
        finally
        {
            if (File.Exists(file))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll",
        EntryPoint = "CreateHardLinkW", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
        SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(
        System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string newFileName, string existingFileName, IntPtr securityAttributes);

    private static string NewFixture()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-scoped-handle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
