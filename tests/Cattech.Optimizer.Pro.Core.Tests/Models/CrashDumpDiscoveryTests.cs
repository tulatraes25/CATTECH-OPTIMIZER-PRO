using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class CrashDumpDiscoveryTests
{
    private sealed class FakeEnumerator : ICrashDumpFileEnumerator
    {
        private readonly Func<IEnumerable<CrashDumpEntry>> _produce;

        public FakeEnumerator(params CrashDumpEntry[] entries)
            : this(() => entries)
        {
        }

        public FakeEnumerator(Func<IEnumerable<CrashDumpEntry>> produce)
        {
            _produce = produce;
        }

        public IEnumerable<CrashDumpEntry> Enumerate(string directory) => _produce();
    }

    private static CrashDumpEntry Dump(string name, int minute)
        => new()
        {
            FileName = name,
            SizeBytes = 512,
            LastModifiedUtc = new DateTimeOffset(2026, 10, 13, 12, minute, 0, TimeSpan.Zero)
        };

    [Fact]
    public void Discover_Empty_IsNotAnErrorOrProofOfNoCrashes()
    {
        var reader = new CrashDumpDiscoveryService(new FakeEnumerator(), @"C:\Windows\Minidump");
        var result = reader.Discover();

        Assert.Equal(CrashDumpDiscoveryStatus.NoDumps, result.Status);
        Assert.Empty(result.Dumps);
        Assert.Equal(0, result.FilesObserved);
        Assert.Contains("no demuestra", result.Message);
    }

    [Fact]
    public void Discover_ReturnsMetadataWithoutFullPaths()
    {
        var reader = new CrashDumpDiscoveryService(
            new FakeEnumerator(Dump("101326-0001.dmp", 2)), @"C:\Windows\Minidump");
        var result = reader.Discover();

        Assert.Equal(CrashDumpDiscoveryStatus.Found, result.Status);
        Assert.Single(result.Dumps);
        Assert.Equal("101326-0001.dmp", result.Dumps[0].FileName);
        Assert.Equal(512L, result.Dumps[0].SizeBytes);
        Assert.DoesNotContain(@"C:\", result.Dumps[0].FileName);
    }

    [Fact]
    public void Discover_BoundedQueueRetainsNewestNotFirst()
    {
        var reader = new CrashDumpDiscoveryService(
            new FakeEnumerator(
                Dump("old.dmp", 1), Dump("middle.dmp", 5),
                Dump("recent.dmp", 9), Dump("not-dump.txt", 10)),
            @"C:\Windows\Minidump",
            maxDumps: 2);

        var result = reader.Discover();

        Assert.Equal(CrashDumpDiscoveryStatus.Found, result.Status);
        Assert.Equal(3, result.FilesObserved);
        Assert.True(result.IsTruncated);
        Assert.Equal(new[] { "recent.dmp", "middle.dmp" },
            result.Dumps.Select(x => x.FileName).ToArray());
    }

    [Fact]
    public void Discover_MissingDirectory_IsDistinguishedFromNoDumps()
    {
        var reader = new CrashDumpDiscoveryService(
            new FakeEnumerator(() => throw new DirectoryNotFoundException()), "missing");

        var result = reader.Discover();

        Assert.Equal(CrashDumpDiscoveryStatus.DirectoryMissing, result.Status);
        Assert.Empty(result.Dumps);
    }

    [Fact]
    public void Discover_Denied_IsDistinguishedFromNoDumps()
    {
        var reader = new CrashDumpDiscoveryService(
            new FakeEnumerator(() => throw new UnauthorizedAccessException()), "denied");

        var result = reader.Discover();

        Assert.Equal(CrashDumpDiscoveryStatus.AccessDenied, result.Status);
        Assert.Empty(result.Dumps);
    }

    [Fact]
    public void Discover_IoError_DoesNotReturnMisleadingPartialList()
    {
        var reader = new CrashDumpDiscoveryService(
            new FakeEnumerator(() => throw new IOException("Sensitive absolute path")), "io");

        var result = reader.Discover();

        Assert.Equal(CrashDumpDiscoveryStatus.ReadError, result.Status);
        Assert.Empty(result.Dumps);
        Assert.DoesNotContain("Sensitive", result.Message);
    }

    [Fact]
    public void Discover_RejectsInvalidExtension()
    {
        var reader = new CrashDumpDiscoveryService(
            new FakeEnumerator(Dump("something.txt", 1)), "fake");
        Assert.Equal(CrashDumpDiscoveryStatus.NoDumps, reader.Discover().Status);
    }

    [Fact]
    public void Discover_InvalidLimit_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CrashDumpDiscoveryService(new FakeEnumerator(), "fake", maxDumps: 0));
    }

    [Fact]
    public void Discover_RealFileSystemScan_IsReadOnly()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cattech-crash-qa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var dump = Path.Combine(dir, "synthetic.dmp");
            var ignored = Path.Combine(dir, "ignored.txt");
            var bytes = new byte[] { 11, 22, 33, 44 };
            File.WriteAllBytes(dump, bytes);
            File.WriteAllText(ignored, "not-a-dump");

            var reader = new CrashDumpDiscoveryService(
                new FileSystemCrashDumpFileEnumerator(), dir);
            var result = reader.Discover();

            Assert.Equal(CrashDumpDiscoveryStatus.Found, result.Status);
            Assert.Single(result.Dumps);
            Assert.Equal("synthetic.dmp", result.Dumps[0].FileName);
            Assert.Equal(bytes.LongLength, result.Dumps[0].SizeBytes);
            Assert.Equal(bytes, File.ReadAllBytes(dump));
            Assert.True(File.Exists(ignored));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
