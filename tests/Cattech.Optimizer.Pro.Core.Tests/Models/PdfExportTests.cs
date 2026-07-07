using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

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
            Name = "WebView2 PDF Export",
            IsAvailable = true,
            StatusMessage = "Disponible",
            Version = "Chromium-based"
        };

        Assert.Equal("WebView2 PDF Export", info.Name);
        Assert.True(info.IsAvailable);
        Assert.Equal("Disponible", info.StatusMessage);
        Assert.Equal("Chromium-based", info.Version);
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
            HtmlPath = @"C:\reports\Informe_Tecnico_CATTECH_Juan_Perez_20240115-143025.html",
            PdfPath = @"C:\reports\pdf\Informe_Tecnico_CATTECH_Juan_Perez_20240115-143025.pdf"
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
            StatusMessage = "WebView2 Runtime detectado. Exportación PDF disponible."
        };

        var unavailable = new PdfExporterInfo
        {
            IsAvailable = false,
            StatusMessage = "WebView2 Runtime no detectado."
        };

        Assert.Contains("detectado", available.StatusMessage);
        Assert.Contains("no detectado", unavailable.StatusMessage);
    }
}

