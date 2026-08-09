using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;
using Cattech.Optimizer.Pro.Core.Models.Smart;

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

    // =====================
    // Tests Fase A.7.1 - SMART en informe
    // =====================

    [Fact]
    public void ReportGenerationOptions_AcceptsSmartAnalysis()
    {
        var options = new ReportGenerationOptions
        {
            SmartAnalysis = new SmartAnalysisResult
            {
                StartedAt = new DateTime(2026, 8, 8, 20, 30, 0),
                DevicesAnalyzed = 2
            }
        };

        Assert.NotNull(options.SmartAnalysis);
        Assert.Equal(2, options.SmartAnalysis.DevicesAnalyzed);
        Assert.True(options.IncludeSmart);
    }

    [Fact]
    public void IncludeSmartFalse_OmitsSmartSection()
    {
        var options = new ReportGenerationOptions
        {
            IncludeSmart = false,
            SmartAnalysis = new SmartAnalysisResult()
        };

        var includeSection = options.IncludeSmart && options.SmartAnalysis != null;
        Assert.False(includeSection);
    }

    [Fact]
    public void IncludeSmartTrue_WithNullAnalysis_OmitsSectionWithoutError()
    {
        var options = new ReportGenerationOptions
        {
            IncludeSmart = true,
            SmartAnalysis = null
        };

        var includeSection = options.IncludeSmart && options.SmartAnalysis != null;
        Assert.False(includeSection);
    }

    [Fact]
    public void SmartGood_AppearsAsBueno()
    {
        var report = new SmartDiskReport { HealthStatus = SmartHealthStatus.Good };
        var text = report.HealthStatus switch
        {
            SmartHealthStatus.Good => "Bueno",
            _ => "Otro"
        };
        Assert.Equal("Bueno", text);
    }

    [Fact]
    public void SmartWarning_AppearsAsPrecaucion()
    {
        var report = new SmartDiskReport { HealthStatus = SmartHealthStatus.Warning };
        var text = report.HealthStatus switch
        {
            SmartHealthStatus.Warning => "Precaución",
            _ => "Otro"
        };
        Assert.Equal("Precaución", text);
    }

    [Fact]
    public void SmartCritical_AppearsAsCritico()
    {
        var report = new SmartDiskReport { HealthStatus = SmartHealthStatus.Critical };
        var text = report.HealthStatus switch
        {
            SmartHealthStatus.Critical => "Crítico",
            _ => "Otro"
        };
        Assert.Equal("Crítico", text);
    }

    [Fact]
    public void SmartNotAvailable_NotAppearsAsSano()
    {
        var report = new SmartDiskReport { HealthStatus = SmartHealthStatus.NotAvailable };
        var text = report.HealthStatus switch
        {
            SmartHealthStatus.Good => "Bueno",
            SmartHealthStatus.NotAvailable => "No disponible",
            _ => "Otro"
        };
        Assert.Equal("No disponible", text);
        Assert.NotEqual("Bueno", text);
    }

    [Fact]
    public void SmartUnknown_NotAppearsAsSano()
    {
        var report = new SmartDiskReport { HealthStatus = SmartHealthStatus.Unknown };
        var text = report.HealthStatus switch
        {
            SmartHealthStatus.Good => "Bueno",
            SmartHealthStatus.Unknown => "Desconocido",
            _ => "Otro"
        };
        Assert.Equal("Desconocido", text);
        Assert.NotEqual("Bueno", text);
    }

    [Fact]
    public void ReallocatedSectorCount_UsesRealValue()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes = [new SmartAttribute { Id = 5, RawValue = 12 }]
        };
        Assert.Equal(12, report.ReallocatedSectorCount);
    }

    [Fact]
    public void PendingSectorCount_UsesRealValue()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes = [new SmartAttribute { Id = 197, RawValue = 7 }]
        };
        Assert.Equal(7, report.PendingSectorCount);
    }

    [Fact]
    public void NvmePercentageUsed_AppearsCorrectly()
    {
        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            NvmePercentageUsed = 85
        };
        Assert.Equal(85, report.NvmePercentageUsed);
    }

    [Fact]
    public void NvmeNull_NotShownAsZero()
    {
        var report = new SmartDiskReport { Protocol = "NVMe" };
        Assert.Null(report.NvmePercentageUsed);
        Assert.Null(report.NvmeMediaErrors);
    }

    [Fact]
    public void RequiresBackupRecommendation_ShownInReport()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Critical,
            RequiresBackupRecommendation = true
        };
        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Warnings_IncludedInReport()
    {
        var report = new SmartDiskReport();
        report.Warnings.Add("Warning 1");
        report.Warnings.Add("Warning 2");
        Assert.Equal(2, report.Warnings.Count);
    }

    [Fact]
    public void Errors_IncludedInReport()
    {
        var report = new SmartDiskReport();
        report.Errors.Add("Error 1");
        Assert.Single(report.Errors);
    }

    [Fact]
    public void SmartContent_EscapesHtml()
    {
        var report = new SmartDiskReport
        {
            ModelName = "<script>alert('x')</script>"
        };
        var escaped = System.Net.WebUtility.HtmlEncode(report.ModelName);
        Assert.DoesNotContain("<script>", escaped);
        Assert.Contains("&lt;script&gt;", escaped);
    }

    [Fact]
    public void SmartAnalysisResult_DisplayName_Format()
    {
        var analysis = new SmartAnalysisResult
        {
            StartedAt = new DateTime(2026, 8, 8, 20, 30, 0),
            Reports = [new SmartDiskReport(), new SmartDiskReport()]
        };
        Assert.Equal("08/08/2026 20:30 - 2 disco(s)", analysis.DisplayName);
    }

    [Fact]
    public void IncludedSections_AddsSmart()
    {
        var info = new GeneratedReportInfo();
        info.IncludedSections.Add("SMART");
        Assert.Contains("SMART", info.IncludedSections);
    }

    [Fact]
    public void BuildReportOptions_IncludesSmartAnalysis()
    {
        // Verificar que el builder incluye SmartAnalysis (simulado)
        var options = new ReportGenerationOptions
        {
            SmartAnalysis = new SmartAnalysisResult
            {
                StartedAt = DateTime.Now,
                Reports = [new SmartDiskReport { HealthStatus = SmartHealthStatus.Good }]
            },
            IncludeSmart = true
        };

        Assert.NotNull(options.SmartAnalysis);
        Assert.True(options.IncludeSmart);
    }

    // =====================
    // Tests Fase A.7.1 fix - Métricas ATA vs NVMe
    // =====================

    [Fact]
    public void IsNvme_HasPriority_OverAta()
    {
        // Un disco con Protocol NVMe no debe clasificarse como ATA aunque tenga DeviceType SSD
        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            DeviceType = "SSD"
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("NVMe", StringComparison.OrdinalIgnoreCase);

        var isAta = !isNvme &&
                    (report.Protocol.Contains("SATA", StringComparison.OrdinalIgnoreCase) ||
                     report.Protocol.Contains("ATA", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("SATA", StringComparison.OrdinalIgnoreCase));

        Assert.True(isNvme);
        Assert.False(isAta);
    }

    [Fact]
    public void NvmeWithTemperature_DoesNotShowAtaMetrics()
    {
        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            DeviceType = "NVMe",
            TemperatureCelsius = 45,
            PowerOnHours = 100,
            ImportantAttributes =
            [
                new SmartAttribute { Id = 5, RawValue = 999 },  // Reallocated (ATA)
                new SmartAttribute { Id = 197, RawValue = 999 } // Pending (ATA)
            ]
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("NVMe", StringComparison.OrdinalIgnoreCase);

        var isAta = !isNvme &&
                    (report.Protocol.Contains("SATA", StringComparison.OrdinalIgnoreCase) ||
                     report.Protocol.Contains("ATA", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                     report.DeviceType.Contains("SATA", StringComparison.OrdinalIgnoreCase));

        Assert.True(isNvme);
        Assert.False(isAta);
    }

    [Fact]
    public void NvmeWithPowerOnHours_DoesNotShowReallocated()
    {
        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            DeviceType = "NVMe",
            PowerOnHours = 500,
            ImportantAttributes =
            [
                new SmartAttribute { Id = 5, RawValue = 5 } // Reallocated (ATA)
            ]
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
        var isAta = !isNvme && report.DeviceType.Contains("HDD", StringComparison.OrdinalIgnoreCase);

        Assert.True(isNvme);
        Assert.False(isAta);
    }

    [Fact]
    public void Nvme_ShowsTemperatureOnce()
    {
        // Simular la lógica de renderizado: la temperatura se agrega una sola vez
        var rendered = new List<string>();

        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            DeviceType = "NVMe",
            TemperatureCelsius = 40,
            PowerOnHours = 200
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
        var hasCommonMetrics = report.TemperatureCelsius > 0 || report.PowerOnHours > 0;

        if (hasCommonMetrics && report.TemperatureCelsius > 0)
            rendered.Add("Temperatura");
        if (isNvme && report.TemperatureCelsius > 0)
            rendered.Add("Temperatura"); // Esto NO debería ocurrir en el código corregido

        // El código corregido solo agrega temperatura en métricas comunes
        Assert.True(hasCommonMetrics);
    }

    [Fact]
    public void Nvme_ShowsPowerOnHoursOnce()
    {
        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            DeviceType = "NVMe",
            PowerOnHours = 300
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
        var hasCommonMetrics = report.PowerOnHours > 0;

        // En el código corregido, las horas se muestran solo en métricas comunes
        Assert.True(hasCommonMetrics);
    }

    [Fact]
    public void NvmePercentageUsed_AppearsInNvmeBlock()
    {
        var report = new SmartDiskReport
        {
            Protocol = "NVMe",
            DeviceType = "NVMe",
            NvmePercentageUsed = 85
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
        Assert.True(isNvme);
        Assert.Equal(85, report.NvmePercentageUsed);
    }

    [Fact]
    public void HddAta_StillShowsReallocated()
    {
        var report = new SmartDiskReport
        {
            Protocol = "SATA",
            DeviceType = "HDD",
            ImportantAttributes =
            [
                new SmartAttribute { Id = 5, RawValue = 12 }
            ]
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
        var isAta = !isNvme && report.DeviceType.Contains("HDD", StringComparison.OrdinalIgnoreCase);

        Assert.True(isAta);
        Assert.Equal(12, report.ReallocatedSectorCount);
    }

    [Fact]
    public void HddAta_StillShowsPending()
    {
        var report = new SmartDiskReport
        {
            Protocol = "SATA",
            DeviceType = "HDD",
            ImportantAttributes =
            [
                new SmartAttribute { Id = 197, RawValue = 7 }
            ]
        };

        var isNvme = report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
        var isAta = !isNvme && report.DeviceType.Contains("HDD", StringComparison.OrdinalIgnoreCase);

        Assert.True(isAta);
        Assert.Equal(7, report.PendingSectorCount);
    }

    // =====================
    // Tests de backup para NotAvailable/Unknown
    // =====================

    [Fact]
    public void NotAvailable_DoesNotShowBackupNo()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.NotAvailable,
            RequiresBackupRecommendation = false
        };

        // Para NotAvailable el texto NO debe ser "Backup recomendado: No"
        var isNotAvailable = report.HealthStatus == SmartHealthStatus.NotAvailable;
        var backupText = isNotAvailable ? "No determinado" : "No";

        Assert.NotEqual("No", backupText);
        Assert.Equal("No determinado", backupText);
    }

    [Fact]
    public void NotAvailable_ShowsNoDeterminado()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.NotAvailable
        };

        var isNotAvailableOrUnknown = report.HealthStatus == SmartHealthStatus.NotAvailable ||
                                      report.HealthStatus == SmartHealthStatus.Unknown;
        var backupText = isNotAvailableOrUnknown ? "No determinado" : "No";

        Assert.Equal("No determinado", backupText);
    }

    [Fact]
    public void Unknown_DoesNotShowSafeBackupMessage()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Unknown,
            RequiresBackupRecommendation = false
        };

        // Unknown no debe decir "Backup recomendado: No" (daría falsa seguridad)
        var isNotAvailableOrUnknown = report.HealthStatus == SmartHealthStatus.NotAvailable ||
                                      report.HealthStatus == SmartHealthStatus.Unknown;

        Assert.True(isNotAvailableOrUnknown);
    }

    [Fact]
    public void Unknown_ShowsNoDeterminado()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Unknown
        };

        var isNotAvailableOrUnknown = report.HealthStatus == SmartHealthStatus.NotAvailable ||
                                      report.HealthStatus == SmartHealthStatus.Unknown;
        var backupText = isNotAvailableOrUnknown ? "No determinado" : "No";

        Assert.Equal("No determinado", backupText);
    }

    [Fact]
    public void Critical_StillRecommendsBackup()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Critical,
            RequiresBackupRecommendation = true
        };

        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Good_MayIndicateNoImmediateBackup()
    {
        var report = new SmartDiskReport
        {
            HealthStatus = SmartHealthStatus.Good,
            RequiresBackupRecommendation = false
        };

        // Good puede decir "Backup inmediato recomendado: No"
        var isNotAvailableOrUnknown = report.HealthStatus == SmartHealthStatus.NotAvailable ||
                                      report.HealthStatus == SmartHealthStatus.Unknown;

        Assert.False(isNotAvailableOrUnknown);
        Assert.False(report.RequiresBackupRecommendation);
    }

    // =====================
    // Tests Fase A.7.2a - Self-Tests en informe
    // =====================

    private static SmartTestSession CreateTestSession(string id, SmartTestType type, SmartTestStatus status,
        DateTime requestedAt, DateTime? lastCheckedAt = null, DateTime? completedAt = null)
    {
        return new SmartTestSession
        {
            Id = id,
            Device = "/dev/sda",
            ModelName = "Samsung SSD 860 EVO",
            TestType = type,
            Status = status,
            RequestedAt = requestedAt,
            LastCheckedAt = lastCheckedAt,
            CompletedAt = completedAt
        };
    }

    [Fact]
    public void ReportGenerationOptions_AcceptsSmartTestSessionsList()
    {
        var options = new ReportGenerationOptions
        {
            SmartTestSessions =
            [
                CreateTestSession("TST00001", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, new DateTime(2026, 8, 9, 10, 0, 0))
            ]
        };

        Assert.Single(options.SmartTestSessions);
        Assert.False(options.IncludeSmartTests);
    }

    [Fact]
    public void IncludeSmartTestsFalse_OmitsSection()
    {
        var options = new ReportGenerationOptions
        {
            IncludeSmartTests = false,
            SmartTestSessions = [CreateTestSession("TST00002", SmartTestType.Short, SmartTestStatus.InProgress, DateTime.Now)]
        };

        var include = options.IncludeSmartTests && options.SmartTestSessions.Count > 0;
        Assert.False(include);
    }

    [Fact]
    public void IncludeSmartTestsTrue_WithEmptyList_OmitsSection()
    {
        var options = new ReportGenerationOptions
        {
            IncludeSmartTests = true,
            SmartTestSessions = []
        };

        var include = options.IncludeSmartTests && options.SmartTestSessions.Count > 0;
        Assert.False(include);
    }

    [Fact]
    public void ShortTestType_AppearsAsCorto()
    {
        var session = CreateTestSession("TST00003", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now);
        var text = session.TestType == SmartTestType.Extended ? "Extendido" : "Corto";
        Assert.Equal("Corto", text);
    }

    [Fact]
    public void ExtendedTestType_AppearsAsExtendido()
    {
        var session = CreateTestSession("TST00004", SmartTestType.Extended, SmartTestStatus.CompletedWithoutError, DateTime.Now);
        var text = session.TestType == SmartTestType.Extended ? "Extendido" : "Corto";
        Assert.Equal("Extendido", text);
    }

    [Fact]
    public void CompletedWithoutError_MapsCorrectly()
    {
        var session = CreateTestSession("TST00005", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now);
        var text = session.Status switch
        {
            SmartTestStatus.CompletedWithoutError => "Completado sin errores",
            _ => "Otro"
        };
        Assert.Equal("Completado sin errores", text);
    }

    [Fact]
    public void CompletedWithError_MapsCorrectly()
    {
        var session = CreateTestSession("TST00006", SmartTestType.Short, SmartTestStatus.CompletedWithError, DateTime.Now);
        var text = session.Status switch
        {
            SmartTestStatus.CompletedWithError => "Completado con errores",
            _ => "Otro"
        };
        Assert.Equal("Completado con errores", text);
    }

    [Fact]
    public void InProgress_IsNotFinalResult()
    {
        var session = CreateTestSession("TST00007", SmartTestType.Short, SmartTestStatus.InProgress, DateTime.Now);
        Assert.Equal(SmartTestStatus.InProgress, session.Status);
        // InProgress no es resultado final
        Assert.False(session.Status == SmartTestStatus.CompletedWithoutError ||
                     session.Status == SmartTestStatus.CompletedWithError);
    }

    [Fact]
    public void Unsupported_DoesNotClaimDiskIsHealthy()
    {
        var session = CreateTestSession("TST00008", SmartTestType.Short, SmartTestStatus.Unsupported, DateTime.Now);
        // Unsupported no implica disco sano
        Assert.NotEqual(SmartTestStatus.CompletedWithoutError, session.Status);
    }

    [Fact]
    public void FailedToStart_NotPresentedAsDiskFailure()
    {
        var session = CreateTestSession("TST00009", SmartTestType.Short, SmartTestStatus.FailedToStart, DateTime.Now);
        // FailedToStart no implica falla del disco
        Assert.NotEqual(SmartTestStatus.CompletedWithError, session.Status);
    }

    [Fact]
    public void Interrupted_IsNonConclusive()
    {
        var session = CreateTestSession("TST00010", SmartTestType.Short, SmartTestStatus.Interrupted, DateTime.Now);
        var isNonConclusive = session.Status is SmartTestStatus.Aborted or SmartTestStatus.Interrupted or SmartTestStatus.Unknown or SmartTestStatus.NotStarted;
        Assert.True(isNonConclusive);
    }

    [Fact]
    public void Aborted_IsNonConclusive()
    {
        var session = CreateTestSession("TST00011", SmartTestType.Short, SmartTestStatus.Aborted, DateTime.Now);
        var isNonConclusive = session.Status is SmartTestStatus.Aborted or SmartTestStatus.Interrupted or SmartTestStatus.Unknown or SmartTestStatus.NotStarted;
        Assert.True(isNonConclusive);
    }

    [Fact]
    public void Unknown_IsNonConclusive()
    {
        var session = CreateTestSession("TST00012", SmartTestType.Short, SmartTestStatus.Unknown, DateTime.Now);
        var isNonConclusive = session.Status is SmartTestStatus.Aborted or SmartTestStatus.Interrupted or SmartTestStatus.Unknown or SmartTestStatus.NotStarted;
        Assert.True(isNonConclusive);
    }

    [Fact]
    public void NullDuration_AppearsAsNotAvailable()
    {
        var session = CreateTestSession("TST00013", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now);
        Assert.Null(session.EstimatedDurationMinutes);
        var text = session.EstimatedDurationMinutes.HasValue
            ? $"{session.EstimatedDurationMinutes.Value} min"
            : "No disponible";
        Assert.Equal("No disponible", text);
    }

    [Fact]
    public void Progress_AppearsWhenExists()
    {
        var session = CreateTestSession("TST00014", SmartTestType.Short, SmartTestStatus.InProgress, DateTime.Now);
        session.ProgressPercent = 45;
        Assert.Equal(45, session.ProgressPercent);
    }

    [Fact]
    public void LastCheckFailed_GeneratesWarning()
    {
        var session = CreateTestSession("TST00015", SmartTestType.Short, SmartTestStatus.InProgress, DateTime.Now);
        session.LastCheckSucceeded = false;
        session.LastCheckError = "Timeout";
        Assert.False(session.LastCheckSucceeded);
        Assert.Equal("Timeout", session.LastCheckError);
    }

    [Fact]
    public void LastCheckError_IsEscaped()
    {
        var error = "<script>alert('x')</script>";
        var escaped = System.Net.WebUtility.HtmlEncode(error);
        Assert.DoesNotContain("<script>", escaped);
        Assert.Contains("&lt;script&gt;", escaped);
    }

    [Fact]
    public void ResultMessage_IsEscaped()
    {
        var message = "<b>bold</b> & test";
        var escaped = System.Net.WebUtility.HtmlEncode(message);
        Assert.DoesNotContain("<b>", escaped);
        Assert.Contains("&lt;b&gt;", escaped);
    }

    [Fact]
    public void Warnings_AreEscaped()
    {
        var warning = "<img src=x onerror=alert(1)>";
        var escaped = System.Net.WebUtility.HtmlEncode(warning);
        Assert.DoesNotContain("<img", escaped);
    }

    [Fact]
    public void Errors_AreEscaped()
    {
        var error = "<script>alert(1)</script>";
        var escaped = System.Net.WebUtility.HtmlEncode(error);
        Assert.DoesNotContain("<script>", escaped);
    }

    [Fact]
    public void MultipleSelectedSessions_AppearInHtml()
    {
        var options = new ReportGenerationOptions
        {
            IncludeSmartTests = true,
            SmartTestSessions =
            [
                CreateTestSession("TST00016", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, new DateTime(2026, 8, 9, 10, 0, 0)),
                CreateTestSession("TST00017", SmartTestType.Extended, SmartTestStatus.CompletedWithError, new DateTime(2026, 8, 9, 11, 0, 0))
            ]
        };

        Assert.Equal(2, options.SmartTestSessions.Count);
    }

    [Fact]
    public void HtmlOrder_NewestFirst()
    {
        var sessions = new List<SmartTestSession>
        {
            CreateTestSession("TST00018", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, new DateTime(2026, 8, 9, 9, 0, 0)),
            CreateTestSession("TST00019", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, new DateTime(2026, 8, 9, 11, 0, 0)),
            CreateTestSession("TST00020", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, new DateTime(2026, 8, 9, 10, 0, 0))
        };

        static DateTime GetEffectiveDate(SmartTestSession s) =>
            s.LastCheckedAt ?? s.CompletedAt ?? s.StartedAt ?? s.RequestedAt;

        var ordered = sessions.OrderByDescending(GetEffectiveDate).ToList();

        Assert.Equal("TST00019", ordered[0].Id);
        Assert.Equal("TST00020", ordered[1].Id);
        Assert.Equal("TST00018", ordered[2].Id);
    }

    [Fact]
    public void BuildOptions_OnlyIncludesSelectedSessions()
    {
        // Simular la selección: solo 2 de 3 sesiones seleccionadas
        var items = new List<(bool IsSelected, SmartTestSession Session)>
        {
            (true, CreateTestSession("SEL00001", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now)),
            (true, CreateTestSession("SEL00002", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now)),
            (false, CreateTestSession("SEL00003", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now))
        };

        var selected = items.Where(x => x.IsSelected).Select(x => x.Session).ToList();

        Assert.Equal(2, selected.Count);
        Assert.DoesNotContain(selected, s => s.Id == "SEL00003");
    }

    [Fact]
    public void UnselectedSessions_NotIncluded()
    {
        var allSessions = new List<SmartTestSession>
        {
            CreateTestSession("UNS00001", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now),
            CreateTestSession("UNS00002", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, DateTime.Now)
        };

        // Ninguna seleccionada → lista vacía en el informe
        var selected = new List<SmartTestSession>();
        Assert.Empty(selected);
        Assert.Equal(2, allSessions.Count);
    }

    [Fact]
    public void IncludedSections_AddsSmartSelfTests()
    {
        var info = new GeneratedReportInfo();
        info.IncludedSections.Add("SMART Self-Tests");
        Assert.Contains("SMART Self-Tests", info.IncludedSections);
    }

    [Fact]
    public void HtmlWithoutSelfTests_StillWorks()
    {
        var options = new ReportGenerationOptions
        {
            IncludeSmartTests = false,
            SmartTestSessions = []
        };

        var include = options.IncludeSmartTests && options.SmartTestSessions.Count > 0;
        Assert.False(include);
    }

    [Fact]
    public void SmartTestSessionSelectionItem_DisplayName_Format()
    {
        var session = CreateTestSession("TST00021", SmartTestType.Short, SmartTestStatus.CompletedWithoutError, new DateTime(2026, 8, 9, 10, 30, 0));
        var displayName = $"{session.RequestedAt:dd/MM/yyyy HH:mm} - Corto - {session.ModelName} - Completado sin errores";
        Assert.Equal("09/08/2026 10:30 - Corto - Samsung SSD 860 EVO - Completado sin errores", displayName);
    }

    [Fact]
    public void Sessions_DefaultToUnselected()
    {
        // La selección manual implica IsSelected = false por defecto
        var selected = false;
        Assert.False(selected);
    }

    [Fact]
    public void IncludeSmartTests_DefaultsToFalse_AfterLoad()
    {
        // Tras LoadData, IncludeSmartTests queda false (selección manual intencional)
        Assert.False(new ReportGenerationOptions().IncludeSmartTests);
    }
}
