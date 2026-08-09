using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SmartHealthPolicyTests
{
    // =====================
    // Helpers
    // =====================

    private static SmartDiskDevice Device(string type = "sat") =>
        new() { Name = "/dev/sda", InfoName = "/dev/sda", Type = type, ApproximateDiskType = "HDD" };

    private static string Attr(int id, long raw = 0, int value = 100, int worst = 100,
        int thresh = 0, string whenFailed = "", bool prefailure = false)
    {
        return $@"{{ ""id"": {id}, ""name"": ""Attr {id}"", ""value"": {value}, ""worst"": {worst}, " +
               $@"""thresh"": {thresh}, ""raw"": {{ ""value"": {raw} }}, ""when_failed"": ""{whenFailed}"", " +
               $@"""flags"": {{ ""prefailure"": {(prefailure ? "true" : "false")}, ""string"": ""PO--CK "" }} }}";
    }

    private static SmartDiskReport ParseWithAttributes(
        string attributesJson,
        bool? passed = true,
        int? temperature = null,
        SmartctlExitFlags? exitFlags = null)
    {
        var smartStatus = passed.HasValue
            ? $@"""smart_status"": {{ ""passed"": {(passed.Value ? "true" : "false")} }},"
            : string.Empty;
        var temp = temperature.HasValue
            ? $@"""temperature"": {{ ""current"": {temperature} }},"
            : string.Empty;
        var json = $"{{ {smartStatus} {temp} \"ata_smart_attributes\": {{ \"table\": [ {attributesJson} ] }} }}";

        return SmartctlParser.ParseSmartJson(json, Device(), "smartctl 7.4", exitFlags);
    }

    private static SmartDiskReport ParseSingle(int id, long raw = 0, int value = 100, int worst = 100,
        int thresh = 0, string whenFailed = "", bool prefailure = false, bool? passed = true,
        int? temperature = null, SmartctlExitFlags? exitFlags = null)
    {
        return ParseWithAttributes(Attr(id, raw, value, worst, thresh, whenFailed, prefailure),
            passed, temperature, exitFlags);
    }

    private static readonly string RealNvmeJson = @"{
        ""smart_status"": { ""passed"": true },
        ""nvme_smart_health_information_log"": {
            ""critical_warning"": 0,
            ""temperature"": 35,
            ""available_spare"": 100,
            ""available_spare_threshold"": 10,
            ""percentage_used"": 13,
            ""unsafe_shutdowns"": 4,
            ""media_errors"": 0
        }
    }";

    private static SmartDiskReport ParseNvme(string nvmeSectionJson, bool? passed = true,
        SmartctlExitFlags? exitFlags = null)
    {
        var smartStatus = passed.HasValue
            ? $@"""smart_status"": {{ ""passed"": {(passed.Value ? "true" : "false")} }},"
            : string.Empty;
        var json = $"{{ {smartStatus} {nvmeSectionJson} }}";
        return SmartctlParser.ParseSmartJson(json, Device("nvme"), "smartctl 7.4", exitFlags);
    }

    // =====================
    // Defaults / Overall (criterios 1-10)
    // =====================

    [Fact]
    public void NewReport_DefaultUnknown()
    {
        Assert.Equal(SmartHealthStatus.Unknown, new SmartDiskReport().HealthStatus);
    }

    [Fact]
    public void OverallHealthPassed_DefaultNull()
    {
        Assert.Null(new SmartDiskReport().OverallHealthPassed);
    }

    [Fact]
    public void PassedTrue_NoFindings_Good()
    {
        var report = ParseSingle(9, raw: 100);

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
        Assert.True(report.OverallHealthPassed);
    }

    [Fact]
    public void PassedFalse_Critical()
    {
        var report = ParseSingle(9, raw: 100, passed: false);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void PassedFalse_BackupTrue()
    {
        var report = ParseSingle(9, raw: 100, passed: false);

        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void NoSmartStatus_NotCriticalAutomatically()
    {
        var report = ParseSingle(9, raw: 100, passed: null);

        Assert.NotEqual(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void NoSmartStatus_NoOtherEvidence_Unknown()
    {
        var report = ParseSingle(9, raw: 100, passed: null);

        Assert.Equal(SmartHealthStatus.Unknown, report.HealthStatus);
        Assert.Contains("no concluyente", report.HealthSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidJson_Unknown_Unsuccessful()
    {
        var report = SmartctlParser.ParseSmartJson("not json", Device(), "smartctl 7.4");

        Assert.False(report.IsAnalysisSuccessful);
        Assert.Equal(SmartHealthStatus.Unknown, report.HealthStatus);
    }

    [Fact]
    public void Critical_CanBeAnalysisSuccessful()
    {
        var report = ParseSingle(5, raw: 20, passed: true);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.True(report.IsAnalysisSuccessful);
    }

    [Fact]
    public void Warning_CanBeAnalysisSuccessful()
    {
        var report = ParseSingle(199, raw: 3, passed: true);

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.True(report.IsAnalysisSuccessful);
    }

    // =====================
    // Reglas de threshold normalizado (criterios 11-19)
    // =====================

    [Fact]
    public void RawValueGtThreshold_Alone_NotCritical()
    {
        // THRESH aplica al valor NORMALIZADO; la comparación genérica raw > thresh
        // fue eliminada. Un atributo sin política CATTECH por raw queda Info.
        var report = ParseSingle(9, raw: 999, value: 100, worst: 100, thresh: 10);

        Assert.Equal(SmartSeverity.Info, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void ValueLeThreshold_Prefailure_Critical()
    {
        var report = ParseSingle(5, raw: 0, value: 5, worst: 5, thresh: 10, prefailure: true);

        Assert.Equal(SmartSeverity.Critical, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void ValueLeThreshold_NoPrefailure_Warning()
    {
        var report = ParseSingle(5, raw: 0, value: 5, worst: 5, thresh: 10, prefailure: false);

        Assert.Equal(SmartSeverity.Warning, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void WhenFailedNow_Prefailure_Critical()
    {
        var report = ParseSingle(5, raw: 0, value: 100, thresh: 10, whenFailed: "now", prefailure: true);

        Assert.Equal(SmartSeverity.Critical, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void WhenFailedNow_Usage_Warning()
    {
        var report = ParseSingle(5, raw: 0, value: 100, thresh: 10, whenFailed: "now", prefailure: false);

        Assert.Equal(SmartSeverity.Warning, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void WhenFailedPast_Warning()
    {
        var report = ParseSingle(5, raw: 0, value: 100, thresh: 10, whenFailed: "past", prefailure: true);

        Assert.Equal(SmartSeverity.Warning, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void WorstLeThreshold_Historical_Warning_NotCritical()
    {
        // Fallo histórico: worst bajo pero valor recuperado → Warning como máximo
        var report = ParseSingle(5, raw: 0, value: 100, worst: 5, thresh: 10, prefailure: true);

        Assert.Equal(SmartSeverity.Warning, report.ImportantAttributes[0].Severity);
        Assert.NotEqual(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void PrefailureFlag_ParsedFromBoolJson()
    {
        var report = ParseSingle(5, raw: 0, value: 5, worst: 5, thresh: 10, prefailure: true);

        Assert.True(report.ImportantAttributes[0].IsPrefailure);
    }

    [Fact]
    public void FlagsString_StillPreserved()
    {
        var report = ParseSingle(5, raw: 0, value: 100, thresh: 10, prefailure: true);

        Assert.Equal("PO--CK ", report.ImportantAttributes[0].Flags);
    }

    // =====================
    // Política CATTECH ATA (criterios 20-37)
    // =====================

    [Theory]
    [InlineData(5, 0, SmartSeverity.Info)]
    [InlineData(5, 1, SmartSeverity.Warning)]
    [InlineData(5, 10, SmartSeverity.Warning)]
    [InlineData(5, 11, SmartSeverity.Critical)]
    public void Id5_PolicyByRaw(int id, long raw, SmartSeverity expected)
    {
        var report = ParseSingle(id, raw: raw);

        Assert.Equal(expected, report.ImportantAttributes[0].Severity);
    }

    [Theory]
    [InlineData(197, 1, SmartSeverity.Warning)]
    [InlineData(197, 5, SmartSeverity.Warning)]
    [InlineData(197, 6, SmartSeverity.Critical)]
    public void Id197_PolicyByRaw(int id, long raw, SmartSeverity expected)
    {
        var report = ParseSingle(id, raw: raw);

        Assert.Equal(expected, report.ImportantAttributes[0].Severity);
    }

    [Theory]
    [InlineData(198, 1, SmartSeverity.Warning)]
    [InlineData(198, 5, SmartSeverity.Warning)]
    [InlineData(198, 6, SmartSeverity.Critical)]
    public void Id198_PolicyByRaw(int id, long raw, SmartSeverity expected)
    {
        var report = ParseSingle(id, raw: raw);

        Assert.Equal(expected, report.ImportantAttributes[0].Severity);
    }

    [Fact]
    public void Id5_Critical_SetsBackup()
    {
        var report = ParseSingle(5, raw: 11);

        Assert.True(report.RequiresBackupRecommendation);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Id199_AlwaysWarning_NeverCriticalByRaw(int raw)
    {
        var report = ParseSingle(199, raw: raw);

        Assert.Equal(SmartSeverity.Warning, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.NotEqual(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void CrcOnly_NoBackupRecommendation()
    {
        var report = ParseSingle(199, raw: 500);

        Assert.False(report.RequiresBackupRecommendation);
        Assert.Contains(report.Warnings, w => w.Contains("CRC", StringComparison.OrdinalIgnoreCase) ||
                                              w.Contains("interfaz", StringComparison.OrdinalIgnoreCase) ||
                                              w.Contains("Atributo a revisar", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(187)]
    [InlineData(188)]
    public void Id187And188_RawPositive_Warning(int id)
    {
        var report = ParseSingle(id, raw: 1);

        Assert.Equal(SmartSeverity.Warning, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Id1And3_HugeRaw_Info(int id)
    {
        var report = ParseSingle(id, raw: 999999);

        Assert.Equal(SmartSeverity.Info, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    // =====================
    // SSD vendor-specific (criterios 38-42)
    // =====================

    [Theory]
    [InlineData(231, 4)]
    [InlineData(231, 99)]
    [InlineData(233, 99)]
    [InlineData(177, 999)]
    public void VendorSsdRaw_DoesNotGovernHealth(int id, long raw)
    {
        var report = ParseSingle(id, raw: raw);

        Assert.Equal(SmartSeverity.Info, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void VendorSsdAttr_PrefailureNow_Critical()
    {
        // Regla oficial: when_failed=now + prefailure → Critical (incluso en vendor attr)
        var report = ParseSingle(231, raw: 4, value: 3, worst: 3, thresh: 10,
            whenFailed: "now", prefailure: true);

        Assert.Equal(SmartSeverity.Critical, report.ImportantAttributes[0].Severity);
        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    // =====================
    // Temperatura (criterios 43-49)
    // =====================

    [Theory]
    [InlineData(50, SmartHealthStatus.Good)]
    [InlineData(55, SmartHealthStatus.Good)]
    [InlineData(56, SmartHealthStatus.Warning)]
    [InlineData(65, SmartHealthStatus.Warning)]
    [InlineData(66, SmartHealthStatus.Critical)]
    public void Temperature_Policy(int temp, SmartHealthStatus expected)
    {
        var report = ParseSingle(9, raw: 100, temperature: temp);

        Assert.Equal(expected, report.HealthStatus);
    }

    [Fact]
    public void TemperatureCritical_BackupTrue()
    {
        var report = ParseSingle(9, raw: 100, temperature: 70);

        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Id194_PackedRaw_NotInterpretedAsTemperature()
    {
        // Sin temperature.current top-level, el raw del ID 194 NO se usa como temperatura
        var report = ParseSingle(194, raw: 999999999);

        Assert.Equal(0, report.TemperatureCelsius);
    }

    // =====================
    // NVMe (criterios 50-68)
    // =====================

    [Fact]
    public void NvmeLog_Principal_IsParsed()
    {
        var report = SmartctlParser.ParseSmartJson(RealNvmeJson, Device("nvme"), "smartctl 7.4");

        Assert.True(report.IsAnalysisSuccessful);
        Assert.Equal(13, report.NvmePercentageUsed);
        Assert.Equal(0, report.NvmeCriticalWarning);
        Assert.Equal(100, report.NvmeAvailableSpare);
        Assert.Equal(10, report.NvmeAvailableSpareThreshold);
        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void NvmeLegacyName_Fallback_IsParsed()
    {
        var legacy = RealNvmeJson.Replace("nvme_smart_health_information_log", "nvme_smart_health_information");
        var report = SmartctlParser.ParseSmartJson(legacy, Device("nvme"), "smartctl 7.4");

        Assert.Equal(13, report.NvmePercentageUsed);
        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void CriticalWarning_Numeric0_Parsed()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""critical_warning"": 0 }");

        Assert.Equal(0, report.NvmeCriticalWarning);
    }

    [Fact]
    public void CriticalWarning_Numeric1_Parsed()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""critical_warning"": 1 }");

        Assert.Equal(1, report.NvmeCriticalWarning);
    }

    [Fact]
    public void CriticalWarning0_PassedTrue_NotCritical()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""critical_warning"": 0, ""media_errors"": 0, ""percentage_used"": 5 }");

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void CriticalWarningNonZero_Critical()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""critical_warning"": 1, ""media_errors"": 0, ""percentage_used"": 5 }");

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void NvmeCriticalWarning_Preserved()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""critical_warning"": 3 }");

        Assert.Equal(3, report.NvmeCriticalWarning);
    }

    [Fact]
    public void AvailableSpare_Preserved()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""available_spare"": 95 }");

        Assert.Equal(95, report.NvmeAvailableSpare);
    }

    [Fact]
    public void AvailableSpareThreshold_Preserved()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""available_spare_threshold"": 10 }");

        Assert.Equal(10, report.NvmeAvailableSpareThreshold);
    }

    [Fact]
    public void SpareLeThreshold_Warning()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""available_spare"": 8, ""available_spare_threshold"": 10, ""critical_warning"": 0, ""media_errors"": 0 }");

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void SpareGtThreshold_NoSpareWarning()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""available_spare"": 95, ""available_spare_threshold"": 10, ""critical_warning"": 0, ""media_errors"": 0 }");

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void Percentage79_NoEarlyWarning()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""percentage_used"": 79, ""critical_warning"": 0, ""media_errors"": 0 }");

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void Percentage80_Warning()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""percentage_used"": 80, ""critical_warning"": 0, ""media_errors"": 0 }");

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void Percentage100_Warning_NotCriticalOnlyByPercentage()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""percentage_used"": 100, ""critical_warning"": 0, ""media_errors"": 0 }");

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.NotEqual(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void PercentageGt100_PreservedNoClamp()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""percentage_used"": 120, ""critical_warning"": 0, ""media_errors"": 0 }");

        Assert.Equal(120, report.NvmePercentageUsed);
    }

    [Fact]
    public void MediaErrors1_Critical()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""media_errors"": 1, ""critical_warning"": 0 }");

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void MediaErrors1_BackupTrue()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""media_errors"": 1, ""critical_warning"": 0 }");

        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void UnsafeShutdowns_NoWarningByItself()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""unsafe_shutdowns"": 42, ""critical_warning"": 0, ""media_errors"": 0, ""percentage_used"": 5 }");

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
        Assert.Equal(42, report.NvmeUnsafeShutdowns);
    }

    [Fact]
    public void NvmeRealJson_NoGetStringException()
    {
        // critical_warning numérico: no debe lanzar InvalidOperationException de GetString
        var report = SmartctlParser.ParseSmartJson(RealNvmeJson, Device("nvme"), "smartctl 7.4");

        Assert.True(report.IsAnalysisSuccessful);
    }

    // =====================
    // Exit flags health (criterios 69-78)
    // =====================

    [Fact]
    public void Bit3_SmartStatusFailed_Critical()
    {
        var report = ParseSingle(9, raw: 100, passed: true,
            exitFlags: SmartctlExitFlags.SmartStatusFailed);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Bit4_PrefailThreshold_Critical()
    {
        var report = ParseSingle(9, raw: 100, passed: true,
            exitFlags: SmartctlExitFlags.PrefailAttributeThreshold);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.True(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Bit5_PastUsage_Warning()
    {
        var report = ParseSingle(9, raw: 100, passed: true,
            exitFlags: SmartctlExitFlags.PastOrUsageAttributeFailure);

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void Bit6_ErrorLog_Warning_NoBackup()
    {
        var report = ParseSingle(9, raw: 100, passed: true,
            exitFlags: SmartctlExitFlags.ErrorLogContainsErrors);

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Bit7_SelfTestLog_Warning_NoBackup()
    {
        var report = ParseSingle(9, raw: 100, passed: true,
            exitFlags: SmartctlExitFlags.SelfTestLogContainsErrors);

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void Bits3Plus6_Critical()
    {
        var report = ParseSingle(9, raw: 100, passed: true,
            exitFlags: SmartctlExitFlags.SmartStatusFailed | SmartctlExitFlags.ErrorLogContainsErrors);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void Bit2_Unknown_Unsuccessful_Precedence()
    {
        // Precedencia operativa: bit 2 nunca deja Good/Warning/Critical
        var report = ParseSingle(5, raw: 0, passed: true,
            exitFlags: SmartctlExitFlags.SmartCommandOrChecksumError);

        Assert.Equal(SmartHealthStatus.Unknown, report.HealthStatus);
        Assert.False(report.IsAnalysisSuccessful);
    }

    [Fact]
    public async Task SmartDiskService_PassesHealthBits_ToPolicy()
    {
        // Integración: exit 8 con JSON válido → la política ve el bit 3 → Critical
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult
            {
                ExitCode = 8,
                StandardOutput = ValidSmartJson()
            }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(new SmartDiskDevice
        {
            Name = "/dev/sda",
            InfoName = "/dev/sda",
            Type = "sat"
        });

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    private sealed class FakeSmartctlRunner : ISmartctlRunner
    {
        public SmartctlCommandResult? AnalyzeResponse { get; set; }

        public Task<SmartctlAvailability> CheckAvailabilityAsync() =>
            Task.FromResult(new SmartctlAvailability
            {
                IsAvailable = true,
                SmartctlPath = "C:\\mock\\smartctl.exe",
                Version = "smartctl 7.4",
                SupportsJson = true
            });

        public Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout)
            => Task.FromResult(AnalyzeResponse ?? new SmartctlCommandResult());

        public Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync()
            => Task.FromResult<IReadOnlyList<SmartDiskDevice>>(new List<SmartDiskDevice>());
    }

    private static string ValidSmartJson() => @"{
        ""smart_status"": { ""passed"": true },
        ""ata_smart_attributes"": { ""table"": [] },
        ""temperature"": { ""current"": 40 }
    }";

    // =====================
    // Prioridad (criterios 79-86)
    // =====================

    [Fact]
    public void PassedTrue_WarningAttr_Warning()
    {
        var report = ParseSingle(197, raw: 2, passed: true);

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
    }

    [Fact]
    public void PassedTrue_CriticalAttr_Critical()
    {
        var report = ParseSingle(5, raw: 20, passed: true);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void WarningPlusCritical_Critical()
    {
        var report = ParseWithAttributes(
            $"{Attr(199, raw: 5)}, {Attr(5, raw: 20)}", passed: true);

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void Percentage80_PlusMediaError_Critical()
    {
        var report = ParseNvme(@"""nvme_smart_health_information_log"": { ""percentage_used"": 80, ""media_errors"": 2, ""critical_warning"": 0 }");

        Assert.Equal(SmartHealthStatus.Critical, report.HealthStatus);
    }

    [Fact]
    public void CrcWarning_PassedTrue_Warning()
    {
        var report = ParseSingle(199, raw: 3, passed: true);

        Assert.Equal(SmartHealthStatus.Warning, report.HealthStatus);
        Assert.False(report.RequiresBackupRecommendation);
    }

    [Fact]
    public void NoFindings_PassedTrue_Good()
    {
        var report = ParseSingle(9, raw: 100, passed: true);

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }

    [Fact]
    public void NoFindings_PassedNull_Unknown()
    {
        var report = ParseSingle(9, raw: 100, passed: null);

        Assert.Equal(SmartHealthStatus.Unknown, report.HealthStatus);
    }

    [Fact]
    public void TechnicalError_DoesNotConvertToCritical()
    {
        // La política no usa report.Errors como señal de salud física
        var report = ParseSingle(9, raw: 100, passed: true);
        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);

        report.Errors.Add("Error técnico de metadata (no relacionado con salud)");
        SmartHealthPolicy.EvaluateOverallHealth(report, null);

        Assert.Equal(SmartHealthStatus.Good, report.HealthStatus);
    }
}
