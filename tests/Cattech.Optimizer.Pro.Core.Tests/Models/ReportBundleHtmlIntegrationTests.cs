using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Real HTML rendering + bundle checksum verification, entirely synthetic.
/// Not a PDF, case-attribution or WPF acceptance test.
/// </summary>
public sealed class ReportBundleHtmlIntegrationTests
{
    [Fact]
    public async Task RealClientFreeHtml_RendersToCommittedChecksumBundle()
    {
        var root = NewRoot();
        try
        {
            var html = new HtmlReportService(root);
            var bundles = new ReportBundlePublicationStore(root);
            var options = ClientFree();
            options.IncludeDiagnostic = true;
            options.DiagnosticReport = new DiagnosticReport
            {
                Id = "ABCDEF12", OsName = "WINDOWS_SYNTHETIC_QA"
            };
            var bundleId = await html.GenerateClientFreeBundleAsync(options, bundles);

            var inspection = await bundles.InspectAsync(bundleId);
            Assert.Equal(ReportBundleInspectionStatus.Current, inspection.Status);
            Assert.False(inspection.HasPdf);
            var storedHtml = Path.Combine(root, "reports", "bundles",
                "bundle-" + bundleId.ToString("N"), "report.html");
            var text = await File.ReadAllTextAsync(storedHtml);
            Assert.Contains("WINDOWS_SYNTHETIC_QA", text);
            Assert.DoesNotContain("SYNTHETIC_CUSTOMER_PII", text);
            Assert.False(Directory.Exists(Path.Combine(root, "reports", "html")));
            Assert.False(Directory.Exists(Path.Combine(root, "reports", "pdf")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClientSectionOrHiddenClientReference_NeverCreatesBundle(
        bool includeClient)
    {
        var root = NewRoot();
        try
        {
            var options = ClientFree();
            options.IncludeClient = includeClient;
            options.ServiceReport = new ServiceReport
            {
                Client = new ClientInfo { Name = "SYNTHETIC_CUSTOMER_PII" },
                ServiceCaseId =
                    Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa")
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new HtmlReportService(root).GenerateClientFreeBundleAsync(
                    options, new ReportBundlePublicationStore(root)));
            Assert.False(Directory.Exists(Path.Combine(
                root, "reports", "bundles")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EnabledClientSectionWithNoCase_AlsoFailsBeforeWriting()
    {
        var root = NewRoot();
        try
        {
            var options = ClientFree();
            options.IncludeClient = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new HtmlReportService(root).GenerateClientFreeBundleAsync(
                    options, new ReportBundlePublicationStore(root)));
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancelledHtmlBundleNeverStartsPublication()
    {
        var root = NewRoot();
        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new HtmlReportService(root).GenerateClientFreeBundleAsync(
                    ClientFree(), new ReportBundlePublicationStore(root),
                    cts.Token));
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static ReportGenerationOptions ClientFree() => new()
    {
        IncludeCompany = false, IncludeClient = false,
        IncludeDiagnostic = false, IncludeStartup = false,
        IncludeCleanup = false, IncludeRestorePoint = false,
        IncludeVisualOptimization = false, IncludeSmart = false,
        IncludeSmartTests = false, IncludeRecommendations = false
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2d4b-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
