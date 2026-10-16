using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DumpCheckFileGuardTests
{
    [Theory]
    [InlineData(@"C:\Windows\Minidump", @"\\?\C:\Windows\Minidump", true)]
    [InlineData(@"C:\Windows\Minidump", @"C:\Users\Someone", false)]
    [InlineData(@"C:\Windows\Minidump", @"\\?\D:\Other\Minidump", false)]
    [InlineData(@"C:\Windows\Minidump", @"\\?\C:\Windows\MinidumpBackup", false)]
    public void CanonicalPathComparison_DetectsRedirects(
        string expected, string actual, bool same)
    {
        Assert.Equal(same, WindowsDumpCheckFileGuard.SamePath(expected, actual));
    }

    [Fact]
    public void RealWindowsLease_BlocksReplacementAndWriteUntilDisposed()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test must run in Windows CI.");

        var directory = Path.Combine(Path.GetTempPath(), "cattech-dump-lease-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var dump = Path.Combine(directory, "synthetic.dmp");
        File.WriteAllBytes(dump, new byte[] { 1, 2, 3, 4 });

        try
        {
            var guard = new WindowsDumpCheckFileGuard();
            using (var lease = guard.TryAcquire(directory, dump))
            {
                Assert.NotNull(lease);
                var writeError = Record.Exception(() =>
                {
                    using var write = new FileStream(dump, FileMode.Open,
                        FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                });
                Assert.True(writeError is IOException or UnauthorizedAccessException,
                    "A read-only lease must deny concurrent writers.");
                Assert.True(File.Exists(dump));
            }

            // Releasing the lease restores normal access.
            using var writable = new FileStream(dump, FileMode.Open, FileAccess.Write,
                FileShare.ReadWrite);
            Assert.True(writable.CanWrite);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RealWindowsGuard_RejectsFileOutsideAuthorizedDirectory()
    {
        Assert.True(OperatingSystem.IsWindows(), "This test must run in Windows CI.");

        var first = Path.Combine(Path.GetTempPath(), "cattech-guard-a-" +
            Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), "cattech-guard-b-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        var dump = Path.Combine(second, "synthetic.dmp");
        File.WriteAllBytes(dump, new byte[] { 1 });
        try
        {
            var guard = new WindowsDumpCheckFileGuard();
            Assert.Null(guard.TryAcquire(first, dump));
        }
        finally
        {
            Directory.Delete(first);
            Directory.Delete(second, recursive: true);
        }
    }
}
