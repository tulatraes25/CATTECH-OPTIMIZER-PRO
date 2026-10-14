using Cattech.Optimizer.Pro.Infrastructure.Cleanup;

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

    private static string NewFixture()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-scoped-handle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
