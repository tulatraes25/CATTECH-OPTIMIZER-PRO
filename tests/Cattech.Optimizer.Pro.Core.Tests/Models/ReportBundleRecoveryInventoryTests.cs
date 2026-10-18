using System.Text;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ReportBundleRecoveryInventoryTests
{
    private static readonly byte[] Html =
        Encoding.UTF8.GetBytes("<html>synthetic qa</html>");

    [Fact]
    public async Task MissingStoreInventory_IsReadOnlyAndNeverCreatesFolders()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var result = await store.InspectInventoryAsync();
            Assert.Equal(ReportBundleInventoryStatus.Absent, result.Status);
            Assert.Equal(0, result.VerifiedBundles);
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MixedCommittedDamagedOrphanedAndUnexpectedEntries_AreCountedWithoutCleanup()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var healthy = await store.PublishClientFreeAsync(Html);
            var damaged = await store.PublishClientFreeAsync(Html);
            var bundles = BundleRoot(root);
            var badFile = Path.Combine(bundles,
                "bundle-" + damaged.ToString("N"), "report.html");
            await File.WriteAllTextAsync(badFile, "CORRUPTED");
            var pending = Path.Combine(bundles,
                ".pending-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pending);
            await File.WriteAllTextAsync(Path.Combine(pending, "report.html"),
                "INCOMPLETE");
            await File.WriteAllTextAsync(Path.Combine(bundles, "notes.txt"),
                "unexpected QA metadata");
            Directory.CreateDirectory(Path.Combine(bundles, ".pending-BAD-ID"));

            var before = Directory.GetFileSystemEntries(bundles)
                .OrderBy(x => x).ToArray();
            var result = await store.InspectInventoryAsync();
            var after = Directory.GetFileSystemEntries(bundles)
                .OrderBy(x => x).ToArray();

            Assert.Equal(ReportBundleInventoryStatus.Ready, result.Status);
            Assert.Equal(1, result.VerifiedBundles);
            Assert.Equal(1, result.CorruptBundles);
            Assert.Equal(1, result.PendingBundles);
            Assert.Equal(2, result.UnexpectedEntries);
            Assert.Equal(before, after);
            Assert.True(Directory.Exists(pending));
            Assert.Equal(ReportBundleInspectionStatus.Current,
                (await store.InspectAsync(healthy)).Status);
            Assert.Equal(ReportBundleInspectionStatus.Corrupt,
                (await store.InspectAsync(damaged)).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ScanLimit_FailsClosedRatherThanReturningPartialCleanCounts()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            await store.PublishClientFreeAsync(Html);
            await File.WriteAllTextAsync(Path.Combine(BundleRoot(root),
                "stray-1.txt"), "QA");
            await File.WriteAllTextAsync(Path.Combine(BundleRoot(root),
                "stray-2.txt"), "QA");
            var limited = await store.InspectInventoryAsync(maxEntries: 2);
            Assert.Equal(ReportBundleInventoryStatus.TooMany, limited.Status);
            Assert.Equal(0, limited.VerifiedBundles);
            Assert.Equal(0, limited.CorruptBundles);
            Assert.Equal(0, limited.PendingBundles);
            Assert.Equal(0, limited.UnexpectedEntries);
            Assert.Equal(3, Directory.GetFileSystemEntries(
                BundleRoot(root)).Length);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InventoryCancellationNeverChangesStore()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            await store.PublishClientFreeAsync(Html);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.InspectInventoryAsync(cancellationToken: cts.Token));
            Assert.Single(Directory.GetDirectories(BundleRoot(root),
                "bundle-*"));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2d4c-inventory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string BundleRoot(string root) =>
        Path.Combine(root, "reports", "bundles");
}
