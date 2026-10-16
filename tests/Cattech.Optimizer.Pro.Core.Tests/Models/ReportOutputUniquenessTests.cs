using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ReportOutputUniquenessTests
{
    private static ReportGenerationOptions NewOptions(string clientName, string model) =>
        new()
        {
            IncludeCompany = false,
            IncludeClient = true,
            IncludeDiagnostic = false,
            IncludeStartup = false,
            IncludeCleanup = false,
            IncludeVisualOptimization = false,
            IncludeRestorePoint = false,
            IncludeSmart = false,
            IncludeSmartTests = false,
            IncludeRecommendations = false,
            ServiceReport = new ServiceReport
            {
                Client = new ClientInfo { Name = clientName },
                Equipment = new EquipmentInfo
                {
                    Brand = "Synthetic QA",
                    Model = model,
                    EquipmentType = "Notebook"
                },
                Service = new ServiceInfo { Reason = "Parallel export regression" }
            }
        };

    [Fact]
    public async Task ParallelReports_SameClient_NoTwoArtifactsShareAPathOrContent()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-qa-unique-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var service = new HtmlReportService(root);
            var options = Enumerable.Range(0, 16)
                .Select(i => NewOptions("Cliente QA Test", $"MODEL-{i:D3}"))
                .ToArray();

            // Same user + timestamp window must never result in silent overwrites.
            var paths = await Task.WhenAll(
                options.Select(o => service.GenerateHtmlReportAsync(o)));
            Assert.Equal(options.Length, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());

            for (int i = 0; i < paths.Length; i++)
            {
                Assert.True(File.Exists(paths[i]));
                var html = await File.ReadAllTextAsync(paths[i]);
                Assert.Contains($"MODEL-{i:D3}", html);
                Assert.DoesNotContain($"MODEL-{(i + 1) % paths.Length:D3}", html);
            }

            var reportsDirectory = Path.Combine(root, "reports", "html");
            Assert.Equal(options.Length, Directory.GetFiles(reportsDirectory, "*.html").Length);
            Assert.Empty(Directory.GetFiles(reportsDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExcessivelyLongClientName_IsBoundedWithoutLosingHtmlContent()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-qa-filename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var name = new string('X', 350) + @"\/:*?<>|";
            var service = new HtmlReportService(root);

            var path = await service.GenerateHtmlReportAsync(
                NewOptions(name, "Synthetic Notebook"));
            var fileName = Path.GetFileName(path);
            var html = await File.ReadAllTextAsync(path);

            Assert.True(fileName.Length < 180, "Keep Windows-compatible file names short.");
            Assert.StartsWith("Informe_Tecnico_CATTECH_", fileName);
            Assert.DoesNotContain("/", fileName);
            Assert.DoesNotContain(@"\", fileName);
            Assert.DoesNotContain("?", fileName);
            Assert.Contains(new string('X', 350), html);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidOnlyClientName_UsesSafeFallback()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-qa-filename-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var service = new HtmlReportService(root);
            var path = await service.GenerateHtmlReportAsync(NewOptions("///", "Synthetic"));
            Assert.Contains("_SinCliente_", Path.GetFileName(path));
            Assert.True(File.Exists(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
