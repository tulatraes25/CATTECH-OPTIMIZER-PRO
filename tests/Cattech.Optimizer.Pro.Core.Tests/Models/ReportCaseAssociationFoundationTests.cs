using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>SPEC #92 stage A — no automatic linkage of legacy snapshots.</summary>
public sealed class ReportCaseAssociationFoundationTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    [Fact]
    public void OldJsonWithoutCase_StaysUnboundForEverySource()
    {
        const string legacy = "{\"Id\":\"synthetic-legacy\"}";
        Assert.Null(JsonSerializer.Deserialize<ServiceReport>(legacy)!.ServiceCaseId);
        Assert.Null(JsonSerializer.Deserialize<DiagnosticReport>(legacy)!.ServiceCaseId);
        Assert.Null(JsonSerializer.Deserialize<StartupAnalysis>(legacy)!.ServiceCaseId);
        Assert.Null(JsonSerializer.Deserialize<SmartAnalysisResult>(legacy)!.ServiceCaseId);
        Assert.Null(JsonSerializer.Deserialize<SmartTestSession>(legacy)!.ServiceCaseId);

        object[] source = { new ServiceReport(), new DiagnosticReport(),
            new StartupAnalysis(), new SmartAnalysisResult(), new SmartTestSession() };
        foreach (var item in source)
            Assert.DoesNotContain("ServiceCaseId", JsonSerializer.Serialize(item, item.GetType()));
    }

    [Fact]
    public void ExplicitCase_RoundTripsOnEachStoredSource()
    {
        var client = new ServiceReport { ServiceCaseId = CaseA };
        var diag = new DiagnosticReport { ServiceCaseId = CaseA };
        var startup = new StartupAnalysis { ServiceCaseId = CaseA };
        var smart = new SmartAnalysisResult { ServiceCaseId = CaseA };
        var test = new SmartTestSession { ServiceCaseId = CaseA };

        Assert.Equal(CaseA, JsonSerializer.Deserialize<ServiceReport>(
            JsonSerializer.Serialize(client))!.ServiceCaseId);
        Assert.Equal(CaseA, JsonSerializer.Deserialize<DiagnosticReport>(
            JsonSerializer.Serialize(diag))!.ServiceCaseId);
        Assert.Equal(CaseA, JsonSerializer.Deserialize<StartupAnalysis>(
            JsonSerializer.Serialize(startup))!.ServiceCaseId);
        Assert.Equal(CaseA, JsonSerializer.Deserialize<SmartAnalysisResult>(
            JsonSerializer.Serialize(smart))!.ServiceCaseId);
        Assert.Equal(CaseA, JsonSerializer.Deserialize<SmartTestSession>(
            JsonSerializer.Serialize(test))!.ServiceCaseId);
    }

    [Fact]
    public void MatchingPolicy_RejectsUnboundEmptyAndDifferentCases()
    {
        Assert.False(ReportSourceCasePolicy.HasMatchingExplicitCase(null, null));
        Assert.False(ReportSourceCasePolicy.HasMatchingExplicitCase(CaseA, null));
        Assert.False(ReportSourceCasePolicy.HasMatchingExplicitCase(null, CaseA));
        Assert.False(ReportSourceCasePolicy.HasMatchingExplicitCase(CaseA, Guid.Empty));
        Assert.False(ReportSourceCasePolicy.HasMatchingExplicitCase(Guid.Empty, Guid.Empty));
        Assert.False(ReportSourceCasePolicy.HasMatchingExplicitCase(CaseA, CaseB));
        Assert.True(ReportSourceCasePolicy.HasMatchingExplicitCase(CaseA, CaseA));
    }
}
