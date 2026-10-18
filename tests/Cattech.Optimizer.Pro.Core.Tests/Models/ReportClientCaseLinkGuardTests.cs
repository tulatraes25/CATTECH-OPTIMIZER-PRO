using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ReportClientCaseLinkGuardTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    private static ReportGenerationOptions Options() => new()
    {
        IncludeClient = true,
        ServiceReport = new ServiceReport { ServiceCaseId = CaseA },
        IncludeDiagnostic = false,
        IncludeStartup = false,
        IncludeSmart = false,
        IncludeSmartTests = false,
        IncludeCleanup = false,
        IncludeRestorePoint = false,
        IncludeVisualOptimization = false,
        IncludeRecommendations = false
    };

    [Theory]
    [InlineData("diagnostic")]
    [InlineData("startup")]
    [InlineData("smart")]
    [InlineData("test")]
    [InlineData("cleanup")]
    public async Task UnboundOrForeignSource_RejectsBeforeAnyHtmlFileIsWritten(string source)
    {
        var options = Options();
        switch (source)
        {
            case "diagnostic":
                options.IncludeDiagnostic = true;
                options.DiagnosticReport = new DiagnosticReport { ServiceCaseId = CaseB };
                break;
            case "startup":
                options.IncludeStartup = true;
                options.StartupAnalysis = new StartupAnalysis { ServiceCaseId = null };
                break;
            case "smart":
                options.IncludeSmart = true;
                options.SmartAnalysis = new SmartAnalysisResult { ServiceCaseId = CaseB };
                break;
            case "test":
                options.IncludeSmartTests = true;
                options.SmartTestSessions.Add(new SmartTestSession { ServiceCaseId = null });
                break;
            case "cleanup":
                options.IncludeCleanup = true;
                options.CleanupResult = new Cattech.Optimizer.Pro.Core.Models.Cleanup.TempCleanupResult();
                break;
        }

        var root = Path.Combine(Path.GetTempPath(),
            "cattech-qa-link-guard-" + Guid.NewGuid().ToString("N"));
        try
        {
            var generator = new HtmlReportService(root);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => generator.GenerateHtmlReportAsync(options));
            Assert.False(Directory.Exists(Path.Combine(root, "reports", "html")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("diagnostic")]
    [InlineData("startup")]
    [InlineData("smart")]
    [InlineData("test")]
    public async Task MatchingGuidWithoutAuditedBinding_RejectsBeforeHtml(string section)
    {
        var options = Options();
        switch (section)
        {
            case "diagnostic":
                options.IncludeDiagnostic = true;
                options.DiagnosticReport = new DiagnosticReport { ServiceCaseId = CaseA };
                break;
            case "startup":
                options.IncludeStartup = true;
                options.StartupAnalysis = new StartupAnalysis { ServiceCaseId = CaseA };
                break;
            case "smart":
                options.IncludeSmart = true;
                options.SmartAnalysis = new SmartAnalysisResult { ServiceCaseId = CaseA };
                break;
            case "test":
                options.IncludeSmartTests = true;
                options.SmartTestSessions.Add(new SmartTestSession { ServiceCaseId = CaseA });
                break;
        }

        var root = Path.Combine(Path.GetTempPath(),
            "cattech-qa-link-same-guid-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new HtmlReportService(root).GenerateHtmlReportAsync(options));
            Assert.False(Directory.Exists(Path.Combine(root, "reports", "html")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task IndependentDiagnostic_IsStillAllowedWithoutNamedClient()
    {
        var options = Options();
        options.IncludeClient = false;
        options.ServiceReport = null;
        options.IncludeDiagnostic = true;
        options.DiagnosticReport = new DiagnosticReport
        {
            ServiceCaseId = CaseA, OsName = "Synthetic standalone Windows"
        };
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-qa-standalone-" + Guid.NewGuid().ToString("N"));
        try
        {
            var file = await new HtmlReportService(root).GenerateHtmlReportAsync(options);
            Assert.Contains("Synthetic standalone Windows", await File.ReadAllTextAsync(file));
            Assert.Contains("_SinCliente_", Path.GetFileName(file));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DirectRecommendations_DoNotBypassClientAttribution()
    {
        var options = Options();
        options.IncludeRecommendations = true;
        options.IncludeDiagnostic = true;
        options.DiagnosticReport = new DiagnosticReport { ServiceCaseId = CaseA };
        Assert.Throws<InvalidOperationException>(
            () => ReportRecommendationEngine.GenerateRecommendations(options));
    }
}
