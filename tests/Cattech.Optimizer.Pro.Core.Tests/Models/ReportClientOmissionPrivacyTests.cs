using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReportClientOmissionPrivacyTests
{
    [Fact]
    public async Task HtmlReport_ExcludedClient_DoesNotLeakNameIntoFilenameOrHtml()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-privacy-" + Guid.NewGuid().ToString("N"));
        const string secretName = "PRIVATE_SYNTHETIC_CUSTOMER_TEST";
        try
        {
            var service = new HtmlReportService(root);
            var options = new ReportGenerationOptions
            {
                IncludeClient = false,
                ServiceReport = new ServiceReport
                {
                    Client = new ClientInfo { Name = secretName },
                    Equipment = new EquipmentInfo { Brand = "Synthetic" }
                },
                IncludeCompany = false,
                IncludeDiagnostic = false,
                IncludeStartup = false,
                IncludeCleanup = false,
                IncludeVisualOptimization = false,
                IncludeRestorePoint = false,
                IncludeSmart = false,
                IncludeSmartTests = false,
                IncludeRecommendations = false
            };

            var output = await service.GenerateHtmlReportAsync(options);
            var html = await File.ReadAllTextAsync(output);

            Assert.DoesNotContain(secretName, Path.GetFileName(output));
            Assert.DoesNotContain(secretName, html);
            Assert.Contains("SinCliente", Path.GetFileName(output));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
