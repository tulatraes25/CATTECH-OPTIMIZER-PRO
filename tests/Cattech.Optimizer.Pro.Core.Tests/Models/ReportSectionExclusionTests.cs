using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
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

    private const string CompanyMarker = "QA-PRIVATE-COMPANY-MARKER";
    private const string OsMarker = "QA-PRIVATE-SYNTHETIC-OS";

    [Fact]
    public async Task ExcludedCompanyAndDiagnostic_CannotLeakThroughHeaderSignatureOrRecommendations()
    {
        await WithTemporaryReportAsync(false, html =>
        {
            Assert.DoesNotContain(CompanyMarker, html);
            Assert.DoesNotContain(OsMarker, html);
            Assert.DoesNotContain("2 GB de RAM", html);
            Assert.DoesNotContain("700 MB", html);
            Assert.DoesNotContain("No se pudo crear punto de restauración", html);
        }, options =>
        {
            options.Settings = new AppSettings { Company = new CompanyInfo
            {
                Name = CompanyMarker,
                TechnicianName = CompanyMarker,
                Email = CompanyMarker
            }};
            options.DiagnosticReport = new DiagnosticReport
            {
                OsName = OsMarker, RamTotalGB = 2
            };
            options.IncludeCompany = false;
            options.IncludeDiagnostic = false;
            options.IncludeRecommendations = true;
            options.CleanupResult = new TempCleanupResult
            {
                DeletedBytes = 700L * 1024 * 1024, DeletedFiles = 44
            };
            options.RestorePointResult = new RestorePointResult
            {
                Success = false, ErrorMessage = RestoreMarker
            };
        });
    }

    [Fact]
    public async Task SelectedCompanyDiagnosticAndRecommendations_StillRenderWhenConsented()
    {
        await WithTemporaryReportAsync(false, html =>
        {
            Assert.Contains(CompanyMarker, html);
            Assert.Contains(OsMarker, html);
            Assert.Contains("2 GB de RAM", html);
        }, options =>
        {
            options.Settings = new AppSettings { Company = new CompanyInfo
            {
                Name = CompanyMarker
            }};
            options.DiagnosticReport = new DiagnosticReport
            {
                OsName = OsMarker, RamTotalGB = 2
            };
            options.IncludeCompany = true;
            options.IncludeDiagnostic = true;
            options.IncludeRecommendations = true;
        });
    }

    [Fact]
    public void RecommendationEngine_RespectsOptOutEvenWithCompleteOldObjects()
    {
        var options = CreateOptions(false);
        options.DiagnosticReport = new DiagnosticReport { RamTotalGB = 2 };
        options.CleanupResult = new TempCleanupResult
        {
            DeletedBytes = 700L * 1024 * 1024, DeletedFiles = 1
        };
        options.RestorePointResult = new RestorePointResult { Success = false };
        options.IncludeRecommendations = true;
        Assert.Empty(ReportRecommendationEngine.GenerateRecommendations(options));

        options.IncludeDiagnostic = true;
        options.IncludeCleanup = true;
        options.IncludeRestorePoint = true;
        Assert.NotEmpty(ReportRecommendationEngine.GenerateRecommendations(options));

        options.IncludeRecommendations = false;
        Assert.Empty(ReportRecommendationEngine.GenerateRecommendations(options));
    }

    private static async Task WithTemporaryReportAsync(
        bool include, Action<string> assertHtml,
        Action<ReportGenerationOptions>? configure = null)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-report-sections-qa-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var generator = new HtmlReportService(root);
            var options = CreateOptions(include);
            configure?.Invoke(options);
            var file = await generator.GenerateHtmlReportAsync(options);
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
