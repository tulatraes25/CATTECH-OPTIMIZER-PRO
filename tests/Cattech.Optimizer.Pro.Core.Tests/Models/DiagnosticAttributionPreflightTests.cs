using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Data;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Disposable synthetic cases and diagnostics only. No report files are
/// created and no real Windows repair or hardware probes are run.
/// </summary>
public sealed class DiagnosticAttributionPreflightTests
{
    private static readonly Guid CaseA =
        Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB =
        Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private const string SourcePath = "diagnostics/diagnostic-qa.json";
    private const string Original = "{\"id\":\"ABCDEF12\",\"osName\":\"Windows QA A\"}";

    [Fact]
    public async Task StoredCaseLiveReviewAndExactSource_YieldOnlyReadOnlyCandidate()
    {
        var root = NewRoot();
        try
        {
            var client = Client(CaseA);
            var cases = new JsonServiceReportService(root);
            await cases.SaveReportAsync(client);
            var path = await WriteSourceAsync(root, Original);
            var binding = await CreateBindingAsync(root);
            var result = await new DiagnosticAttributionPreflight(cases, root)
                .InspectAsync(client, "ABCDEF12", binding);

            Assert.Equal(
                DiagnosticAttributionPreflightStatus.SnapshotForFurtherValidation,
                result.Status);
            Assert.NotNull(result.Candidate);
            Assert.Equal(binding, result.Candidate.BindingId);
            Assert.Equal(CaseA, result.Candidate.ServiceCaseId);
            Assert.Equal("ABCDEF12", result.Candidate.SourceRecordId);
            var first = result.Candidate.ReadDetachedModels();
            var second = result.Candidate.ReadDetachedModels();
            Assert.Equal("Windows QA A", first.Diagnostic.OsName);
            Assert.Equal(CaseA, first.SavedCase.ServiceCaseId);
            // A caller cannot mutate the frozen candidate by editing its
            // own detached DTO or even the WPF-selected report object.
            first.Diagnostic.OsName = "TAMPERED COPY";
            first.SavedCase.Client.Name = "TAMPERED CLIENT";
            client.Client.Name = "MUTATED SELECTION";
            Assert.Equal("Windows QA A", second.Diagnostic.OsName);
            var third = result.Candidate.ReadDetachedModels();
            Assert.Equal("Windows QA A", third.Diagnostic.OsName);
            Assert.Equal("SYNTHETIC CUSTOMER", third.SavedCase.Client.Name);
            Assert.NotSame(first.Diagnostic, second.Diagnostic);
            Assert.NotSame(first.SavedCase, second.SavedCase);
            Assert.Equal(Original, await File.ReadAllTextAsync(path));
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("revoked")]
    [InlineData("changed")]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    public async Task RevocationSourceReplacementDuplicateOrForeignCase_FailClosed(
        string scenario)
    {
        var root = NewRoot();
        try
        {
            var client = Client(CaseA);
            var cases = new JsonServiceReportService(root);
            await cases.SaveReportAsync(client);
            var json = scenario == "foreign"
                ? "{\"id\":\"ABCDEF12\",\"serviceCaseId\":\"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb\"}"
                : Original;
            var file = await WriteSourceAsync(root, json);
            var binding = await CreateBindingAsync(root);
            switch (scenario)
            {
                case "revoked":
                    Assert.Equal(SourceBindingJournalStatus.Revoked,
                        (await new SourceBindingReviewJournal(root).RevokeAsync(
                            binding, "TechQA", true)).Status);
                    break;
                case "changed":
                    await File.WriteAllTextAsync(file,
                        "{\"id\":\"ABCDEF12\",\"osName\":\"REPLACED\"}");
                    break;
                case "duplicate":
                    await File.WriteAllTextAsync(Path.Combine(root, "data",
                        "diagnostics", "diagnostic-copy.json"), json);
                    break;
            }

            var result = await new DiagnosticAttributionPreflight(cases, root)
                .InspectAsync(client, "ABCDEF12", binding);
            var expected = scenario switch
            {
                "revoked" => DiagnosticAttributionPreflightStatus.BindingRevoked,
                "changed" => DiagnosticAttributionPreflightStatus.SourceChanged,
                "duplicate" => DiagnosticAttributionPreflightStatus.AmbiguousSource,
                _ => DiagnosticAttributionPreflightStatus.CaseMismatch
            };
            Assert.Equal(expected, result.Status);
            Assert.Null(result.Candidate);
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ChangedSelectedCustomerOrMissingBinding_CannotInventAssociation()
    {
        var root = NewRoot();
        try
        {
            var client = Client(CaseA);
            var cases = new JsonServiceReportService(root);
            await cases.SaveReportAsync(client);
            await WriteSourceAsync(root, Original);
            var binding = await CreateBindingAsync(root);
            var inspector = new DiagnosticAttributionPreflight(cases, root);

            Assert.Equal(DiagnosticAttributionPreflightStatus.CaseUnavailable,
                (await inspector.InspectAsync(
                    Client(CaseB), "ABCDEF12", binding)).Status);
            Assert.Equal(DiagnosticAttributionPreflightStatus.BindingMissing,
                (await inspector.InspectAsync(client, "ABCDEF12",
                    Guid.NewGuid())).Status);
            Assert.Equal(DiagnosticAttributionPreflightStatus.InvalidSelection,
                (await inspector.InspectAsync(client, "BAD/ID", binding)).Status);
            Assert.False(Directory.Exists(Path.Combine(root, "reports")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static ServiceReport Client(Guid caseId) => new()
    {
        Id = "11111111111141118111111111111111",
        ServiceCaseId = caseId,
        Client = new ClientInfo { Name = "SYNTHETIC CUSTOMER" },
        Equipment = new EquipmentInfo { Brand = "QA", Model = "Notebook" }
    };

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2d2-preflight-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> WriteSourceAsync(string root, string json)
    {
        var file = Path.Combine(root, "data", "diagnostics", "diagnostic-qa.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, json);
        return file;
    }

    private static async Task<Guid> CreateBindingAsync(string root)
    {
        var receipt = await new SourceBindingReviewJournal(root).RecordReviewedAsync(
            CaseA, "Diagnostic", SourcePath, "TechQA", true);
        Assert.Equal(SourceBindingJournalStatus.Recorded, receipt.Status);
        return Assert.IsType<Guid>(receipt.BindingId);
    }
}
