using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// End-to-end synthetic stage publication tests. They DO NOT enable named
/// customer diagnostics, PDF or report index integration.
/// </summary>
public sealed class ReportHtmlStagingIntegrationTests
{
    private static readonly Guid CaseId =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ClientFreeHtmlStage_PublishesUnderExclusiveCurrentJournalLease()
    {
        var root = NewRoot();
        try
        {
            var journal = new SourceBindingReviewJournal(root);
            await WriteSourceAsync(root, "{\"id\":\"ABCDEF12\",\"state\":1}");
            var receipt = await journal.RecordReviewedAsync(CaseId,
                "Diagnostic", "diagnostics/diagnostic-qa.json", "TechQA", true);
            var binding = Assert.IsType<Guid>(receipt.BindingId);
            var service = new HtmlReportService(root);
            HtmlReportService.StagedHtmlReport? stage = null;

            var outcome = await journal.StageAndPublishWhileCurrentAsync(
                binding, CaseId, "Diagnostic", "diagnostics/diagnostic-qa.json",
                async ct =>
                {
                    stage = await service.StageHtmlForPublicationAsync(
                        ClientFreeOptions(), ct);
                    return stage;
                });
            Assert.Equal(SourceBindingReadStatus.CurrentSnapshot, outcome.Status);
            Assert.NotNull(stage);
            Assert.True(File.Exists(stage.OutputPath));
            Assert.Contains("SinCliente", Path.GetFileName(stage.OutputPath));
            var folder = Path.Combine(root, "reports", "html");
            Assert.Single(Directory.GetFiles(folder, "*.html"));
            Assert.Empty(Directory.GetFiles(folder, ".cattech-report-*.tmp"));
            Assert.Empty(Directory.GetFiles(folder, "reports-info.json"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task SourceChangeAfterHtmlStaging_DiscardsTempAndNeverPublishes()
    {
        var root = NewRoot();
        try
        {
            var original = await WriteSourceAsync(root,
                "{\"id\":\"ABCDEF12\",\"state\":1}");
            var journal = new SourceBindingReviewJournal(root);
            var receipt = await journal.RecordReviewedAsync(CaseId,
                "Diagnostic", "diagnostics/diagnostic-qa.json", "TechQA", true);
            var binding = Assert.IsType<Guid>(receipt.BindingId);
            var html = new HtmlReportService(root);
            var outcome = await journal.StageAndPublishWhileCurrentAsync(
                binding, CaseId, "Diagnostic", "diagnostics/diagnostic-qa.json",
                async ct =>
                {
                    var staged = await html.StageHtmlForPublicationAsync(
                        ClientFreeOptions(), ct);
                    await File.WriteAllTextAsync(original,
                        "{\"id\":\"ABCDEF12\",\"state\":2}");
                    return staged;
                });
            Assert.Equal(SourceBindingReadStatus.SourceChanged, outcome.Status);
            var folder = Path.Combine(root, "reports", "html");
            Assert.Empty(Directory.GetFiles(folder, "*.html"));
            Assert.Empty(Directory.GetFiles(folder, ".cattech-report-*.tmp"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task RawNamedClientDiagnostic_CannotBeStagedEvenWithEqualGuid()
    {
        var root = NewRoot();
        try
        {
            var options = ClientFreeOptions();
            options.IncludeClient = true;
            options.IncludeDiagnostic = true;
            options.ServiceReport = new ServiceReport
            {
                ServiceCaseId = CaseId,
                Client = new ClientInfo { Name = "PRIVATE_SYNTHETIC" }
            };
            options.DiagnosticReport = new DiagnosticReport
            {
                Id = "ABCDEF12",
                ServiceCaseId = CaseId
            };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new HtmlReportService(root).StageHtmlForPublicationAsync(options));
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static ReportGenerationOptions ClientFreeOptions() => new()
    {
        IncludeCompany = false, IncludeClient = false,
        IncludeDiagnostic = false, IncludeStartup = false,
        IncludeSmart = false, IncludeSmartTests = false,
        IncludeCleanup = false, IncludeRestorePoint = false,
        IncludeVisualOptimization = false, IncludeRecommendations = false
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2d3d-html-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> WriteSourceAsync(string root, string data)
    {
        var path = Path.Combine(root, "data", "diagnostics", "diagnostic-qa.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, data);
        return path;
    }
}
