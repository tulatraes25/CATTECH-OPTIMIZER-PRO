using System.Security.Cryptography;
using System.Text;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ReportBundlePublicationStoreTests
{
    private static readonly byte[] Html =
        Encoding.UTF8.GetBytes("<!DOCTYPE html><html>synthetic QA</html>");
    private static readonly byte[] Pdf =
        Encoding.ASCII.GetBytes("%PDF-1.4\nsynthetic\n%%EOF\n");

    [Fact]
    public async Task ClientFreeHtmlBundle_IsCommittedAsOneDirectoryWithValidManifest()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var id = await store.PublishClientFreeAsync(Html);
            var read = await store.InspectAsync(id);
            Assert.Equal(ReportBundleInspectionStatus.Current, read.Status);
            Assert.Equal(id, read.BundleId);
            Assert.False(read.HasPdf);
            var folder = Committed(root, id);
            Assert.Equal(2, Directory.GetFiles(folder).Length);
            Assert.Equal(Html, await File.ReadAllBytesAsync(
                Path.Combine(folder, "report.html")));
            var manifest = await File.ReadAllTextAsync(
                Path.Combine(folder, "manifest.json"));
            Assert.Contains(Convert.ToHexString(SHA256.HashData(Html)), manifest);
            Assert.Contains("ClientFree", manifest);
            Assert.DoesNotContain("ServiceCaseId", manifest,
                StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.GetDirectories(BundleRoot(root),
                ".pending-*"));
            Assert.False(Directory.Exists(Path.Combine(root, "reports", "html")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task HtmlAndPdfBundle_BothHashesAreRechecked()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var id = await store.PublishClientFreeAsync(Html, Pdf);
            Assert.Equal(ReportBundleInspectionStatus.Current,
                (await store.InspectAsync(id)).Status);
            Assert.True((await store.InspectAsync(id)).HasPdf);
            var pdfPath = Path.Combine(Committed(root, id), "report.pdf");
            await File.WriteAllBytesAsync(pdfPath,
                Encoding.ASCII.GetBytes("%PDF-1.4\nTAMPERED\n%%EOF\n"));
            Assert.Equal(ReportBundleInspectionStatus.Corrupt,
                (await store.InspectAsync(id)).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ChangedHtmlOrUnknownExtraFile_IsNotTrusted()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var id = await store.PublishClientFreeAsync(Html);
            var file = Path.Combine(Committed(root, id), "report.html");
            await File.WriteAllTextAsync(file, "<html>modified</html>");
            Assert.Equal(ReportBundleInspectionStatus.Corrupt,
                (await store.InspectAsync(id)).Status);

            await File.WriteAllBytesAsync(file, Html);
            await File.WriteAllTextAsync(Path.Combine(Committed(root, id),
                "unexpected.txt"), "synthetic");
            Assert.Equal(ReportBundleInspectionStatus.Corrupt,
                (await store.InspectAsync(id)).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"extra\":\"ignored\"}")]
    [InlineData("{}")]
    public async Task MalformedDuplicateOrUnknownManifest_FailsClosed(string json)
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var id = await store.PublishClientFreeAsync(Html);
            await File.WriteAllTextAsync(Path.Combine(Committed(root, id),
                "manifest.json"), json);
            Assert.Equal(ReportBundleInspectionStatus.Corrupt,
                (await store.InspectAsync(id)).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OrphanPendingOrMissingBundle_AreNotPublished()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            var id = Guid.NewGuid();
            var before = await store.InspectAsync(id);
            Assert.Equal(ReportBundleInspectionStatus.Absent, before.Status);
            Assert.False(Directory.Exists(BundleRoot(root)));

            Directory.CreateDirectory(Path.Combine(BundleRoot(root),
                ".pending-" + id.ToString("N")));
            await File.WriteAllBytesAsync(Path.Combine(BundleRoot(root),
                ".pending-" + id.ToString("N"), "report.html"), Html);
            Assert.Equal(ReportBundleInspectionStatus.Absent,
                (await store.InspectAsync(id)).Status);
            Assert.True(Directory.Exists(Path.Combine(BundleRoot(root),
                ".pending-" + id.ToString("N"))));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectedInputOrPrecancel_NeverCreatesPublicationDirectory()
    {
        var root = NewRoot();
        try
        {
            var store = new ReportBundlePublicationStore(root);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                store.PublishClientFreeAsync(ReadOnlyMemory<byte>.Empty));
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.PublishClientFreeAsync(Html, cancellationToken: cancel.Token));
            Assert.False(Directory.Exists(BundleRoot(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "cattech-c2d4a-bundle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string BundleRoot(string root) =>
        Path.Combine(root, "reports", "bundles");

    private static string Committed(string root, Guid id) =>
        Path.Combine(BundleRoot(root), "bundle-" + id.ToString("N"));
}
