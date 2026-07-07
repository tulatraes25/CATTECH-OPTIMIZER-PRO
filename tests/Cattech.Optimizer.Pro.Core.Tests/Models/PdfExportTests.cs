using System.Text;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class PdfExportTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void PdfExporterInfo_DefaultValues_AreValid()
    {
        var info = new PdfExporterInfo();

        Assert.Equal(string.Empty, info.Name);
        Assert.False(info.IsAvailable);
        Assert.Equal(string.Empty, info.StatusMessage);
        Assert.Equal(string.Empty, info.Version);
    }

    [Fact]
    public void PdfExporterInfo_CanSetAllFields()
    {
        var info = new PdfExporterInfo
        {
            Name = "Microsoft Edge PDF Export",
            IsAvailable = true,
            StatusMessage = "Disponible",
            Version = "Edge Headless"
        };

        Assert.Equal("Microsoft Edge PDF Export", info.Name);
        Assert.True(info.IsAvailable);
        Assert.Equal("Disponible", info.StatusMessage);
        Assert.Equal("Edge Headless", info.Version);
    }

    [Fact]
    public void GeneratedReportInfo_PdfPath_IsSettable()
    {
        var info = new GeneratedReportInfo
        {
            HtmlPath = @"C:\reports\informe.html",
            PdfPath = @"C:\reports\pdf\informe.pdf"
        };

        Assert.Equal(@"C:\reports\informe.html", info.HtmlPath);
        Assert.Equal(@"C:\reports\pdf\informe.pdf", info.PdfPath);
    }

    [Fact]
    public void GeneratedReportInfo_SerializeToJson_IncludesPdfPath()
    {
        var info = new GeneratedReportInfo
        {
            ClientName = "Test Client",
            HtmlPath = @"C:\test.html",
            PdfPath = @"C:\test.pdf"
        };

        var json = JsonSerializer.Serialize(info, SerializerOptions);

        Assert.Contains("htmlPath", json);
        Assert.Contains("pdfPath", json);
        Assert.Contains("test.pdf", json);
    }

    [Fact]
    public void GeneratedReportInfo_PdfPath_DefaultsToEmpty()
    {
        var info = new GeneratedReportInfo();
        Assert.Equal(string.Empty, info.PdfPath);
    }

    [Fact]
    public void GeneratedReportInfo_CanHaveBothPaths()
    {
        var info = new GeneratedReportInfo
        {
            HtmlPath = @"C:\reports\informe.html",
            PdfPath = @"C:\reports\pdf\informe.pdf"
        };

        Assert.False(string.IsNullOrEmpty(info.HtmlPath));
        Assert.False(string.IsNullOrEmpty(info.PdfPath));
    }

    [Fact]
    public void ReportGenerationOptions_PdfPath_InGeneratedReportInfo()
    {
        var info = new GeneratedReportInfo
        {
            ClientName = "Juan Perez",
            HtmlPath = @"C:\reports\Informe_Tecnico_CATTECH_Juan_Perez_20260115-143025.html",
            PdfPath = @"C:\reports\pdf\Informe_Tecnico_CATTECH_Juan_Perez_20260115-143025.pdf"
        };

        Assert.Contains("Informe_Tecnico_CATTECH_Juan_Perez", info.HtmlPath);
        Assert.Contains("Informe_Tecnico_CATTECH_Juan_Perez", info.PdfPath);
        Assert.EndsWith(".html", info.HtmlPath);
        Assert.EndsWith(".pdf", info.PdfPath);
    }

    [Fact]
    public void PdfExporterInfo_IsAvailable_CanBeToggled()
    {
        var info1 = new PdfExporterInfo { IsAvailable = false };
        var info2 = new PdfExporterInfo { IsAvailable = true };

        Assert.False(info1.IsAvailable);
        Assert.True(info2.IsAvailable);
    }

    [Fact]
    public void PdfExporterInfo_StatusMessage_DescribesState()
    {
        var available = new PdfExporterInfo
        {
            IsAvailable = true,
            StatusMessage = "Microsoft Edge detectado. Exportación PDF disponible."
        };

        var unavailable = new PdfExporterInfo
        {
            IsAvailable = false,
            StatusMessage = "Microsoft Edge no detectado."
        };

        Assert.Contains("detectado", available.StatusMessage);
        Assert.Contains("no detectado", unavailable.StatusMessage);
    }

    // === Tests de validación de PDF real ===

    [Fact]
    public async Task ValidatePdfFile_ValidPdf_StartsWithPercentPDF()
    {
        // Crear un archivo PDF válido mínimo
        var tempFile = Path.GetTempFileName();
        try
        {
            var pdfContent = "%PDF-1.4 test content";
            await File.WriteAllTextAsync(tempFile, pdfContent, Encoding.ASCII);

            var isValid = await PdfExportService.ValidatePdfFileAsync(tempFile);

            Assert.True(isValid);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidatePdfFile_HtmlContent_ReturnsFalse()
    {
        // Simular el bug anterior: HTML en archivo .pdf
        var tempFile = Path.GetTempFileName();
        try
        {
            var htmlContent = "<!DOCTYPE html><html><head><title>Test</title></head><body><h1>Hola</h1></body></html>";
            await File.WriteAllTextAsync(tempFile, htmlContent, Encoding.UTF8);

            var isValid = await PdfExportService.ValidatePdfFileAsync(tempFile);

            Assert.False(isValid);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidatePdfFile_EmptyFile_ReturnsFalse()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Archivo vacío
            var isValid = await PdfExportService.ValidatePdfFileAsync(tempFile);

            Assert.False(isValid);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidatePdfFile_NonExistentFile_ReturnsFalse()
    {
        var isValid = await PdfExportService.ValidatePdfFileAsync(@"C:\nonexistent\file.pdf");

        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidatePdfFile_PdfWithDifferentHeader_ReturnsFalse()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "This is a text file, not a PDF", Encoding.ASCII);

            var isValid = await PdfExportService.ValidatePdfFileAsync(tempFile);

            Assert.False(isValid);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ValidatePdfFile_PdfWithValidHeader_ReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // PDF válido con cabecera correcta
            var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n");
            await File.WriteAllBytesAsync(tempFile, pdfBytes);

            var isValid = await PdfExportService.ValidatePdfFileAsync(tempFile);

            Assert.True(isValid);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void PdfExportService_GetPdfOutputPath_GeneratesCorrectPath()
    {
        var service = new PdfExportService();
        var htmlPath = @"C:\reports\html\Informe_Tecnico_CATTECH_Cliente_20260115-143025.html";

        var pdfPath = service.GetPdfOutputPath(htmlPath);

        Assert.Contains("pdf", pdfPath);
        Assert.EndsWith(".pdf", pdfPath);
        Assert.Contains("Informe_Tecnico_CATTECH_Cliente_20260115-143025", pdfPath);
    }

    [Fact]
    public void PdfExportService_GetPdfOutputPath_HandlesDifferentPaths()
    {
        var service = new PdfExportService();

        var path1 = service.GetPdfOutputPath("/reports/html/test.html");
        var path2 = service.GetPdfOutputPath(@"C:\other\path\report.html");

        Assert.EndsWith(".pdf", path1);
        Assert.EndsWith(".pdf", path2);
    }

    [Fact]
    public void PdfExportService_IsAvailable_ViaCanExport()
    {
        // Test que el servicio existe y CanExport no lanza excepción
        var service = new PdfExportService();
        var info = service.CanExportAsync().Result;

        Assert.NotNull(info);
        Assert.IsType<PdfExporterInfo>(info);
    }

    [Fact]
    public void PdfExporterInfo_Name_DefaultsToEmpty()
    {
        var info = new PdfExporterInfo();
        Assert.Equal(string.Empty, info.Name);
    }

    [Fact]
    public void PdfExporterInfo_Version_DefaultsToEmpty()
    {
        var info = new PdfExporterInfo();
        Assert.Equal(string.Empty, info.Version);
    }

    // === Tests de detección de Edge ===

    [Fact]
    public void PdfExportService_IsEdgeAvailable_ReturnsBool()
    {
        // Test que el método existe y no lanza excepción
        var result = PdfExportService.IsEdgeAvailable();

        Assert.IsType<bool>(result);
    }

    [Fact]
    public void PdfExporterInfo_EdgeDetection_MatchesIsAvailable()
    {
        // Verificar que CanExportAsync es consistente con IsEdgeAvailable
        var service = new PdfExportService();
        var syncResult = PdfExportService.IsEdgeAvailable();
        var asyncResult = service.CanExportAsync().Result.IsAvailable;

        Assert.Equal(syncResult, asyncResult);
    }

    [Fact]
    public void PdfExporterInfo_Name_IndicatesEdge()
    {
        var info = new PdfExporterInfo
        {
            Name = "Microsoft Edge PDF Export",
            Version = "Edge Headless (--print-to-pdf)"
        };

        Assert.Contains("Edge", info.Name);
        Assert.Contains("Edge", info.Version);
    }

    [Fact]
    public async Task ExportHtmlToPdf_NonExistentHtml_ThrowsFileNotFoundException()
    {
        var service = new PdfExportService();
        var tempDir = Path.GetTempPath();
        var pdfPath = Path.Combine(tempDir, "test_output.pdf");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.ExportHtmlToPdfAsync(@"C:\nonexistent\file.html", pdfPath));
    }

    [Fact]
    public async Task ExportHtmlToPdf_NoEdge_ThrowsInvalidOperationException()
    {
        // Crear un HTML temporal
        var tempHtml = Path.GetTempFileName() + ".html";
        var tempPdf = Path.GetTempFileName() + ".pdf";
        try
        {
            await File.WriteAllTextAsync(tempHtml, "<html><body>Test</body></html>");

            // Solo lanzar excepción si Edge NO está disponible
            if (!PdfExportService.IsEdgeAvailable())
            {
                var service = new PdfExportService();
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.ExportHtmlToPdfAsync(tempHtml, tempPdf));
            }
            // Si Edge está disponible, no debería lanzar excepción
        }
        finally
        {
            if (File.Exists(tempHtml)) File.Delete(tempHtml);
            if (File.Exists(tempPdf)) File.Delete(tempPdf);
        }
    }
}
