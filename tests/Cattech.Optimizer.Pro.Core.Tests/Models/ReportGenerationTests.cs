using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReportGenerationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void ReportGenerationOptions_DefaultValues_AreValid()
    {
        var options = new ReportGenerationOptions();

        Assert.True(options.IncludeCompany);
        Assert.True(options.IncludeClient);
        Assert.True(options.IncludeDiagnostic);
        Assert.True(options.IncludeStartup);
        Assert.True(options.IncludeCleanup);
        Assert.True(options.IncludeVisualOptimization);
        Assert.True(options.IncludeRestorePoint);
        Assert.True(options.IncludeRecommendations);
        Assert.Equal(string.Empty, options.FinalObservations);
    }

    [Fact]
    public void ReportGenerationOptions_CanDisableSections()
    {
        var options = new ReportGenerationOptions
        {
            IncludeCompany = false,
            IncludeClient = false,
            IncludeDiagnostic = false
        };

        Assert.False(options.IncludeCompany);
        Assert.False(options.IncludeClient);
        Assert.False(options.IncludeDiagnostic);
        Assert.True(options.IncludeStartup); // Default
    }

    [Fact]
    public void GeneratedReportInfo_DefaultValues_AreValid()
    {
        var info = new GeneratedReportInfo();

        Assert.NotNull(info.Id);
        Assert.NotEmpty(info.Id);
        Assert.Equal(8, info.Id.Length);
        Assert.True(info.CreatedAt <= DateTime.Now);
        Assert.NotNull(info.IncludedSections);
        Assert.Empty(info.IncludedSections);
    }

    [Fact]
    public void GeneratedReportInfo_SerializeToJson_ProducesValidJson()
    {
        var info = new GeneratedReportInfo
        {
            ClientName = "Juan Perez",
            EquipmentName = "Dell Latitude 5520",
            HtmlPath = @"C:\reports\informe.html",
            Notes = "Informe de prueba"
        };

        info.IncludedSections.Add("Empresa");
        info.IncludedSections.Add("Cliente");

        var json = JsonSerializer.Serialize(info, SerializerOptions);

        Assert.Contains("Juan Perez", json);
        Assert.Contains("Dell Latitude 5520", json);
        Assert.Contains("informe.html", json);
    }

    [Fact]
    public void GeneratedReportInfo_DeserializeFromJson_PreservesAllFields()
    {
        var original = new GeneratedReportInfo
        {
            ClientName = "Test Client",
            EquipmentName = "HP ProBook",
            HtmlPath = @"C:\test.html",
            Notes = "Test notes"
        };

        original.IncludedSections.Add("Diagnóstico");

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<GeneratedReportInfo>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("Test Client", deserialized!.ClientName);
        Assert.Equal("HP ProBook", deserialized.EquipmentName);
        Assert.Single(deserialized.IncludedSections);
        Assert.Contains("Diagnóstico", deserialized.IncludedSections);
    }

    [Fact]
    public void ReportRecommendation_DefaultValues_AreValid()
    {
        var rec = new ReportRecommendation();

        Assert.Equal(string.Empty, rec.Category);
        Assert.Equal(string.Empty, rec.Message);
        Assert.Equal("Info", rec.Severity);
        Assert.Equal("ℹ️", rec.Icon);
    }

    [Fact]
    public void ReportRecommendation_AllSeverities_AreValid()
    {
        var recInfo = new ReportRecommendation { Severity = "Info" };
        var recWarning = new ReportRecommendation { Severity = "Warning" };
        var recCritical = new ReportRecommendation { Severity = "Critical" };

        Assert.Equal("Info", recInfo.Severity);
        Assert.Equal("Warning", recWarning.Severity);
        Assert.Equal("Critical", recCritical.Severity);
    }

    [Fact]
    public void ReportGenerationOptions_WithCompanyData_HasCorrectStructure()
    {
        var settings = new AppSettings
        {
            Company = new CompanyInfo
            {
                Name = "CATTECH Services",
                TechnicianName = "Juan Perez",
                Phone = "+54 11 1234-5678",
                Email = "info@cattech.com",
                City = "Buenos Aires"
            }
        };

        var options = new ReportGenerationOptions
        {
            Settings = settings,
            IncludeCompany = true
        };

        Assert.NotNull(options.Settings);
        Assert.Equal("CATTECH Services", options.Settings.Company.Name);
        Assert.Equal("Juan Perez", options.Settings.Company.TechnicianName);
    }

    [Fact]
    public void ReportGenerationOptions_WithDiagnosticData_HasCorrectStructure()
    {
        var diagnostic = new DiagnosticReport
        {
            OsName = "Windows 11 Pro",
            RamTotalGB = 16,
            DiskType = "NVMe",
            DiskFreePercent = 45
        };

        var options = new ReportGenerationOptions
        {
            DiagnosticReport = diagnostic,
            IncludeDiagnostic = true
        };

        Assert.NotNull(options.DiagnosticReport);
        Assert.Equal("Windows 11 Pro", options.DiagnosticReport.OsName);
        Assert.Equal(16, options.DiagnosticReport.RamTotalGB);
    }

    [Fact]
    public void ReportGenerationOptions_WithRestorePoint_HasCorrectStructure()
    {
        var rp = new RestorePointResult
        {
            Success = true,
            RestorePointName = "CATTECH - Test Point"
        };

        var options = new ReportGenerationOptions
        {
            RestorePointResult = rp,
            IncludeRestorePoint = true
        };

        Assert.NotNull(options.RestorePointResult);
        Assert.True(options.RestorePointResult.Success);
        Assert.Equal("CATTECH - Test Point", options.RestorePointResult.RestorePointName);
    }

    [Fact]
    public void ReportGenerationOptions_FinalObservations_IsSettable()
    {
        var options = new ReportGenerationOptions
        {
            FinalObservations = "El equipo fue revisado completamente y se encontró en buen estado."
        };

        Assert.Equal("El equipo fue revisado completamente y se encontró en buen estado.", options.FinalObservations);
    }

    [Fact]
    public void ReportGenerationOptions_OutputFileName_IsSettable()
    {
        var options = new ReportGenerationOptions
        {
            OutputFileName = "Informe_Perez_20240115"
        };

        Assert.Equal("Informe_Perez_20240115", options.OutputFileName);
    }
}
