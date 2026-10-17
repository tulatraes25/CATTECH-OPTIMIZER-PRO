using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>Only disposable synthetic client and diagnostic fixtures.</summary>
public sealed class ReportSourceReviewCoordinatorTests
{
    private static readonly Guid CaseA =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Fact]
    public async Task RealSavedCaseAndUniqueSource_RequireExplicitConfirmation()
    {
        var root = NewRoot();
        try
        {
            var client = NewClient();
            var reports = new JsonServiceReportService(root);
            await reports.SaveReportAsync(client);
            await WriteDiagnosticAsync(root, "{\"id\":\"ABCDEF12\",\"synthetic\":true}");
            var coordinator = new ReportSourceReviewCoordinator(reports, root);
            var prepared = await coordinator.PrepareAsync(
                client, "Diagnostic", "ABCDEF12");
            Assert.Equal(SourceReviewCoordinationStatus.Prepared, prepared.Status);
            Assert.NotNull(prepared.Preview);

            var refused = await coordinator.ConfirmAsync(prepared.Preview,
                "TechQA", false);
            Assert.Equal(SourceReviewCoordinationStatus.ReviewRequired,
                refused.Status);
            Assert.False(Directory.Exists(JournalDir(root)));

            var accepted = await coordinator.ConfirmAsync(prepared.Preview,
                "TechQA", true);
            Assert.Equal(SourceReviewCoordinationStatus.Recorded, accepted.Status);
            Assert.NotNull(accepted.BindingId);
            Assert.Single(Directory.GetFiles(JournalDir(root), "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ChangedClientOrSourceAfterPreview_FailsWithoutBinding()
    {
        var root = NewRoot();
        try
        {
            var selected = NewClient();
            var reports = new JsonServiceReportService(root);
            await reports.SaveReportAsync(selected);
            var source = await WriteDiagnosticAsync(root,
                "{\"id\":\"ABCDEF12\",\"value\":1}");
            var coordinator = new ReportSourceReviewCoordinator(reports, root);
            var prepared = await coordinator.PrepareAsync(
                selected, "Diagnostic", "ABCDEF12");
            Assert.Equal(SourceReviewCoordinationStatus.Prepared, prepared.Status);
            await File.WriteAllTextAsync(source,
                "{\"id\":\"ABCDEF12\",\"value\":2}");
            Assert.Equal(SourceReviewCoordinationStatus.SourceChanged,
                (await coordinator.ConfirmAsync(prepared.Preview,
                    "TechQA", true)).Status);
            Assert.False(Directory.Exists(JournalDir(root)));

            await File.WriteAllTextAsync(source,
                "{\"id\":\"ABCDEF12\",\"value\":1}");
            var modifiedCase = NewClient();
            modifiedCase.Client.Name = "DIFFERENT_SYNTHETIC";
            await reports.SaveReportAsync(modifiedCase);
            Assert.Equal(SourceReviewCoordinationStatus.CaseUnavailable,
                (await coordinator.ConfirmAsync(prepared.Preview,
                    "TechQA", true)).Status);
            Assert.False(Directory.Exists(JournalDir(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExpiredReviewOrMissingCase_NeverProducesReceipt()
    {
        var root = NewRoot();
        try
        {
            var selected = NewClient();
            var reports = new JsonServiceReportService(root);
            await reports.SaveReportAsync(selected);
            await WriteDiagnosticAsync(root, "{\"id\":\"ABCDEF12\"}");
            var now = new DateTimeOffset(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
            var coordinator = new ReportSourceReviewCoordinator(reports, root,
                () => now);
            var prepared = await coordinator.PrepareAsync(
                selected, "Diagnostic", "ABCDEF12");
            Assert.Equal(SourceReviewCoordinationStatus.Prepared, prepared.Status);
            now = now.AddMinutes(6);
            Assert.Equal(SourceReviewCoordinationStatus.ExpiredPreview,
                (await coordinator.ConfirmAsync(prepared.Preview,
                    "TechQA", true)).Status);
            Assert.False(Directory.Exists(JournalDir(root)));

            selected.ServiceCaseId = null;
            Assert.Equal(SourceReviewCoordinationStatus.CaseUnavailable,
                (await coordinator.PrepareAsync(selected, "Diagnostic",
                    "ABCDEF12")).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DuplicateSourceCannotBeOfferedForReview()
    {
        var root = NewRoot();
        try
        {
            var selected = NewClient();
            var reports = new JsonServiceReportService(root);
            await reports.SaveReportAsync(selected);
            await WriteDiagnosticAsync(root, "{\"id\":\"ABCDEF12\"}");
            var folder = Path.Combine(root, "data", "diagnostics");
            await File.WriteAllTextAsync(Path.Combine(folder,
                "diagnostic-second.json"), "{\"id\":\"ABCDEF12\"}");
            var result = await new ReportSourceReviewCoordinator(reports, root)
                .PrepareAsync(selected, "Diagnostic", "ABCDEF12");
            Assert.Equal(SourceReviewCoordinationStatus.AmbiguousSource, result.Status);
            Assert.Null(result.Preview);
            Assert.False(Directory.Exists(JournalDir(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    private static ServiceReport NewClient() => new()
    {
        Id = "11111111111141118111111111111111",
        ServiceCaseId = CaseA,
        Client = new ClientInfo { Name = "SYNTHETIC CLIENT" }
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2c0-coordinator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string JournalDir(string root) =>
        Path.Combine(root, "data", "source-binding-journal");

    private static async Task<string> WriteDiagnosticAsync(
        string root, string json)
    {
        var folder = Path.Combine(root, "data", "diagnostics");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "diagnostic-one.json");
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
