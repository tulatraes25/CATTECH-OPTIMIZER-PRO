using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// C2-D preflight: current or revoked on-disk review receipts MUST NOT
/// implicitly bypass the P0 named-client report attribution boundary.
/// All sources and customer names here are synthetic.
/// </summary>
public sealed class ReportBindingExportBoundaryTests
{
    private static readonly Guid CaseA =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JournalReceiptWithoutExportAuthorization_CannotAttributeDiagnostics(
        bool revoked)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2d-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var folder = Path.Combine(root, "data", "diagnostics");
            Directory.CreateDirectory(folder);
            const string sourceJson =
                "{\"id\":\"ABCDEF12\",\"osName\":\"PRIVATE_SYNTHETIC_SOURCE\"}";
            var evidence = Path.Combine(folder, "synthetic.json");
            await File.WriteAllTextAsync(evidence, sourceJson);

            var journal = new SourceBindingReviewJournal(root);
            var accepted = await journal.RecordReviewedAsync(
                CaseA, "Diagnostic", "diagnostics/synthetic.json",
                "TechQA", explicitConsent: true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, accepted.Status);
            Assert.NotNull(accepted.BindingId);
            if (revoked)
            {
                var disabled = await journal.RevokeAsync(
                    accepted.BindingId!.Value, "TechQA", explicitConsent: true);
                Assert.Equal(SourceBindingJournalStatus.Revoked, disabled.Status);
            }

            var options = new ReportGenerationOptions
            {
                IncludeClient = true,
                ServiceReport = new ServiceReport
                {
                    ServiceCaseId = CaseA,
                    Client = new ClientInfo { Name = "Synthetic customer" }
                },
                IncludeDiagnostic = true,
                DiagnosticReport = new DiagnosticReport
                {
                    ServiceCaseId = CaseA,
                    Id = "ABCDEF12",
                    OsName = "PRIVATE_SYNTHETIC_SOURCE"
                },
                IncludeCompany = false, IncludeStartup = false,
                IncludeCleanup = false, IncludeRestorePoint = false,
                IncludeVisualOptimization = false, IncludeSmart = false,
                IncludeSmartTests = false, IncludeRecommendations = false
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new HtmlReportService(root).GenerateHtmlReportAsync(options));
            Assert.False(Directory.Exists(Path.Combine(root, "reports", "html")));
            Assert.Equal(sourceJson, await File.ReadAllTextAsync(evidence));
            Assert.Single(Directory.GetFiles(Path.Combine(
                root, "data", "source-binding-journal"), "binding-*.json"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
