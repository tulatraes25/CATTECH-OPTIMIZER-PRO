using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// SPEC #85 — even a direct report-generator caller carrying formerly selected
/// data cannot disclose it after a section is deliberately excluded.
/// All artifacts live under a unique synthetic test directory.
/// </summary>
public sealed class ReportSectionExclusionTests
{
    private const string RestoreMarker = "QA-PRIVATE-RESTORE-MARKER";

    private static ReportGenerationOptions CreateOptions(bool include)
        => new()
        {
            IncludeCompany = false,
            IncludeClient = false,
            IncludeDiagnostic = false,
            IncludeStartup = false,
            IncludeSmart = false,
            IncludeSmartTests = false,
            IncludeRecommendations = false,
            IncludeCleanup = include,
            IncludeRestorePoint = include,
            IncludeVisualOptimization = include,
            CleanupResult = new TempCleanupResult
            {
                DeletedFiles = 77,
                DeletedBytes = 77L * 1024 * 1024
            },
            RestorePointResult = new RestorePointResult
            {
                Success = true,
                RestorePointName = RestoreMarker
            },
            VisualOptimizationResult = new VisualOptimizationResult
            {
                AppliedCount = 5,
                RequiresRestart = true
            }
        };

    [Fact]
    public async Task ExcludedSections_CannotLeakOldResultContentsIntoHtml()
    {
        await WithTemporaryReportAsync(false, html =>
        {
            Assert.DoesNotContain(RestoreMarker, html);
            Assert.DoesNotContain("77 MB", html);
            Assert.DoesNotContain("5 ajustes aplicados", html);
            Assert.DoesNotContain("Ajustes visuales aplicados", html);
            Assert.DoesNotContain("Se requiere reinicio", html);
            Assert.DoesNotContain("Punto de restauración creado", html);
        });
    }

    [Fact]
    public async Task ExplicitlyIncludedSections_StillRenderSelectedResultDetails()
    {
        await WithTemporaryReportAsync(true, html =>
        {
            Assert.Contains(RestoreMarker, html);
            Assert.Contains("77 MB", html);
            Assert.Contains("5 ajustes aplicados", html);
            Assert.Contains("Se requiere reinicio", html);
        });
    }

    private static async Task WithTemporaryReportAsync(
        bool include, Action<string> assertHtml)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-report-sections-qa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var generator = new HtmlReportService(root);
            var file = await generator.GenerateHtmlReportAsync(CreateOptions(include));
            var html = await File.ReadAllTextAsync(file);
            assertHtml(html);
        }
        finally
        {
            // Only the unique synthetic directory created by this test.
            Directory.Delete(root, recursive: true);
        }
    }
}
