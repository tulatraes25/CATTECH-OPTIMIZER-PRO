using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ReportBrandingSafetyTests
{
    [Theory]
    [InlineData("blue'; data-synthetic='unsafe")]
    [InlineData("#12<")]
    [InlineData("red")]
    [InlineData("#12345678")]
    [InlineData("")]
    [InlineData(" #00ABCD ")]
    public async Task UntrustedBrandColor_UsesSafeFallback_WithoutHtmlAttributeInjection(
        string configured)
    {
        var html = await GenerateSyntheticReportAsync(configured);
        Assert.Contains("style='border-bottom: 4px solid #0078D4;'", html);
        Assert.Contains("style='color: #0078D4;'", html);
        if (!string.IsNullOrEmpty(configured))
            Assert.DoesNotContain(configured, html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-synthetic=", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#12AbEF")]
    [InlineData("#abc")]
    [InlineData("#000000")]
    public async Task ValidHexBrandColors_ArePreserved(string configured)
    {
        var html = await GenerateSyntheticReportAsync(configured);
        Assert.Contains("style='border-bottom: 4px solid " + configured + ";'", html);
        Assert.Contains("style='color: " + configured + ";'", html);
    }

    private static async Task<string> GenerateSyntheticReportAsync(string configured)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-brand-safety-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new HtmlReportService(root);
            var options = new ReportGenerationOptions
            {
                Settings = new AppSettings
                {
                    Company = new CompanyInfo
                    {
                        Name = "Synthetic company",
                        PrimaryColor = configured
                    }
                },
                IncludeClient = false,
                IncludeDiagnostic = false,
                IncludeStartup = false,
                IncludeCleanup = false,
                IncludeVisualOptimization = false,
                IncludeRestorePoint = false,
                IncludeSmart = false,
                IncludeSmartTests = false,
                IncludeRecommendations = false
            };
            var path = await service.GenerateHtmlReportAsync(options);
            return await File.ReadAllTextAsync(path);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
