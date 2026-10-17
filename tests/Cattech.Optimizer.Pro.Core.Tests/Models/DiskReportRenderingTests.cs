using System.Globalization;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class DiskReportRenderingTests
{
    // Source-level disk-rendering regression: intentionally scoped to ONE
    // synthetic case, unlike unrelated legacy histories.
    private static readonly Guid LinkedCase =
        Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");

    private static ReportGenerationOptions Options(
        bool known, double capacity, double free, double percent) => new()
    {
        IncludeClient = true,
        IncludeDiagnostic = true,
        IncludeRecommendations = false,
        ServiceReport = new ServiceReport
        {
            ServiceCaseId = LinkedCase,
            Client = new ClientInfo { Name = "Synthetic Customer" },
            Equipment = new EquipmentInfo
            {
                PrimaryDisk = "Synthetic Disk",
                DiskType = "SSD",
                DiskCapacityGB = capacity,
                DiskFreeGB = free,
                DiskFreeSpaceKnown = known
            }
        },
        DiagnosticReport = new DiagnosticReport
        {
            ServiceCaseId = LinkedCase,
            PrimaryDiskName = "Synthetic Disk",
            DiskType = "SSD",
            DiskCapacityGB = capacity,
            DiskFreeGB = free,
            DiskFreePercent = percent,
            DiskFreeSpaceKnown = known,
            SystemVolumeLetter = "C:"
        }
    };

    private static async Task<string> RenderAsync(ReportGenerationOptions options)
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "cattech-storage-test-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        try
        {
            var service = new HtmlReportService(directory);
            // Until audited C2 binding, these are two separate legitimate
            // documents, not one invented named-client diagnostic.
            options.IncludeDiagnostic = false;
            var clientPath = await service.GenerateHtmlReportAsync(options);
            options.IncludeClient = false;
            options.ServiceReport = null;
            options.IncludeDiagnostic = true;
            var diagnosticPath = await service.GenerateHtmlReportAsync(options);
            var client = await File.ReadAllTextAsync(clientPath);
            var diagnostic = await File.ReadAllTextAsync(diagnosticPath);
            Assert.DoesNotContain("Synthetic Customer", diagnostic);
            return client + "\n" + diagnostic;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownSpaceShowsNdInClientAndDiagnosticNotFalseZero()
    {
        var html = await RenderAsync(Options(known: false, 300, 0, 0));

        Assert.Contains("Datos del Equipo", html);
        Assert.Contains("Diagnóstico Inicial", html);
        Assert.Contains("Volumen del sistema:</td><td>N/D", html);
        Assert.Contains("Espacio libre:</td><td>N/D", html);
        Assert.DoesNotContain("Espacio libre:</td><td>0.0 GB", html);
        Assert.DoesNotContain("Libre:</td><td>0.0 GB", html);
    }

    [Fact]
    public async Task KnownSpaceUsesLogicalVolumeValueInBothSections()
    {
        var html = await RenderAsync(Options(known: true, 200, 50, 25));

        var capacity = 200d.ToString("F1", CultureInfo.CurrentCulture);
        var free = 50d.ToString("F1", CultureInfo.CurrentCulture);
        Assert.Contains($"Volumen del sistema:</td><td>{capacity} GB", html);
        Assert.Contains($"Libre:</td><td>{free} GB", html);
        Assert.Contains($"Espacio libre:</td><td>{free} GB (25%)", html);
        Assert.DoesNotContain("1000.0 GB", html);
    }

    [Fact]
    public async Task KnownExactlyFullSpaceRendersZeroWithoutPretendingUnknown()
    {
        var html = await RenderAsync(Options(known: true, 200, 0, 0));

        var zero = 0d.ToString("F1", CultureInfo.CurrentCulture);
        Assert.Contains($"Libre:</td><td>{zero} GB", html);
        Assert.Contains($"Espacio libre:</td><td>{zero} GB (0%)", html);
    }
}
