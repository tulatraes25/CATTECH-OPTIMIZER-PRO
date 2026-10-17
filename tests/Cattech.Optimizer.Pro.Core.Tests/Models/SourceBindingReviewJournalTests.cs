using System.Text.Json;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Synthetic-only local journal tests. No UI, clients, diagnostics or
/// real hardware commands are touched by this pre-release C2-B2-A slice.
/// </summary>
public sealed class SourceBindingReviewJournalTests
{
    private static readonly Guid CaseA =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB =
        Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    [Theory]
    [InlineData(false, "TechQA")]
    [InlineData(true, "")]
    [InlineData(true, "user@example.com")]
    [InlineData(true, "../outside")]
    public async Task AbsentReviewOrUnsafeIdentity_CannotWriteJournal(
        bool consent, string reviewer)
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\"}");
            var result = await new SourceBindingReviewJournal(root)
                .RecordReviewedAsync(CaseA, "Diagnostic",
                    "diagnostics/source.json", reviewer, consent);
            Assert.Equal(SourceBindingJournalStatus.ReviewRequired, result.Status);
            Assert.Null(result.BindingId);
            Assert.False(Directory.Exists(JournalDirectory(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ValidSyntheticReview_PublishesOnceWithoutModifyingEvidence()
    {
        var root = NewRoot();
        try
        {
            const string original = "{\"id\":\"ABCDEF12\",\"device\":\"PRIVATE_SYNTHETIC\"}";
            var source = await WriteSourceAsync(root,
                "diagnostics/source.json", original);
            var journal = new SourceBindingReviewJournal(root);
            var result = await journal.RecordReviewedAsync(CaseA,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, result.Status);
            Assert.NotNull(result.BindingId);
            Assert.Equal(original, await File.ReadAllTextAsync(source));
            var records = Directory.GetFiles(JournalDirectory(root),
                "binding-*.json");
            var record = Assert.Single(records);
            Assert.Contains(result.BindingId!.Value.ToString("N"),
                Path.GetFileName(record));
            var contents = await File.ReadAllTextAsync(record);
            Assert.Contains("ABCDEF12", contents);
            Assert.DoesNotContain("PRIVATE_SYNTHETIC", contents);
            Assert.True(File.Exists(Path.Combine(JournalDirectory(root),
                ".cattech-source-binding.lock")));
            Assert.Empty(Directory.GetFiles(JournalDirectory(root),
                ".cattech-binding-*.tmp"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task DuplicateSourceInAnotherCase_RefusesSecondBinding()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\"}");
            var journal = new SourceBindingReviewJournal(root);
            var first = await journal.RecordReviewedAsync(CaseA,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            var second = await journal.RecordReviewedAsync(CaseB,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, first.Status);
            Assert.Equal(SourceBindingJournalStatus.ConflictingClaim, second.Status);
            Assert.Null(second.BindingId);
            Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CorruptPriorAudit_PreventsNewClaimAndKeepsSourceFilesIntact()
    {
        var root = NewRoot();
        try
        {
            var sourceA = await WriteSourceAsync(root,
                "diagnostics/source-a.json", "{\"id\":\"ABCD1234\"}");
            var sourceB = await WriteSourceAsync(root,
                "diagnostics/source-b.json", "{\"id\":\"DEADBEEF\"}");
            var journal = new SourceBindingReviewJournal(root);
            Assert.Equal(SourceBindingJournalStatus.Recorded,
                (await journal.RecordReviewedAsync(CaseA,
                    "Diagnostic", "diagnostics/source-a.json",
                    "TechQA", true)).Status);
            var recorded = Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
            await File.WriteAllTextAsync(recorded,
                "{\"schemaVersion\":1,\"schemaVersion\":1}");
            var result = await journal.RecordReviewedAsync(CaseB,
                "Diagnostic", "diagnostics/source-b.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.InvalidJournal, result.Status);
            Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
            Assert.Equal("{\"id\":\"ABCD1234\"}",
                await File.ReadAllTextAsync(sourceA));
            Assert.Equal("{\"id\":\"DEADBEEF\"}",
                await File.ReadAllTextAsync(sourceB));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TwoIndependentInstances_CannotApproveSameSourceTwice()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "smart-reports/device.json",
                "{\"id\":\"BEEF1234\"}");
            var first = new SourceBindingReviewJournal(root);
            var second = new SourceBindingReviewJournal(root);
            var results = await Task.WhenAll(
                first.RecordReviewedAsync(CaseA, "SmartAnalysis",
                    "smart-reports/device.json", "TechQA", true),
                second.RecordReviewedAsync(CaseB, "SmartAnalysis",
                    "smart-reports/device.json", "TechQB", true));
            Assert.Single(results.Where(r =>
                r.Status == SourceBindingJournalStatus.Recorded));
            Assert.Single(results.Where(r =>
                r.Status == SourceBindingJournalStatus.ConflictingClaim));
            Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidOrMissingSourceCannotCreateReviewJournal()
    {
        var root = NewRoot();
        try
        {
            var result = await new SourceBindingReviewJournal(root)
                .RecordReviewedAsync(CaseA, "Diagnostic",
                    "diagnostics/missing.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.SourceUnavailable,
                result.Status);
            Assert.False(Directory.Exists(JournalDirectory(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2b2-journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string JournalDirectory(string root) =>
        Path.Combine(root, "data", "source-binding-journal");

    private static async Task<string> WriteSourceAsync(
        string root, string relativePath, string content)
    {
        var path = Path.Combine(root, "data",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }
}
