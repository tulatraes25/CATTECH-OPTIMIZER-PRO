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
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
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
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

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
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
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
            File.SetCreationTimeUtc(target, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddDays(-8));

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
            File.SetCreationTimeUtc(target, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddDays(-8));

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
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

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
            File.SetCreationTimeUtc(original, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddDays(-8));

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
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));
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


    [Fact]
    public void RecentlyCreatedFile_WithSpoofedAncientModification_IsPreserved()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "copied-new.txt");
            File.WriteAllText(file, "KEEP-COPIED-CONTENT");
            // Copying or extracting a file may preserve an old modified time.
            // The actual NTFS creation timestamp must still block deletion.
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-40));

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("KEEP-COPIED-CONTENT", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void RecentlyModifiedFile_WithAncientCreation_IsPreserved()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "changed-today.txt");
            File.WriteAllText(file, "KEEP-RECENT-UPDATE");
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-40));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow);

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("KEEP-RECENT-UPDATE", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }


    [Fact]
    public void VolumeRoot_IsRejectedWithoutAttemptingToDeleteUserFiles()
    {
        var fixture = NewFixture();
        try
        {
            var volumeRoot = Path.GetPathRoot(fixture)!;
            Assert.False(WindowsScopedFileDelete.IsSafeAllowedRoot(volumeRoot));
            Assert.True(WindowsScopedFileDelete.IsSafeAllowedRoot(fixture));

            var file = Path.Combine(fixture, "keep-synthetic.txt");
            File.WriteAllText(file, "KEEP-ROOT-SCOPE");
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

            Assert.False(WindowsScopedFileDelete.TryDelete(volumeRoot, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("KEEP-ROOT-SCOPE", File.ReadAllText(file));
        }
        finally { Directory.Delete(fixture, recursive: true); }
    }

    [Fact]
    public void SiblingWithSameDirectoryPrefix_IsNotAnAllowedChild()
    {
        var root = NewFixture();
        var neighbor = root + "-prefix-neighbor";
        Directory.CreateDirectory(neighbor);
        try
        {
            var file = Path.Combine(neighbor, "synthetic.txt");
            File.WriteAllText(file, "KEEP-NEIGHBOR");
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

            Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("KEEP-NEIGHBOR", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(neighbor, recursive: true);
        }
    }

    [Fact]
    public void AlternateDataStreamInput_DoesNotDeleteBaseFile()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "synthetic-base.txt");
            File.WriteAllText(file, "KEEP-BASE");
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

            // Colon-bearing input is rejected before any native open. The test
            // does not need to create an ADS and does not alter user data.
            Assert.False(WindowsScopedFileDelete.TryDelete(
                root, file + ":cattech_synthetic_ads", out var bytes));
            Assert.Equal(0L, bytes);
            Assert.Equal("KEEP-BASE", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }


    [Fact]
    public void OldFileWithOpenReaderAllowingDelete_IsPreservedUntilReaderCloses()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "reader-still-active.txt");
            File.WriteAllText(file, "KEEP-ACTIVE-READER");
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

            // The existing reader permits delete sharing. With the previous
            // FILE_SHARE_READ open, the native cleanup could delete it.
            using (var held = new FileStream(file, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
                Assert.Equal(0L, bytes);
                Assert.True(File.Exists(file));
            }

            // After the reader closes, an otherwise eligible old fixture
            // can be deleted using the same verified handle policy.
            Assert.True(WindowsScopedFileDelete.TryDelete(root, file, out var removed));
            Assert.True(removed > 0);
            Assert.False(File.Exists(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void OldFileWithOpenWriterAllowingDelete_IsPreserved()
    {
        var root = NewFixture();
        try
        {
            var file = Path.Combine(root, "writer-still-active.txt");
            File.WriteAllText(file, "KEEP-ACTIVE-WRITER");
            File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-8));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-8));

            using (var held = new FileStream(file, FileMode.Open,
                FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
            {
                Assert.False(WindowsScopedFileDelete.TryDelete(root, file, out var bytes));
                Assert.Equal(0L, bytes);
                Assert.Equal("KEEP-ACTIVE-WRITER", File.ReadAllText(file));
            }
            Assert.Equal("KEEP-ACTIVE-WRITER", File.ReadAllText(file));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string NewFixture()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-scoped-handle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
