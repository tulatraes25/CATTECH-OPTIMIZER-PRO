using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>All options are synthetic; staging does not grant source binding.</summary>
public sealed class ReportHtmlPublicationCheckTests
{
    private static ReportGenerationOptions SafeOptions() => new()
    {
        IncludeCompany = false, IncludeClient = false,
        IncludeDiagnostic = false, IncludeStartup = false,
        IncludeSmart = false, IncludeSmartTests = false,
        IncludeCleanup = false, IncludeRestorePoint = false,
        IncludeVisualOptimization = false, IncludeRecommendations = false
    };

    [Fact]
    public async Task RejectedLastMomentValidation_DoesNotPublishHtmlOrTemp()
    {
        var root = NewRoot();
        try
        {
            var called = 0;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new HtmlReportService(root).GenerateHtmlWithPublicationCheckAsync(
                    SafeOptions(), _ =>
                    {
                        called++;
                        return Task.FromResult(false);
                    }));
            Assert.Equal(1, called);
            AssertNoPublishedHtml(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancelledPrepublication_DoesNotPublishHtmlOrTemp()
    {
        var root = NewRoot();
        try
        {
            using var cancelled = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new HtmlReportService(root).GenerateHtmlWithPublicationCheckAsync(
                    SafeOptions(), _ =>
                    {
                        cancelled.Cancel();
                        return Task.FromResult(true);
                    }, cancelled.Token));
            AssertNoPublishedHtml(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SuccessfulLastCheck_CommitsOneHtmlWithoutStagingRemnants()
    {
        var root = NewRoot();
        try
        {
            var file = await new HtmlReportService(root)
                .GenerateHtmlWithPublicationCheckAsync(
                    SafeOptions(), _ => Task.FromResult(true));
            Assert.True(File.Exists(file));
            Assert.EndsWith(".html", file, StringComparison.OrdinalIgnoreCase);
            var folder = Path.Combine(root, "reports", "html");
            Assert.Single(Directory.GetFiles(folder, "*.html"));
            Assert.Empty(Directory.GetFiles(folder, ".cattech-report-*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PublicationCheckNeverBypassesNamedClientSourceRejection()
    {
        var root = NewRoot();
        try
        {
            var options = SafeOptions();
            options.IncludeClient = true;
            options.IncludeDiagnostic = true;
            options.ServiceReport = new ServiceReport
            {
                ServiceCaseId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
                Client = new ClientInfo { Name = "PRIVATE_QA_CUSTOMER" }
            };
            options.DiagnosticReport = new DiagnosticReport
            {
                ServiceCaseId = options.ServiceReport.ServiceCaseId,
                Id = "ABCDEF12", OsName = "SYNTHETIC"
            };
            var callbackCalled = false;
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new HtmlReportService(root).GenerateHtmlWithPublicationCheckAsync(
                    options, _ =>
                    {
                        callbackCalled = true;
                        return Task.FromResult(true);
                    }));
            Assert.False(callbackCalled);
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static void AssertNoPublishedHtml(string root)
    {
        var path = Path.Combine(root, "reports", "html");
        Assert.True(Directory.Exists(path));
        Assert.Empty(Directory.GetFiles(path, "*.html"));
        Assert.Empty(Directory.GetFiles(path, ".cattech-report-*.tmp"));
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2d3a-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
