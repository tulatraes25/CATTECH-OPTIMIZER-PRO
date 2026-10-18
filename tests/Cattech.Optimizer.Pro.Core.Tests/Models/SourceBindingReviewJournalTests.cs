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

    [Fact]
    public async Task RevocationIsAppendOnlyAndPreservesOriginalEvidence()
    {
        var root = NewRoot();
        try
        {
            const string raw = "{\"id\":\"ABCDEF12\",\"synthetic\":true}";
            var file = await WriteSourceAsync(root, "diagnostics/source.json", raw);
            var store = new SourceBindingReviewJournal(root);
            var accepted = await store.RecordReviewedAsync(CaseA, "Diagnostic",
                "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, accepted.Status);
            var revoked = await store.RevokeAsync(accepted.BindingId!.Value,
                "TechQB", true);
            Assert.Equal(SourceBindingJournalStatus.Revoked, revoked.Status);
            Assert.Equal(accepted.BindingId, revoked.BindingId);
            Assert.Single(Directory.GetFiles(JournalDirectory(root), "binding-*.json"));
            Assert.Single(Directory.GetFiles(JournalDirectory(root), "revoked-*.json"));
            Assert.Equal(raw, await File.ReadAllTextAsync(file));

            var repeated = await store.RevokeAsync(accepted.BindingId.Value,
                "TechQB", true);
            Assert.Equal(SourceBindingJournalStatus.AlreadyRevoked, repeated.Status);
            Assert.Single(Directory.GetFiles(JournalDirectory(root), "revoked-*.json"));

            // Deliberate fail-closed interim: re-association after revocation
            // needs the future explicit C2-C workflow; no silent reuse.
            var differentCase = await store.RecordReviewedAsync(CaseB, "Diagnostic",
                "diagnostics/source.json", "TechQB", true);
            Assert.Equal(SourceBindingJournalStatus.ConflictingClaim,
                differentCase.Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task UnknownOrUnconfirmedRevocationCannotPublishRecord()
    {
        var root = NewRoot();
        try
        {
            var store = new SourceBindingReviewJournal(root);
            var absent = await store.RevokeAsync(Guid.NewGuid(), "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.UnknownBinding, absent.Status);
            Assert.False(Directory.Exists(JournalDirectory(root)));

            var denied = await store.RevokeAsync(Guid.NewGuid(), "TechQA", false);
            Assert.Equal(SourceBindingJournalStatus.ReviewRequired, denied.Status);
            Assert.False(Directory.Exists(JournalDirectory(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TamperedRevocationPreventsAdditionalSourceReview()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/one.json", "{\"id\":\"ABCDEF12\"}");
            await WriteSourceAsync(root, "diagnostics/two.json", "{\"id\":\"DEADBEEF\"}");
            var store = new SourceBindingReviewJournal(root);
            var first = await store.RecordReviewedAsync(CaseA, "Diagnostic",
                "diagnostics/one.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, first.Status);
            Assert.Equal(SourceBindingJournalStatus.Revoked,
                (await store.RevokeAsync(first.BindingId!.Value,
                    "TechQA", true)).Status);

            var revocation = Assert.Single(Directory.GetFiles(
                JournalDirectory(root), "revoked-*.json"));
            await File.WriteAllTextAsync(revocation,
                "{\"schemaVersion\":1,\"schemaVersion\":1}");
            var next = await store.RecordReviewedAsync(CaseB, "Diagnostic",
                "diagnostics/two.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.InvalidJournal, next.Status);
            Assert.Single(Directory.GetFiles(JournalDirectory(root), "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ConcurrentRevocationsProduceOnlyOneAcceptedEvent()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "smart-reports/disk.json",
                "{\"id\":\"DEADBEEF\"}");
            var first = new SourceBindingReviewJournal(root);
            var accepted = await first.RecordReviewedAsync(CaseA, "SmartAnalysis",
                "smart-reports/disk.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, accepted.Status);
            var second = new SourceBindingReviewJournal(root);
            var results = await Task.WhenAll(
                first.RevokeAsync(accepted.BindingId!.Value, "TechQA", true),
                second.RevokeAsync(accepted.BindingId.Value, "TechQB", true));
            Assert.Single(results.Where(x =>
                x.Status == SourceBindingJournalStatus.Revoked));
            Assert.Single(results.Where(x =>
                x.Status == SourceBindingJournalStatus.AlreadyRevoked));
            Assert.Single(Directory.GetFiles(JournalDirectory(root), "revoked-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CurrentReadOnlyInspection_RequiresExactCaseAndUnalteredBytes()
    {
        var root = NewRoot();
        try
        {
            var source = await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\",\"serial\":\"SYNTHETIC\"}");
            var journal = new SourceBindingReviewJournal(root);
            var recorded = await journal.RecordReviewedAsync(CaseA, "Diagnostic",
                "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, recorded.Status);
            var bindingId = recorded.BindingId!.Value;
            var folder = JournalDirectory(root);
            var before = Directory.GetFiles(folder).OrderBy(x => x).ToArray();

            var matching = await journal.InspectCurrentAsync(bindingId, CaseA,
                "Diagnostic", "diagnostics/source.json");
            Assert.Equal(SourceBindingReadStatus.CurrentSnapshot, matching.Status);
            var foreign = await journal.InspectCurrentAsync(bindingId, CaseB,
                "Diagnostic", "diagnostics/source.json");
            Assert.Equal(SourceBindingReadStatus.CaseMismatch, foreign.Status);
            var wrongKind = await journal.InspectCurrentAsync(bindingId, CaseA,
                "SmartAnalysis", "diagnostics/source.json");
            Assert.Equal(SourceBindingReadStatus.SourceChanged, wrongKind.Status);
            var wrongFile = await journal.InspectCurrentAsync(bindingId, CaseA,
                "Diagnostic", "diagnostics/other.json");
            Assert.Equal(SourceBindingReadStatus.SourceChanged, wrongFile.Status);
            Assert.Equal(before, Directory.GetFiles(folder).OrderBy(x => x).ToArray());

            // The source keeps its ID and any spoofable equipment serial.
            // A byte change must nevertheless revoke current-snapshot status.
            await File.WriteAllTextAsync(source,
                "{\"id\":\"ABCDEF12\",\"serial\":\"SYNTHETIC\",\"changed\":true}");
            var changed = await journal.InspectCurrentAsync(bindingId, CaseA,
                "Diagnostic", "diagnostics/source.json");
            Assert.Equal(SourceBindingReadStatus.SourceChanged, changed.Status);
            Assert.Equal(before, Directory.GetFiles(folder).OrderBy(x => x).ToArray());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RevokedBinding_NeverAppearsCurrentEvenWithUnchangedSource()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "smart-reports/disk.json",
                "{\"id\":\"DEADBEEF\"}");
            var journal = new SourceBindingReviewJournal(root);
            var receipt = await journal.RecordReviewedAsync(CaseA, "SmartAnalysis",
                "smart-reports/disk.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, receipt.Status);
            Assert.Equal(SourceBindingReadStatus.CurrentSnapshot,
                (await journal.InspectCurrentAsync(receipt.BindingId!.Value,
                    CaseA, "SmartAnalysis", "smart-reports/disk.json")).Status);

            Assert.Equal(SourceBindingJournalStatus.Revoked,
                (await journal.RevokeAsync(receipt.BindingId.Value, "TechQA", true)).Status);
            Assert.Equal(SourceBindingReadStatus.Revoked,
                (await journal.InspectCurrentAsync(receipt.BindingId.Value,
                    CaseA, "SmartAnalysis", "smart-reports/disk.json")).Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task UnknownInspection_NeverCreatesDirectoryOrMarker()
    {
        var root = NewRoot();
        try
        {
            var outcome = await new SourceBindingReviewJournal(root)
                .InspectCurrentAsync(Guid.NewGuid(), CaseA,
                    "Diagnostic", "diagnostics/missing.json");
            Assert.Equal(SourceBindingReadStatus.UnknownBinding, outcome.Status);
            Assert.False(Directory.Exists(JournalDirectory(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OrphanedRevocationFailsAllInspectionsAndNewReviews()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/a.json",
                "{\"id\":\"ABCD1234\"}");
            await WriteSourceAsync(root, "diagnostics/b.json",
                "{\"id\":\"DEADBEEF\"}");
            var journal = new SourceBindingReviewJournal(root);
            var receipt = await journal.RecordReviewedAsync(CaseA, "Diagnostic",
                "diagnostics/a.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, receipt.Status);

            var revocation = new SourceBindingRevocationEntry(1, Guid.NewGuid(),
                Guid.NewGuid(), DateTimeOffset.UtcNow, "TechQA");
            var orphanFile = Path.Combine(JournalDirectory(root),
                "revoked-" + revocation.BindingId.ToString("N") + ".json");
            await File.WriteAllTextAsync(orphanFile,
                JsonSerializer.Serialize(revocation, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
            Assert.Equal(SourceBindingReadStatus.InvalidJournal,
                (await journal.InspectCurrentAsync(receipt.BindingId!.Value,
                    CaseA, "Diagnostic", "diagnostics/a.json")).Status);
            Assert.Equal(SourceBindingJournalStatus.InvalidJournal,
                (await journal.RecordReviewedAsync(CaseB, "Diagnostic",
                    "diagnostics/b.json", "TechQA", true)).Status);
            Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BusyJournal_InspectionFailsClosedWithoutChangingReceipts()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/a.json",
                "{\"id\":\"ABCDEF12\"}");
            var journal = new SourceBindingReviewJournal(root);
            var receipt = await journal.RecordReviewedAsync(CaseA, "Diagnostic",
                "diagnostics/a.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, receipt.Status);
            var marker = Path.Combine(JournalDirectory(root),
                ".cattech-source-binding.lock");
            await using var exclusive = new FileStream(marker, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None);
            var result = await journal.InspectCurrentAsync(receipt.BindingId!.Value,
                CaseA, "Diagnostic", "diagnostics/a.json");
            Assert.Equal(SourceBindingReadStatus.InvalidJournal, result.Status);
            Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ExplicitSourceIdReview_RequiresDisplayedDigestAndWritesOnce()
    {
        var root = NewRoot();
        try
        {
            const string json = "{\"id\":\"ABCDEF12\",\"synthetic\":true}";
            await WriteSourceAsync(root, "diagnostics/diagnostic-one.json", json);
            var reader = new ReportSourceFingerprintReader(root);
            var preview = await reader.ResolveUniqueAsync("Diagnostic", "ABCDEF12");
            Assert.Equal(SourceIdResolutionStatus.FoundUnique, preview.Status);
            var journal = new SourceBindingReviewJournal(root);
            var result = await journal.RecordReviewedByIdAsync(CaseA,
                "Diagnostic", "ABCDEF12", preview.Fingerprint!.Sha256,
                "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, result.Status);
            Assert.NotNull(result.BindingId);
            Assert.Equal(SourceBindingReadStatus.CurrentSnapshot,
                (await journal.InspectCurrentAsync(result.BindingId!.Value,
                    CaseA, "Diagnostic", "diagnostics/diagnostic-one.json")).Status);
            Assert.Single(Directory.GetFiles(JournalDirectory(root),
                "binding-*.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ChangedAfterReview_DoesNotGrantNewReceiptOrRewriteEvidence()
    {
        var root = NewRoot();
        try
        {
            var path = await WriteSourceAsync(root,
                "diagnostics/diagnostic-one.json", "{\"id\":\"ABCDEF12\",\"v\":1}");
            var reader = new ReportSourceFingerprintReader(root);
            var preview = await reader.ResolveUniqueAsync("Diagnostic", "ABCDEF12");
            var oldDigest = preview.Fingerprint!.Sha256;
            const string modified = "{\"id\":\"ABCDEF12\",\"v\":2}";
            await File.WriteAllTextAsync(path, modified);
            var journal = new SourceBindingReviewJournal(root);
            var outcome = await journal.RecordReviewedByIdAsync(CaseA,
                "Diagnostic", "ABCDEF12", oldDigest, "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.SourceChangedSinceReview,
                outcome.Status);
            Assert.Null(outcome.BindingId);
            Assert.False(Directory.Exists(JournalDirectory(root)));
            Assert.Equal(modified, await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AmbiguousHistoricalSourceId_NoReviewReceipt()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/diagnostic-a.json",
                "{\"id\":\"ABCDEF12\"}");
            await WriteSourceAsync(root, "diagnostics/diagnostic-b.json",
                "{\"id\":\"ABCDEF12\"}");
            var journal = new SourceBindingReviewJournal(root);
            var result = await journal.RecordReviewedByIdAsync(CaseA,
                "Diagnostic", "ABCDEF12", new string('A', 64), "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.ConflictingClaim, result.Status);
            Assert.False(Directory.Exists(JournalDirectory(root)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RefusedConsentOrInvalidDigestNeverSearchesOrWrites()
    {
        var root = NewRoot();
        try
        {
            var journal = new SourceBindingReviewJournal(root);
            var denied = await journal.RecordReviewedByIdAsync(CaseA,
                "Diagnostic", "ABCDEF12", new string('A', 64),
                "TechQA", false);
            var invalid = await journal.RecordReviewedByIdAsync(CaseA,
                "Diagnostic", "ABCDEF12", "NOT_A_SHA",
                "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.ReviewRequired, denied.Status);
            Assert.Equal(SourceBindingJournalStatus.ReviewRequired, invalid.Status);
            Assert.False(Directory.Exists(Path.Combine(root, "data")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelWhileAnotherProcessHoldsJournalLease_NeverPublishesLateEvent(
        bool revocation)
    {
        var root = NewRoot();
        try
        {
            const string original = "{\"id\":\"ABCDEF12\",\"qa\":true}";
            var source = await WriteSourceAsync(root,
                "diagnostics/source.json", original);
            var journal = new SourceBindingReviewJournal(root);
            Guid bindingId = Guid.Empty;
            if (revocation)
            {
                var initial = await journal.RecordReviewedAsync(CaseA,
                    "Diagnostic", "diagnostics/source.json", "TechQA", true);
                Assert.Equal(SourceBindingJournalStatus.Recorded, initial.Status);
                bindingId = initial.BindingId!.Value;
            }

            // Simulated independent CATTECH process owns the permanent
            // marker. The requested operation must honor user cancellation
            // while waiting, not resume after that process lets go.
            var directory = JournalDirectory(root);
            Directory.CreateDirectory(directory);
            var marker = Path.Combine(directory,
                ".cattech-source-binding.lock");
            await using (var foreignLease = new FileStream(marker,
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                using var cts = new CancellationTokenSource();
                Task<SourceBindingJournalOutcome> pending = revocation
                    ? journal.RevokeAsync(bindingId, "TechQA", true, cts.Token)
                    : journal.RecordReviewedAsync(CaseA,
                        "Diagnostic", "diagnostics/source.json",
                        "TechQA", true, cts.Token);
                cts.CancelAfter(TimeSpan.FromMilliseconds(120));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async () => await pending.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Empty(Directory.GetFiles(directory, "revoked-*.json"));
                Assert.Equal(revocation ? 1 : 0,
                    Directory.GetFiles(directory, "binding-*.json").Length);
                Assert.Empty(Directory.GetFiles(directory,
                    ".cattech-binding-*.tmp"));
                Assert.Empty(Directory.GetFiles(directory,
                    ".cattech-revocation-*.tmp"));
                Assert.Equal(original, await File.ReadAllTextAsync(source));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScopedValidation_HoldsExclusiveJournalLeaseUntilCallbackEnds()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\",\"synthetic\":true}");
            var journal = new SourceBindingReviewJournal(root);
            var recorded = await journal.RecordReviewedAsync(CaseA,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, recorded.Status);
            var id = recorded.BindingId!.Value;
            var entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var resume = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var current = journal.ExecuteWhileCurrentAsync(
                id, CaseA, "Diagnostic", "diagnostics/source.json",
                async token =>
                {
                    entered.TrySetResult(true);
                    await resume.Task.WaitAsync(token);
                });
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                // The marker must STILL be exclusive while the callback
                // is awaiting; an outside inspection cannot read it.
                var concurrentRead = await new SourceBindingReviewJournal(root)
                    .InspectCurrentAsync(id, CaseA, "Diagnostic",
                        "diagnostics/source.json");
                Assert.Equal(SourceBindingReadStatus.InvalidJournal,
                    concurrentRead.Status);

                var pendingRevocation = new SourceBindingReviewJournal(root)
                    .RevokeAsync(id, "TechQB", true);
                await Task.Delay(100);
                Assert.False(pendingRevocation.IsCompleted);
                Assert.Empty(Directory.GetFiles(JournalDirectory(root),
                    "revoked-*.json"));
                resume.TrySetResult(true);
                Assert.Equal(SourceBindingReadStatus.CurrentSnapshot,
                    (await current.WaitAsync(TimeSpan.FromSeconds(10))).Status);
                Assert.Equal(SourceBindingJournalStatus.Revoked,
                    (await pendingRevocation.WaitAsync(
                        TimeSpan.FromSeconds(10))).Status);
                Assert.Equal(SourceBindingReadStatus.Revoked,
                    (await journal.InspectCurrentAsync(id, CaseA, "Diagnostic",
                        "diagnostics/source.json")).Status);
            }
            finally
            {
                resume.TrySetResult(true);
                await current.WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ScopedValidation_DoesNotInvokeCallbackForInvalidOrRevokedReceipt(
        bool revoked)
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\"}");
            var journal = new SourceBindingReviewJournal(root);
            var accepted = await journal.RecordReviewedAsync(CaseA,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, accepted.Status);
            var id = accepted.BindingId!.Value;
            if (revoked)
                Assert.Equal(SourceBindingJournalStatus.Revoked,
                    (await journal.RevokeAsync(id, "TechQA", true)).Status);
            var invoked = false;
            var outcome = await journal.ExecuteWhileCurrentAsync(
                id, CaseB, "Diagnostic", "diagnostics/source.json",
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                });
            Assert.False(invoked);
            Assert.Equal(revoked ? SourceBindingReadStatus.Revoked
                : SourceBindingReadStatus.CaseMismatch, outcome.Status);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScopedValidation_SeesExternalMutationAfterCallback()
    {
        var root = NewRoot();
        try
        {
            var path = await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\",\"state\":1}");
            var journal = new SourceBindingReviewJournal(root);
            var receipt = await journal.RecordReviewedAsync(CaseA,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, receipt.Status);
            var outcome = await journal.ExecuteWhileCurrentAsync(
                receipt.BindingId!.Value, CaseA, "Diagnostic",
                "diagnostics/source.json", async _ =>
                {
                    // Models a non-cooperating external actor: the journal
                    // marker does not protect arbitrary source files.
                    await File.WriteAllTextAsync(path,
                        "{\"id\":\"ABCDEF12\",\"state\":2}");
                });
            Assert.Equal(SourceBindingReadStatus.SourceChanged, outcome.Status);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ScopedValidation_CancellationDoesNotLeaveMarkerHeld()
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/source.json",
                "{\"id\":\"ABCDEF12\"}");
            var journal = new SourceBindingReviewJournal(root);
            var receipt = await journal.RecordReviewedAsync(CaseA,
                "Diagnostic", "diagnostics/source.json", "TechQA", true);
            Assert.Equal(SourceBindingJournalStatus.Recorded, receipt.Status);
            using var cts = new CancellationTokenSource();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                journal.ExecuteWhileCurrentAsync(receipt.BindingId!.Value,
                    CaseA, "Diagnostic", "diagnostics/source.json",
                    ct =>
                    {
                        cts.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }, cts.Token));

            Assert.Equal(SourceBindingReadStatus.CurrentSnapshot,
                (await journal.InspectCurrentAsync(receipt.BindingId.Value,
                    CaseA, "Diagnostic", "diagnostics/source.json")).Status);
        }
        finally { Directory.Delete(root, recursive: true); }
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
