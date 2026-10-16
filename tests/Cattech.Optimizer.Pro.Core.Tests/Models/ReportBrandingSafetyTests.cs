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

    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9WlNdjIAAAAASUVORK5CYII=";

    [Fact]
    public async Task SmallPngLogo_IsEmbeddedWithoutDisclosingItsLocalPath()
    {
        var root = CreateTemporaryLogoDirectory();
        try
        {
            var logoPath = Path.Combine(root, "synthetic-logo.png");
            File.WriteAllBytes(logoPath, Convert.FromBase64String(OnePixelPngBase64));

            var html = await GenerateSyntheticReportAsync("#0078D4", logoPath);
            Assert.Contains("data:image/png;base64,", html);
            Assert.DoesNotContain(logoPath, html, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".png", false)]
    public async Task LogoWithUnsupportedExtensionOrForgedContent_IsNotEmbedded(
        string extension, bool hasPngBytes)
    {
        var root = CreateTemporaryLogoDirectory();
        try
        {
            var logoPath = Path.Combine(root, "synthetic-logo" + extension);
            File.WriteAllBytes(logoPath, hasPngBytes
                ? Convert.FromBase64String(OnePixelPngBase64)
                : System.Text.Encoding.UTF8.GetBytes(
                    "synthetic customer information must stay private"));
            var html = await GenerateSyntheticReportAsync("#0078D4", logoPath);

            Assert.DoesNotContain("data:image/", html, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic customer information", html,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task OversizedPngLogo_IsNotReadIntoPublicHtml()
    {
        var root = CreateTemporaryLogoDirectory();
        try
        {
            var logoPath = Path.Combine(root, "oversized-logo.png");
            await using (var stream = new FileStream(logoPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                stream.SetLength(4 * 1024 * 1024 + 1);
                stream.Position = 0;
                await stream.WriteAsync(new byte[] {
                    137, 80, 78, 71, 13, 10, 26, 10
                });
            }

            var html = await GenerateSyntheticReportAsync("#0078D4", logoPath);
            Assert.DoesNotContain("data:image/", html, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryLogoDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-logo-safety-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> GenerateSyntheticReportAsync(string configured, string? logoPath = null)
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
                        PrimaryColor = configured,
                        LogoPath = logoPath ?? string.Empty
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
