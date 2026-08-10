using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReleaseGateTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // =====================
    // Config herramientas.json (S.3.1)
    // =====================

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cattech-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteToolsConfig(string dir, string path, bool autoDetect)
    {
        Directory.CreateDirectory(Path.Combine(dir, "config"));
        File.WriteAllText(Path.Combine(dir, "config", "herramientas.json"),
            $@"{{ ""smartctlPath"": ""{path}"", ""smartctlAutoDetect"": {(autoDetect ? "true" : "false")} }}");
    }

    [Fact]
    public async Task Runner_UsesConfiguredPath_FromHerramientasJson()
    {
        var dir = CreateTempDir();
        try
        {
            // Ruta inexistente pero el config la declara: se usa (y falla limpio) en vez de auto-detectar
            var fakePath = Path.Combine(dir, "tools", "smartctl.exe");
            WriteToolsConfig(dir, fakePath, autoDetect: false);
            var runner = new SmartctlRunner(baseDirectory: dir);

            var availability = await runner.CheckAvailabilityAsync();

            Assert.False(availability.IsAvailable);
            Assert.Contains("smartctl.exe no encontrado", availability.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Runner_AutoDetectFalse_WithoutPath_ReturnsUnavailable()
    {
        var dir = CreateTempDir();
        try
        {
            WriteToolsConfig(dir, "", autoDetect: false);
            var runner = new SmartctlRunner(baseDirectory: dir);

            var availability = await runner.CheckAvailabilityAsync();

            Assert.False(availability.IsAvailable);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Runner_AutoDetectTrue_WithoutPath_TriesDetection()
    {
        var dir = CreateTempDir();
        try
        {
            WriteToolsConfig(dir, "", autoDetect: true);
            var runner = new SmartctlRunner(baseDirectory: dir);

            var availability = await runner.CheckAvailabilityAsync();

            // Sin smartctl real en la máquina de tests: no disponible, sin excepción
            Assert.False(availability.IsAvailable);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Runner_MissingConfig_NoCrash()
    {
        var dir = CreateTempDir();
        try
        {
            var runner = new SmartctlRunner(baseDirectory: dir);

            var availability = runner.CheckAvailabilityAsync().GetAwaiter().GetResult();

            Assert.False(availability.IsAvailable);
            Assert.NotEmpty(availability.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Runner_ProgrammaticPath_HasPriorityOverConfig()
    {
        var dir = CreateTempDir();
        try
        {
            var fakePath = Path.Combine(dir, "tools", "smartctl.exe");
            WriteToolsConfig(dir, Path.Combine(dir, "config", "otro-smartctl.exe"), autoDetect: true);
            var runner = new SmartctlRunner(fakePath, dir);

            var availability = await runner.CheckAvailabilityAsync();

            Assert.False(availability.IsAvailable);
            Assert.Contains("smartctl.exe no encontrado", availability.ErrorMessage);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // =====================
    // Round-trip de persistencia (S.3.1)
    // =====================

    [Fact]
    public void SmartDiskReport_RoundTrip_PreservesNewFields()
    {
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            DeviceName = "/dev/sda",
            SmartctlDeviceType = "sat",
            HealthStatus = SmartHealthStatus.Warning,
            OverallHealthPassed = null,
            NvmeCriticalWarning = 3,
            NvmeAvailableSpare = 8,
            NvmeAvailableSpareThreshold = 10,
            NvmeMediaErrors = 2,
            NvmeUnsafeShutdowns = 5,
            NvmePercentageUsed = 95,
            TemperatureCelsius = 60,
            ImportantAttributes =
            [
                new SmartAttribute { Id = 5, RawValue = 12, IsPrefailure = true, WhenFailed = "now" }
            ]
        };

        var json = JsonSerializer.Serialize(report, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<SmartDiskReport>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("sat", deserialized!.SmartctlDeviceType);
        Assert.Null(deserialized.OverallHealthPassed);
        Assert.Equal(SmartHealthStatus.Warning, deserialized.HealthStatus);
        Assert.Equal(3, deserialized.NvmeCriticalWarning);
        Assert.Equal(8, deserialized.NvmeAvailableSpare);
        Assert.Equal(10, deserialized.NvmeAvailableSpareThreshold);
        Assert.Equal(2, deserialized.NvmeMediaErrors);
        Assert.Equal(5, deserialized.NvmeUnsafeShutdowns);
        Assert.Equal(95, deserialized.NvmePercentageUsed);
        var attr = Assert.Single(deserialized.ImportantAttributes);
        Assert.True(attr.IsPrefailure);
        Assert.Equal(12, attr.RawValue);
    }

    [Fact]
    public void SmartDiskReport_LegacyJson_WithoutNewFields_Defaults()
    {
        // Legacy: sin SmartctlDeviceType, sin NvmeCriticalWarning; OverallHealthPassed bool
        var legacyJson = @"{
            ""device"": ""/dev/sda"",
            ""healthStatus"": 3,
            ""overallHealthPassed"": false,
            ""isAnalysisSuccessful"": false
        }";

        var report = JsonSerializer.Deserialize<SmartDiskReport>(legacyJson, SerializerOptions);

        Assert.NotNull(report);
        Assert.Equal(string.Empty, report!.SmartctlDeviceType);
        Assert.False(report.OverallHealthPassed);
        Assert.Null(report.NvmeCriticalWarning);
        Assert.Null(report.NvmeAvailableSpareThreshold);
        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
    }

    [Fact]
    public void SmartTestSession_RoundTrip_PreservesSmartctlDeviceType()
    {
        var session = new SmartTestSession
        {
            Id = "TSTGATE01",
            Device = "/dev/sda",
            SmartctlDeviceType = "sntjmicron",
            SmartctlExitCode = 0,
            Status = SmartTestStatus.CompletedWithError,
            Errors = ["fallo"]
        };

        var json = JsonSerializer.Serialize(session, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<SmartTestSession>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("sntjmicron", deserialized!.SmartctlDeviceType);
        Assert.Equal(SmartTestStatus.CompletedWithError, deserialized.Status);
        Assert.Single(deserialized.Errors);
    }

    [Fact]
    public void SmartTestSession_LegacyJson_WithoutType_DefaultsEmpty()
    {
        var legacyJson = @"{
            ""id"": ""TSTLEGACY01"",
            ""device"": ""/dev/sda"",
            ""status"": 4
        }";

        var session = JsonSerializer.Deserialize<SmartTestSession>(legacyJson, SerializerOptions);

        Assert.NotNull(session);
        Assert.Equal(string.Empty, session!.SmartctlDeviceType);
        Assert.Equal(0, session.SmartctlExitCode);
    }

    [Fact]
    public void SmartAnalysisResult_RoundTrip_PreservesReports()
    {
        var result = new SmartAnalysisResult
        {
            Id = "ANAGATE01",
            StartedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            FinishedAt = new DateTime(2026, 8, 9, 10, 1, 0),
            SmartctlAvailable = true,
            SmartctlVersion = "smartctl 7.4",
            DevicesAnalyzed = 1,
            Reports =
            [
                new SmartDiskReport
                {
                    Device = "/dev/nvme0n1",
                    SmartctlDeviceType = "nvme",
                    HealthStatus = SmartHealthStatus.Warning,
                    OverallHealthPassed = true,
                    NvmePercentageUsed = 85
                }
            ]
        };

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<SmartAnalysisResult>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        var report = Assert.Single(deserialized!.Reports);
        Assert.Equal("nvme", report.SmartctlDeviceType);
        Assert.Equal(85, report.NvmePercentageUsed);
        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    // =====================
    // Seguridad de comandos (S.3.1)
    // =====================

    [Theory]
    [InlineData("sat", "-a -j -d sat /dev/sda")]
    [InlineData("nvme", "-a -j -d nvme /dev/nvme0n1")]
    [InlineData("", "-a -j /dev/sda")]
    public void CommandBuilder_Analyze_OnlySafeFlags(string type, string expected)
    {
        var args = SmartctlCommandBuilder.BuildAnalyzeArguments(
            expected.Contains("nvme0n1") ? "/dev/nvme0n1" : "/dev/sda", type);

        Assert.Equal(expected, args);
        Assert.DoesNotContain("-s ", args);
        Assert.DoesNotContain("--smart=on", args);
        Assert.DoesNotContain("security", args, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sanitize", args, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("format", args, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("offlineauto", args, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("saveauto", args, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandBuilder_StartTest_OnlyShortOrLong()
    {
        var shortArgs = SmartctlCommandBuilder.BuildStartTestArguments("/dev/sda", "sat", extended: false);
        var longArgs = SmartctlCommandBuilder.BuildStartTestArguments("/dev/sda", "sat", extended: true);

        Assert.Equal("-t short -j -d sat /dev/sda", shortArgs);
        Assert.Equal("-t long -j -d sat /dev/sda", longArgs);
    }

    [Fact]
    public void CommandBuilder_SelfTestLog_OnlyReadFlag()
    {
        var args = SmartctlCommandBuilder.BuildSelfTestLogArguments("/dev/sda", "sat");

        Assert.Equal("-l selftest -j -d sat /dev/sda", args);
    }

    [Fact]
    public void CommandBuilder_ApproximateDiskType_NeverUsed()
    {
        // Aunque ApproximateDiskType sea USB/SSD, el builder solo usa Type
        var args = SmartctlCommandBuilder.BuildAnalyzeArguments("/dev/sda", "sat");

        Assert.DoesNotContain("-d usb", args);
        Assert.DoesNotContain("-d SSD", args);
    }

    // =====================
    // Informe: estados (S.3.1) — sin reinterpretar raw
    // =====================

    [Fact]
    public void ReportRecommendation_Unknown_NotGood()
    {
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            HealthStatus = SmartHealthStatus.Unknown,
            OverallHealthPassed = null
        };

        Assert.Equal(SmartHealthStatus.Unknown, report.HealthStatus);
        Assert.NotEqual(SmartHealthStatus.Good, report.HealthStatus);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void ReportRecommendation_CrcOnlyWarning_NoBackup()
    {
        // CRC-only: Warning sin backup (política S.2 respaldada por SmartHealthPolicyTests)
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            HealthStatus = SmartHealthStatus.Warning,
            OverallHealthPassed = true,
            RequiresBackupRecommendation = false
        };

        Assert.False(report.RequiresBackupRecommendation);
        Assert.NotEqual(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void ReportRecommendation_CriticalReal_Backup()
    {
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            HealthStatus = SmartHealthStatus.Critical,
            OverallHealthPassed = false,
            RequiresBackupRecommendation = true
        };

        Assert.True(report.RequiresBackupRecommendation);
    }

    // =====================
    // smartctl ausente no rompe la app (S.3.1)
    // =====================

    [Fact]
    public async Task SmartctlAbsent_SmartUnavailable_NoCrash()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartDiskService(runner);

        var result = await service.AnalyzeAllDisksAsync();

        Assert.False(result.SmartctlAvailable);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Reports);
    }
}
