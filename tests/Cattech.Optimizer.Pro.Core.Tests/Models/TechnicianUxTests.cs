using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Core.Models.Repair;
using Cattech.Optimizer.Pro.Infrastructure.Repair;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// UX para técnicos avanzados: funciones puras de copia/exportación de
/// Diagnóstico, Hardware, Pantallas azules y Reparaciones. Sin Clipboard,
/// sin diálogos y sin hardware real: solo contenido generado por los ViewModels.
/// </summary>
public class TechnicianUxTests
{
    // =====================
    // Diagnóstico rápido
    // =====================

    private static DiagnosticReport CompleteReport() => new()
    {
        OsName = "Windows 11 Pro",
        WindowsEdition = "23H2",
        Architecture = "x64",
        ComputerName = "QA-EQUIPO",
        CurrentUser = "tecnico",
        Processor = "AMD Ryzen 7 5700X",
        CpuCores = 8,
        RamTotalGB = 16,
        RamUsedGB = 8,
        RamAvailableGB = 8,
        RamUsagePercent = 50,
        PrimaryDiskName = "Samsung SSD 980 PRO",
        DiskType = "NVMe",
        DiskCapacityGB = 931,
        DiskFreeGB = 450,
        DiskFreePercent = 48.3,
        DiskFreeSpaceKnown = true,
        SystemVolumeLetter = "C:",
        Startup = new StartupInfo { TotalCount = 10, ThirdPartyCount = 3 },
        TempFiles = new TempFilesInfo
        {
            TotalSizeBytes = 2L * 1024 * 1024 * 1024,
            FileCount = 120,
            FolderCount = 2
        },
        Security = new SecurityInfo
        {
            AntivirusName = "Defender",
            AntivirusActive = true,
            FirewallActive = true,
            WindowsUpdateStatus = "Servicio activo"
        },
        VirtualMemory = new VirtualMemoryInfo
        {
            PagingFileSizeGB = 4,
            IsAutoManaged = true,
            Location = @"C:\pagefile.sys"
        },
        TechnicianName = "Técnico QA"
    };

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void DiagnosticReport_LegacyJsonWithoutErrors_DeserializesEmptyLists()
    {
        // JSON de v0.3.1 (sin errors/warnings): campo aditivo, no rompe lectura.
        const string legacyJson = """
        {
          "id": "AB12CD34",
          "osName": "Windows 10 Pro",
          "ramTotalGB": 8,
          "alerts": []
        }
        """;

        var report = JsonSerializer.Deserialize<DiagnosticReport>(legacyJson, CamelCaseOptions);

        Assert.NotNull(report);
        Assert.Equal("Windows 10 Pro", report!.OsName);
        Assert.NotNull(report.Errors);
        Assert.NotNull(report.Warnings);
        Assert.Empty(report.Errors);
        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void DiagnosticReport_ErrorsAndWarnings_RoundTripInJson()
    {
        var report = CompleteReport();
        report.Errors.Add("Inicio: falló la lectura de la configuración de arranque.");
        report.Warnings.Add("Seguridad: no se pudo consultar FirewallProduct (WMI).");

        var json = QuickDiagnosticViewModel.BuildJson(report);

        Assert.Contains("\"errors\"", json);
        Assert.Contains("\"warnings\"", json);
        // System.Text.Json escapa no-ASCII; el contenido se valida al deserializar.
        Assert.Contains("Inicio: fall", json);

        var back = JsonSerializer.Deserialize<DiagnosticReport>(json, CamelCaseOptions);
        Assert.NotNull(back);
        Assert.Single(back!.Errors);
        Assert.Single(back.Warnings);
        Assert.Contains("Inicio: falló la lectura", back.Errors[0]);
        Assert.Equal("Windows 11 Pro", back.OsName);
    }

    [Fact]
    public void BuildJson_UsesCamelCaseAndIndentation()
    {
        var report = CompleteReport();

        var json = QuickDiagnosticViewModel.BuildJson(report);

        Assert.Contains("\"osName\"", json);
        Assert.DoesNotContain("\"OsName\"", json);
        Assert.Contains("\n", json);
        Assert.Contains("  \"", json);
    }

    [Fact]
    public void BuildSourceIssues_MapsReportErrorsAndWarnings()
    {
        var report = CompleteReport();
        report.Errors.Add("Inicio: falló la lectura de la configuración de arranque.");
        report.Warnings.Add("Seguridad: no se pudo consultar FirewallProduct (WMI).");

        var issues = QuickDiagnosticViewModel.BuildSourceIssues(report);

        Assert.Equal(2, issues.Count);
        Assert.Equal(DiagnosticIssueSeverity.Error, issues[0].Severity);
        Assert.Equal("ERROR", issues[0].SeverityText);
        Assert.Equal("Inicio", issues[0].Source);
        Assert.Equal(DiagnosticIssueSeverity.Warning, issues[1].Severity);
        Assert.Equal("ADVERTENCIA", issues[1].SeverityText);
        Assert.Equal("Seguridad", issues[1].Source);
    }

    [Fact]
    public void BuildSummaryText_WhenSourceFailed_KeepsRestAndMarksPartial()
    {
        var report = CompleteReport();
        report.Errors.Add("Inicio: falló la lectura de la configuración de arranque.");
        report.Warnings.Add("Seguridad: no se pudo consultar FirewallProduct (WMI).");
        report.Alerts.Add(new DiagnosticAlert
        {
            Severity = AlertSeverity.Warning,
            Category = "RAM",
            Message = "RAM justa: 8 GB",
            Recommendation = "Alinear con el uso real."
        });

        var summary = QuickDiagnosticViewModel.BuildSummaryText(report);

        // El fallo de una fuente no descarta el resto del reporte.
        Assert.Contains("Windows 11 Pro", summary);
        Assert.Contains("AMD Ryzen 7 5700X", summary);
        Assert.Contains("Estado: Diagnóstico parcial", summary);
        Assert.Contains("ERRORES DE FUENTE (1)", summary);
        Assert.Contains("Inicio: falló la lectura", summary);
        Assert.Contains("ADVERTENCIAS DE FUENTE (1)", summary);
        Assert.Contains("Seguridad: no se pudo consultar", summary);
        // Alertas en formato TSV con encabezado.
        Assert.Contains("SEVERIDAD\tCATEGORIA\tMENSAJE\tRECOMENDACION", summary);
        Assert.Contains("RAM\tRAM justa: 8 GB", summary);
    }

    [Fact]
    public void BuildSummaryText_WithoutIssues_ReportsComplete()
    {
        var summary = QuickDiagnosticViewModel.BuildSummaryText(CompleteReport());

        Assert.Contains("Estado: Completo", summary);
        Assert.Contains("ERRORES DE FUENTE (0)", summary);
        Assert.Contains("- (sin registros)", summary);
        Assert.Contains("Capacidad: 931 GB", summary);
    }

    [Fact]
    public void ResolveDiagnosticsDirectory_IsRelativeToBaseDirectory()
    {
        var expected = Path.Combine(@"C:\CATTECH", "data", "diagnostics");

        var resolved = QuickDiagnosticViewModel.ResolveDiagnosticsDirectory(@"C:\CATTECH");

        Assert.Equal(expected, resolved);
    }

    // =====================
    // Hardware: copiar tabla / exportar CSV
    // =====================

    private static HardwareViewModel CreatePopulatedHardwareViewModel()
    {
        // Los builders son puros: no tocan los servicios inyectados.
        var vm = new HardwareViewModel(null!, null!)
        {
            LastUpdatedAt = new DateTime(2026, 11, 22, 10, 30, 0),
            StatusText = "Lectura disponible",
            ProviderStatusText = "Disponible",
            AdminText = "No",
            InventoryStatusText = "Inventario actualizado",
            Cpu = new CpuInfo
            {
                Name = "AMD Ryzen 7 5700X",
                Manufacturer = "AMD",
                Cores = 8,
                Threads = 16,
                BaseSpeedGHz = 3.4
            },
            Memory = new MemoryInfo
            {
                TotalGB = 16,
                AvailableGB = 8,
                SpeedMHz = 3200,
                Type = "DDR4",
                SlotsUsed = 2,
                SlotsTotal = 4
            },
            Motherboard = new MotherboardInfo
            {
                Manufacturer = "ASUS",
                Model = "PRIME B550",
                BiosVersion = "2806"
            }
        };

        vm.TemperatureSensors.Add(new HardwareTemperatureSensor
        {
            HardwareName = "AMD Ryzen 7 5700X",
            HardwareType = "CPU",
            SensorName = "CPU Package",
            ValueCelsius = 45.5,
            MinCelsius = 40,
            MaxCelsius = 61.25
        });

        vm.PerformanceSensors.Add(new HardwarePerformanceSensor
        {
            HardwareName = "AMD Ryzen 7 5700X",
            HardwareType = "CPU",
            SensorName = "CPU Total",
            MetricType = HardwarePerformanceMetricType.Load,
            Value = 12.5,
            Min = 1,
            Max = 30,
            Unit = "%"
        });

        vm.GpuMemorySensors.Add(new HardwareGpuMemorySensor
        {
            HardwareName = "NVIDIA GeForce RTX 4070",
            HardwareType = "GPU",
            SensorName = "GPU Memory Used",
            ValueMB = 2048,
            MinMB = 1024,
            MaxMB = 4096
        });

        vm.BatterySensors.Add(new HardwareBatterySensor
        {
            HardwareName = "Batería estándar",
            HardwareType = "Batería",
            SensorName = "Charge Level",
            MetricType = HardwareBatteryMetricType.Level,
            Value = 80,
            Min = 70,
            Max = 100,
            Unit = "%"
        });

        vm.MemoryTimingSensors.Add(new HardwareMemoryTimingSensor
        {
            HardwareName = "DDR4-3200",
            HardwareType = "Memoria",
            SensorName = "tAA",
            ValueNanoseconds = 16,
            MinNanoseconds = 10,
            MaxNanoseconds = 20
        });

        vm.Gpus.Add(new GpuInfo
        {
            Name = "NVIDIA GeForce RTX 4070",
            Manufacturer = "NVIDIA",
            MemoryGB = 12,
            MemoryType = "GDDR6X"
        });

        vm.MemoryModules.Add(new MemoryModuleInfo
        {
            DeviceLocator = "DIMM 0",
            BankLabel = "BANK 0",
            Manufacturer = "Kingston",
            PartNumber = "KF3200C16",
            SerialNumber = "QA0001",
            CapacityBytes = 16UL * 1024 * 1024 * 1024,
            ConfiguredClockSpeedMHz = 3200,
            SMBIOSMemoryTypeCode = 26,
            MemoryType = "DDR4",
            DataWidthBits = 64,
            TotalWidthBits = 64,
            Rank = 2
        });

        vm.Warnings.Add("sensor sin valor");
        vm.Errors.Add("error de lectura");

        return vm;
    }

    [Fact]
    public void Hardware_BuildCsv_IncludesRawLiveValuesWithUnitsAndInventory()
    {
        var vm = CreatePopulatedHardwareViewModel();

        var csv = vm.BuildCsv();

        Assert.StartsWith("sep=;", csv);
        Assert.Contains("familia;hardware;tipo;sensor;metrica;valor;unidad;min;max", csv);
        Assert.Contains("temperatura;AMD Ryzen 7 5700X;CPU;CPU Package;;45.5;°C;40;61.25", csv);
        Assert.Contains("performance;AMD Ryzen 7 5700X;CPU;CPU Total;Load;12.5;%;1;30", csv);
        Assert.Contains("gpu_memoria;NVIDIA GeForce RTX 4070;GPU;GPU Memory Used;;2048;MB;1024;4096", csv);
        Assert.Contains("bateria;Batería estándar;Batería;Charge Level;Level;80;%;70;100", csv);
        Assert.Contains("spd;DDR4-3200;Memoria;tAA;;16;ns;10;20", csv);

        // Inventario estático WMI con valores crudos y unidades.
        Assert.Contains("seccion;campo;valor", csv);
        Assert.Contains("cpu;nombre;AMD Ryzen 7 5700X", csv);
        Assert.Contains("cpu;nucleos;8", csv);
        Assert.Contains("ram;total_gb;16", csv);
        Assert.Contains("ram;tipo;DDR4", csv);
        Assert.Contains("ram;slots;2/4", csv);
        Assert.Contains("ram_modulo;DIMM 0:capacidad_gb;16", csv);
        Assert.Contains("ram_modulo;DIMM 0:velocidad_mhz;3200", csv);
        Assert.Contains("ram_modulo;DIMM 0:part_number;KF3200C16", csv);
        Assert.Contains("gpu;memoria_gb;12", csv);
        Assert.Contains("placa_madre;modelo;PRIME B550", csv);

        // Diagnóstico técnico: warnings/errors incluidos.
        Assert.Contains("diagnostico;temperatura_validos;1/1", csv);
        Assert.Contains("warning;sensor sin valor", csv);
        Assert.Contains("error;error de lectura", csv);
    }

    [Fact]
    public void Hardware_BuildTableText_IsTsvAndIncludesDiagnostics()
    {
        var vm = CreatePopulatedHardwareViewModel();

        var table = vm.BuildTableText();

        Assert.DoesNotContain("sep=", table);
        Assert.Contains("\t", table);
        Assert.Contains("familia\thardware\ttipo\tsensor\tmetrica\tvalor\tunidad\tmin\tmax", table);
        Assert.Contains("diagnostico\tproveedor\tDisponible", table);

        var diagnostics = vm.BuildDiagnosticsText();
        Assert.Contains("PROVEEDOR: Disponible", diagnostics);
        Assert.Contains("ADMIN: No", diagnostics);
        Assert.Contains("TEMPERATURA: 1/1 válidos", diagnostics);
        Assert.Contains("PERFORMANCE: 1/1 válidos", diagnostics);
        Assert.Contains("WARNINGS (1)", diagnostics);
        Assert.Contains("- sensor sin valor", diagnostics);
        Assert.Contains("ERRORS (1)", diagnostics);
    }

    [Fact]
    public void Hardware_BuildCsv_WithoutReadings_StillHasHeaders()
    {
        var vm = new HardwareViewModel(null!, null!);

        var csv = vm.BuildCsv();

        Assert.Contains("familia;hardware;tipo;sensor;metrica;valor;unidad;min;max", csv);
        Assert.Contains("(sin sensores live)", csv);
        Assert.Contains("cpu;nombre;", csv);
    }

    // =====================
    // Pantallas azules: carpetas y copia del análisis
    // =====================

    private sealed class FakeDumpDiscovery : ICrashDumpDiscoveryService
    {
        public CrashDumpDiscoveryResult Discover() => new()
        {
            Status = CrashDumpDiscoveryStatus.Found,
            Dumps = new[]
            {
                new CrashDumpEntry
                {
                    FileName = "synthetic.dmp",
                    SizeBytes = 1024,
                    LastModifiedUtc = DateTimeOffset.UtcNow
                }
            },
            FilesObserved = 1
        };
    }

    private sealed class FakeEventDiscovery : ICrashEventDiscoveryService
    {
        public CrashEventDiscoveryResult Discover() => new()
        {
            Status = CrashEventDiscoveryStatus.Found,
            Events = new[]
            {
                new CrashEventEvidence
                {
                    BugCheckCode = 0x124,
                    EventId = 1001,
                    Source = "Microsoft-Windows-WER-SystemErrorReporting"
                }
            }
        };
    }

    private sealed class FakeDecoder : IBugCheckDecoder
    {
        public BugCheckDecodeResult Decode(uint code) => new()
        {
            Code = code,
            IsKnown = true,
            Name = "WHEA_UNCORRECTABLE_ERROR",
            Description = "Error de hardware."
        };
    }

    [Fact]
    public async Task CrashEvidence_BuildAnalysisText_IncludesDumpBugcheckAndEvents()
    {
        var vm = new CrashEvidenceViewModel(
            new FakeDumpDiscovery(), new FakeEventDiscovery(), new FakeDecoder());

        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedDump = Assert.Single(vm.Dumps);

        var analysis = vm.BuildAnalysisText();

        Assert.Contains("synthetic.dmp", analysis);
        Assert.Contains("0x00000124", analysis);
        Assert.Contains("WHEA_UNCORRECTABLE_ERROR", analysis);
        Assert.Contains("Microsoft-Windows-WER-SystemErrorReporting", analysis);
        Assert.Contains("MÓDULOS", analysis);
        Assert.DoesNotContain("(ninguno seleccionado)", analysis);
        // No se exponen rutas privadas del perfil de usuario.
        Assert.DoesNotContain("C:\\Users", analysis);
    }

    [Fact]
    public void CrashEvidence_ResolveDumpsDirectory_Preferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "cattech-ux-tests", Guid.NewGuid().ToString("N"));
        var windows = Path.Combine(root, "Windows");
        var minidump = Path.Combine(windows, "Minidump");
        var configured = Path.Combine(root, "configured");

        try
        {
            Directory.CreateDirectory(windows);

            // Sin carpetas existentes: fallback a Windows\Minidump.
            Assert.Equal(
                minidump,
                CrashEvidenceViewModel.ResolveDumpsDirectory(windows, null, null));

            // Carpeta configurable existente cuando Minidump no existe.
            Directory.CreateDirectory(configured);
            Assert.Equal(
                configured,
                CrashEvidenceViewModel.ResolveDumpsDirectory(windows, configured, null));

            // Windows\Minidump tiene prioridad si existe.
            Directory.CreateDirectory(minidump);
            Assert.Equal(
                minidump,
                CrashEvidenceViewModel.ResolveDumpsDirectory(windows, configured, null));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void CrashEvidence_DefaultAnalysis_IsEmptyButHonest()
    {
        var vm = new CrashEvidenceViewModel(
            new FakeDumpDiscovery(), new FakeEventDiscovery(), new FakeDecoder());

        var analysis = vm.BuildAnalysisText();

        Assert.Contains("Evidencia BSOD", analysis);
        Assert.Contains("sin archivos .dmp visibles", analysis);
        Assert.Contains("(ninguno seleccionado)", analysis);
        Assert.Contains("No disponibles", analysis);
    }

    // =====================
    // Reparaciones: comandos exactos y plan completo
    // =====================

    private sealed class FakeWindowsUpdateInspection : IWindowsUpdateReadinessService
    {
        public WindowsUpdateReadinessResult Inspect() => new()
        {
            Status = WindowsUpdateReadinessStatus.Observed,
            Services = Array.Empty<WindowsUpdateServiceObservation>()
        };
    }

    private sealed class FakePreflight : ISystemRepairPreflightService
    {
        public SystemRepairPreflightResult Inspect(SystemRepairOperation operation) => new()
        {
            Operation = operation,
            Status = SystemRepairPreflightStatus.ReviewRequired
        };
    }

    private static RepairCenterViewModel CreateRepairCenterViewModel() => new(
        new FakeWindowsUpdateInspection(),
        new SystemRepairPlanService(),
        new FakePreflight());

    [Fact]
    public void RepairCenter_BuildCommandsText_ReturnsExactPlanCommand()
    {
        var vm = CreateRepairCenterViewModel();
        var restore = vm.Plans.Single(x => x.Operation == SystemRepairOperation.DismRestoreHealth);
        var scan = vm.Plans.Single(x => x.Operation == SystemRepairOperation.SfcScanNow);

        Assert.Equal(
            "dism.exe /Online /Cleanup-Image /RestoreHealth" + Environment.NewLine,
            RepairCenterViewModel.BuildCommandsText(restore));
        Assert.Equal(
            "sfc.exe /scannow" + Environment.NewLine,
            RepairCenterViewModel.BuildCommandsText(scan));

        vm.SelectedPlan = restore;
        Assert.Contains("dism.exe /Online /Cleanup-Image /RestoreHealth", vm.SelectedPlanCommandsText);
    }

    [Fact]
    public void RepairCenter_BuildFullPlanText_ListsAllPlansAndDeclaresNoExecution()
    {
        var vm = CreateRepairCenterViewModel();

        var text = vm.BuildFullPlanText();

        Assert.Contains("EJECUCIÓN NO IMPLEMENTADA EN ESTA VERSIÓN", text);
        Assert.Contains("ejecución manual controlada", text);
        Assert.Contains("dism.exe /Online /Cleanup-Image /RestoreHealth", text);
        Assert.Contains("sfc.exe /scannow", text);
        Assert.Contains("sfc.exe /verifyonly", text);
        Assert.Contains("PREFLIGHT DEL PLAN SELECCIONADO", text);
        Assert.Contains("(sin plan seleccionado)", text);
        Assert.Contains("ESTADO WINDOWS UPDATE", text);
        // 5 planes del catálogo cerrado.
        Assert.Contains("[5]", text);
    }

    [Fact]
    public void RepairCenter_SelectedPlanCommands_UpdatesWithSelection()
    {
        var vm = CreateRepairCenterViewModel();

        Assert.Equal("(sin plan seleccionado)", vm.SelectedPlanCommandsText);

        vm.SelectedPlan = vm.Plans.Single(x => x.Operation == SystemRepairOperation.SfcVerifyOnly);

        Assert.Contains("sfc.exe /verifyonly", vm.SelectedPlanCommandsText);
        Assert.True(vm.CopyCommandsCommand.CanExecute(null));

        vm.SelectedPlan = null;

        Assert.Equal("(sin plan seleccionado)", vm.SelectedPlanCommandsText);
        Assert.False(vm.CopyCommandsCommand.CanExecute(null));
    }
}
