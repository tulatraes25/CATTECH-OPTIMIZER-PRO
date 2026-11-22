using System.Xml.Linq;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// UX para técnicos avanzados: atributos SMART crudos, exportación/copia de
/// informes, sesiones self-test persistidas, filtros reales y detalle técnico
/// de programas de inicio, e historial técnico de limpieza.
/// Todo con fakes y datos sintéticos: no se ejecuta smartctl, no se borran
/// archivos reales y no se abren diálogos ni el Explorador.
/// </summary>
public sealed class AdvancedUxTests
{
    // =====================
    // SMART: atributos crudos
    // =====================

    [Fact]
    public void SmartAttributeRow_FromReport_ProjectsAtaRawColumns()
    {
        var report = new SmartDiskReport
        {
            ImportantAttributes =
            [
                new SmartAttribute
                {
                    Id = 5,
                    Name = "Reallocated_Sector_Ct",
                    Value = 100,
                    Worst = 100,
                    Threshold = 10,
                    RawValue = 42,
                    Flags = "0x0033",
                    Description = "Sectores reasignados",
                    IsPrefailure = true,
                    Severity = SmartSeverity.Warning
                }
            ]
        };

        var rows = SmartAttributeRow.FromReport(report);

        var row = Assert.Single(rows);
        Assert.Equal("5", row.IdDisplay);
        Assert.Equal("Reallocated_Sector_Ct", row.Name);
        Assert.Equal("100", row.ValueDisplay);
        Assert.Equal("100", row.WorstDisplay);
        Assert.Equal("10", row.ThresholdDisplay);
        Assert.Equal("42", row.RawDisplay);
        Assert.Equal("0x0033", row.Flags);
        Assert.True(row.IsPrefailure);
        Assert.Equal("ATA", row.Source);
        Assert.Equal("Advertencia", row.SeverityDisplay);
        Assert.Contains("Flags: 0x0033", row.ToolTipText);
    }

    [Fact]
    public void SmartAttributeRow_FromReport_ProjectsNvmeCriticals()
    {
        var report = new SmartDiskReport
        {
            NvmePercentageUsed = 85,
            NvmeAvailableSpare = 90,
            NvmeAvailableSpareThreshold = 10,
            NvmeCriticalWarning = 3,
            NvmeMediaErrors = 2,
            NvmeUnsafeShutdowns = 7
        };

        var rows = SmartAttributeRow.FromReport(report);

        Assert.Equal(6, rows.Count);
        Assert.All(rows, row => Assert.Equal("NVMe", row.IdDisplay));
        Assert.Contains(rows, row => row.Name == "percentage_used" && row.RawDisplay == "85 %");
        Assert.Contains(rows, row => row.Name == "available_spare" && row.RawDisplay == "90 %");
        Assert.Contains(rows, row => row.Name == "available_spare_threshold" && row.RawDisplay == "10 %");
        Assert.Contains(rows, row => row.Name == "critical_warning"
            && row.RawDisplay == "0x03"
            && row.Severity == SmartSeverity.Critical);
        Assert.Contains(rows, row => row.Name == "media_errors" && row.RawDisplay == "2");
        Assert.Contains(rows, row => row.Name == "unsafe_shutdowns" && row.RawDisplay == "7");
    }

    [Fact]
    public void SmartAttributeRow_FromReport_EmptyReport_HasNoInventedRows()
    {
        Assert.Empty(SmartAttributeRow.FromReport(new SmartDiskReport()));
        Assert.Empty(SmartAttributeRow.FromReport(null));
    }

    // =====================
    // SMART: informe técnico TSV/CSV y portapapeles
    // =====================

    [Fact]
    public async Task SmartDiskViewModel_BuildSmartReportTsv_ContainsDisksAttributesAndSessions()
    {
        var (vm, _) = CreateSmartViewModel();
        vm.Reports.Add(SyntheticReport());

        await vm.CheckSmartctlCommand.ExecuteAsync(null);
        await vm.RefreshSessionsCommand.ExecuteAsync(null);

        var tsv = vm.BuildSmartReportTsv();

        Assert.Contains("CATTECH SMART", tsv);
        Assert.Contains("## DISCOS", tsv);
        Assert.Contains("/dev/sda", tsv);
        Assert.Contains("Synthetic SSD", tsv);
        Assert.Contains("Reallocated_Sector_Ct", tsv);
        Assert.Contains("\t42\t", tsv);
        Assert.Contains("## SESIONES SELF-TEST", tsv);
        Assert.Contains("TST00001", tsv);
        Assert.Contains("En ejecución", tsv);
    }

    [Fact]
    public async Task SmartDiskViewModel_BuildSmartReportCsv_QuotesSeparators()
    {
        var (vm, _) = CreateSmartViewModel();
        var report = SyntheticReport();
        report.ModelName = "SSD; con separador";
        report.SerialNumber = "SER\"IE";
        vm.Reports.Add(report);
        await vm.CheckSmartctlCommand.ExecuteAsync(null);

        var csv = vm.BuildSmartReportCsv();

        Assert.Contains("[DISCOS]", csv);
        Assert.Contains("[ATRIBUTOS]", csv);
        Assert.Contains("[SESIONES]", csv);
        Assert.Contains("\"SSD; con separador\"", csv);
        Assert.Contains("\"SER\"\"IE\"", csv);
    }

    [Fact]
    public async Task SmartDiskViewModel_WriteCsvFile_CreatesUtf8FileWithContent()
    {
        var (vm, _) = CreateSmartViewModel();
        vm.Reports.Add(SyntheticReport());
        await vm.CheckSmartctlCommand.ExecuteAsync(null);

        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "smart.csv");

        vm.WriteCsvFile(path);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("[DISCOS]", content);
        Assert.Contains("/dev/sda", content);
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 3);
        Assert.Equal(0xEF, (int)bytes[0]);
        Assert.Equal(0xBB, (int)bytes[1]);
        Assert.Equal(0xBF, (int)bytes[2]);
    }

    [Fact]
    public async Task SmartDiskViewModel_CopySmartReport_UsesClipboardHook()
    {
        var (vm, _) = CreateSmartViewModel();
        vm.Reports.Add(SyntheticReport());
        await vm.CheckSmartctlCommand.ExecuteAsync(null);

        string? copied = null;
        vm.CopyTextOverride = text =>
        {
            copied = text;
            return true;
        };

        await vm.CopySmartReportCommand.ExecuteAsync(null);

        Assert.NotNull(copied);
        Assert.Contains("## DISCOS", copied!);
        Assert.True(vm.IsSuccess);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task SmartDiskViewModel_CopySmartReport_WithoutData_ShowsVisibleError()
    {
        var (vm, _) = CreateSmartViewModel();

        await vm.CopySmartReportCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("No hay informe SMART", vm.ErrorMessage);
    }

    // =====================
    // SMART: sesiones persistidas
    // =====================

    [Fact]
    public async Task SmartDiskViewModel_RefreshSessions_RestoresPersistedInProgressSession()
    {
        var (vm, testService) = CreateSmartViewModel();
        testService.Sessions =
        [
            new SmartTestSession
            {
                Id = "TST00001",
                Device = "/dev/sda",
                ModelName = "Synthetic SSD",
                TestType = SmartTestType.Short,
                Status = SmartTestStatus.InProgress,
                ProgressPercent = 40,
                RequestedAt = new DateTime(2026, 1, 15, 10, 0, 0),
                LastCheckedAt = new DateTime(2026, 1, 15, 10, 5, 0)
            }
        ];

        await vm.RefreshSessionsCommand.ExecuteAsync(null);

        Assert.Equal(1, testService.ListSessionsCalls);
        Assert.True(vm.HasSessions);
        Assert.Single(vm.TestSessions);
        Assert.Equal("En ejecución", vm.TestSessions[0].StatusText);
        Assert.NotNull(vm.CurrentTestSession);
        Assert.Equal("/dev/sda", vm.CurrentTestSession!.Device);
        Assert.True(vm.IsTestInProgress);
        Assert.True(vm.CanCheckTestStatus);
        Assert.Null(vm.SelectedReport); // no hay reportes: no se inventa un disco
    }

    [Fact]
    public async Task SmartDiskViewModel_RefreshSessions_EmptyList_ReportsNoSessions()
    {
        var (vm, testService) = CreateSmartViewModel();
        testService.Sessions = [];

        await vm.RefreshSessionsCommand.ExecuteAsync(null);

        Assert.False(vm.HasSessions);
        Assert.Empty(vm.TestSessions);
        Assert.Null(vm.CurrentTestSession);
        Assert.Contains("Sin sesiones", vm.SessionsStatusText);
    }

    // =====================
    // SMART: carpetas técnicas
    // =====================

    [Fact]
    public async Task SmartDiskViewModel_OpenReportsFolder_CreatesDirectoryAndCallsOpener()
    {
        using var temp = new TempDirectory();
        string? opened = null;
        var vm = new SmartDiskViewModel(
            new FakeSmartctlRunner(),
            new FakeSmartDiskService(),
            new FakeSmartTestService(),
            temp.Path,
            directory => opened = directory);

        await vm.OpenReportsFolderCommand.ExecuteAsync(null);

        var expected = Path.Combine(temp.Path, "data", "smart-reports");
        Assert.True(Directory.Exists(expected));
        Assert.Equal(Path.GetFullPath(expected), opened);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task SmartDiskViewModel_OpenReportsFolder_WhenOpenerFails_ShowsGenericError()
    {
        using var temp = new TempDirectory();
        var vm = new SmartDiskViewModel(
            new FakeSmartctlRunner(),
            new FakeSmartDiskService(),
            new FakeSmartTestService(),
            temp.Path,
            _ => throw new IOException("PRIVATE " + temp.Path));

        await vm.OpenReportsFolderCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("carpeta de informes SMART", vm.ErrorMessage);
        Assert.DoesNotContain(temp.Path, vm.ErrorMessage);
        Assert.DoesNotContain("PRIVATE", vm.ErrorMessage);
    }

    // =====================
    // Programas de inicio: filtros reales
    // =====================

    [Fact]
    public async Task Startup_Filters_ActuallyFilterTheGridCollection()
    {
        var vm = await CreateAnalyzedStartupViewModel();

        Assert.Equal(5, vm.FilteredSelectableEntries.Count);
        Assert.Equal(vm.SelectableEntries.Count, vm.FilteredEntries.Count);

        vm.SetFilterCommand.Execute("No Microsoft");
        Assert.Equal(4, vm.FilteredSelectableEntries.Count);
        Assert.All(vm.FilteredSelectableEntries, s => Assert.False(s.Entry.IsMicrosoft));

        vm.SetFilterCommand.Execute("Recomendados para revisar");
        Assert.Single(vm.FilteredSelectableEntries);
        Assert.Equal("Beta", vm.FilteredSelectableEntries[0].Entry.Name);

        vm.SetFilterCommand.Execute("Posible desactivar");
        Assert.Single(vm.FilteredSelectableEntries);
        Assert.Equal("Gamma", vm.FilteredSelectableEntries[0].Entry.Name);

        vm.SetFilterCommand.Execute("Alertas");
        Assert.Single(vm.FilteredSelectableEntries);
        Assert.Equal("Broken", vm.FilteredSelectableEntries[0].Entry.Name);

        vm.SetFilterCommand.Execute("Todos");
        Assert.Equal(5, vm.FilteredSelectableEntries.Count);
    }

    [Fact]
    public async Task Startup_Search_IsReactiveAndMatchesLocation()
    {
        var vm = await CreateAnalyzedStartupViewModel();

        vm.SearchText = "gamma";
        Assert.Single(vm.FilteredSelectableEntries);
        Assert.Equal("Gamma", vm.FilteredSelectableEntries[0].Entry.Name);

        vm.SearchText = "HKLM";
        Assert.Single(vm.FilteredSelectableEntries);
        Assert.Equal("Broken", vm.FilteredSelectableEntries[0].Entry.Name);

        vm.SearchText = string.Empty;
        Assert.Equal(5, vm.FilteredSelectableEntries.Count);
    }

    [Fact]
    public async Task Startup_FilteredCountText_ReflectsVisibleRows()
    {
        var vm = await CreateAnalyzedStartupViewModel();
        Assert.Equal("5 de 5", vm.FilteredCountText);

        vm.SearchText = "Beta";
        Assert.Equal("1 de 5 (filtrado)", vm.FilteredCountText);
    }

    // =====================
    // Programas de inicio: detalle técnico y acciones
    // =====================

    [Fact]
    public async Task Startup_Selection_UpdatesTechnicalDetail()
    {
        var vm = await CreateAnalyzedStartupViewModel();

        var alpha = vm.FilteredSelectableEntries.First(s => s.Entry.Name == "Alpha");
        vm.SelectedSelectableEntry = alpha;

        Assert.True(vm.HasSelectedEntry);
        Assert.Equal("Alpha", vm.SelectedEntry!.Name);
        Assert.Equal(alpha.Entry.Command, vm.SelectedEntryCommand);
        Assert.Equal(alpha.Entry.Location, vm.SelectedEntryLocation);
        Assert.True(vm.HasSelectedEntryLocation);
        Assert.Equal("HKEY_CURRENT_USER\\Software\\Synthetic\\Run", vm.RegistryKeyFullPath);
    }

    [Theory]
    [InlineData("HKCU\\Software\\Synthetic\\Run", "HKEY_CURRENT_USER\\Software\\Synthetic\\Run")]
    [InlineData("HKLM\\Software\\Synthetic\\Run", "HKEY_LOCAL_MACHINE\\Software\\Synthetic\\Run")]
    [InlineData("HKU\\S-1-5-21\\Run", "HKEY_USERS\\S-1-5-21\\Run")]
    public async Task Startup_RegistryKeyFullPath_MapsHives(string location, string expected)
    {
        var vm = await CreateAnalyzedStartupViewModel();
        var entry = vm.FilteredSelectableEntries.First().Entry;
        entry.Location = location;
        entry.SourceType = StartupSourceType.RegistryRun;
        vm.SelectedSelectableEntry = vm.FilteredSelectableEntries.First();

        Assert.True(vm.IsRegistryEntry);
        Assert.Equal(expected, vm.RegistryKeyFullPath);
        Assert.Equal("Clave de registro", vm.LocationKindText);
    }

    [Fact]
    public async Task Startup_CopyCommand_UsesClipboardHook()
    {
        var vm = await CreateAnalyzedStartupViewModel();
        vm.SelectedSelectableEntry = vm.FilteredSelectableEntries.First(s => s.Entry.Name == "Alpha");

        string? copied = null;
        vm.CopyTextOverride = text =>
        {
            copied = text;
            return true;
        };

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.NotNull(copied);
        Assert.Equal(vm.SelectedEntry!.Command, copied);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Startup_CopyEntry_BuildsFullTechnicalText()
    {
        var vm = await CreateAnalyzedStartupViewModel();
        var alpha = vm.FilteredSelectableEntries.First(s => s.Entry.Name == "Alpha");
        vm.SelectedSelectableEntry = alpha;

        string? copied = null;
        vm.CopyTextOverride = text =>
        {
            copied = text;
            return true;
        };

        await vm.CopyEntryCommand.ExecuteAsync(null);

        Assert.NotNull(copied);
        Assert.Contains($"ID: {alpha.Entry.Id}", copied!);
        Assert.Contains("Nombre: Alpha", copied!);
        Assert.Contains($"Comando: {alpha.Entry.Command}", copied!);
        Assert.Contains($"Ubicación: {alpha.Entry.Location}", copied!);
        Assert.Contains("Riesgo:", copied!);
        Assert.Contains("Recomendación:", copied!);
    }

    [Fact]
    public async Task Startup_CopyCommand_WithoutSelection_ShowsVisibleError()
    {
        var vm = await CreateAnalyzedStartupViewModel();
        vm.SelectedSelectableEntry = null;

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("Seleccioná una entrada", vm.ErrorMessage);
    }

    [Fact]
    public async Task Startup_OpenRegistryLocation_UsesRegeditWithMappedKey()
    {
        var vm = await CreateAnalyzedStartupViewModel();
        var entry = vm.FilteredSelectableEntries.First().Entry;
        entry.Location = "HKCU\\Software\\Synthetic\\Run";
        entry.SourceType = StartupSourceType.RegistryRun;
        vm.SelectedSelectableEntry = vm.FilteredSelectableEntries.First();

        string? fileName = null;
        string? arguments = null;
        vm.OpenShellTargetRequest = (file, args) =>
        {
            fileName = file;
            arguments = args;
            return true;
        };

        await vm.OpenLocationCommand.ExecuteAsync(null);

        Assert.Equal("regedit.exe", fileName);
        Assert.NotNull(arguments);
        Assert.Contains("HKEY_CURRENT_USER\\Software\\Synthetic\\Run", arguments!);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Startup_OpenFolderLocation_UsesExplorerSelect()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "synthetic-app.lnk");
        await File.WriteAllTextAsync(file, "synthetic");

        var vm = await CreateAnalyzedStartupViewModel();
        var entry = vm.FilteredSelectableEntries.First().Entry;
        entry.Location = file;
        entry.SourceType = StartupSourceType.StartupFolder;
        vm.SelectedSelectableEntry = vm.FilteredSelectableEntries.First();

        string? fileName = null;
        string? arguments = null;
        vm.OpenShellTargetRequest = (name, args) =>
        {
            fileName = name;
            arguments = args;
            return true;
        };

        await vm.OpenLocationCommand.ExecuteAsync(null);

        Assert.Equal("explorer.exe", fileName);
        Assert.NotNull(arguments);
        Assert.Contains("/select,", arguments!);
        Assert.Contains(file, arguments!);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Startup_OpenLocation_WhenLaunchFails_ShowsGenericError()
    {
        var vm = await CreateAnalyzedStartupViewModel();
        vm.SelectedSelectableEntry = vm.FilteredSelectableEntries.First();
        vm.OpenShellTargetRequest = (_, _) => false;

        await vm.OpenLocationCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("No se pudo abrir la ubicación", vm.ErrorMessage);
        Assert.DoesNotContain("C:\\Users", vm.ErrorMessage);
    }

    // =====================
    // Limpieza: historial técnico
    // =====================

    [Fact]
    public async Task Cleanup_LoadHistory_PopulatesTechnicalHistory()
    {
        var fake = new FakeCleanupService();
        fake.History =
        [
            new TempCleanupResultSummary
            {
                Id = "A1",
                StartedAt = new DateTime(2026, 1, 15, 10, 0, 0),
                DeletedMB = 12.5,
                DeletedFiles = 3,
                HasWarnings = true,
                FileName = "cleanup-result-20260115-100000.json"
            }
        ];
        var vm = new TempCleanupViewModel(fake);

        await vm.LoadHistoryCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.ListResultsCalls);
        Assert.True(vm.HasHistory);
        var row = Assert.Single(vm.HistoryRows);
        Assert.Equal("15/01/2026 10:00", row.StartedAtDisplay);
        Assert.Equal("12.5 MB", row.DeletedMBDisplay);
        Assert.Equal("3", row.DeletedFilesDisplay);
        Assert.Equal("Sí", row.WarningsDisplay);
        Assert.Equal("cleanup-result-20260115-100000.json", row.FileNameDisplay);
    }

    [Fact]
    public async Task Cleanup_LoadHistory_Empty_ShowsNoRecords()
    {
        var vm = new TempCleanupViewModel(new FakeCleanupService());

        await vm.LoadHistoryCommand.ExecuteAsync(null);

        Assert.False(vm.HasHistory);
        Assert.Empty(vm.HistoryRows);
        Assert.Contains("Sin registros", vm.HistoryStatusText);
    }

    [Fact]
    public async Task Cleanup_LoadHistory_ServiceFailure_ShowsGenericStatus()
    {
        var fake = new FakeCleanupService { ThrowOnListResults = true };
        var vm = new TempCleanupViewModel(fake);

        await vm.LoadHistoryCommand.ExecuteAsync(null);

        Assert.False(vm.HasHistory);
        Assert.Empty(vm.HistoryRows);
        Assert.Contains("No se pudo leer el historial", vm.HistoryStatusText);
        Assert.DoesNotContain("PRIVATE", vm.HistoryStatusText);
    }

    [Fact]
    public async Task Cleanup_AfterCleaning_ReloadsHistory()
    {
        var target = SyntheticCleanupTarget();
        var fake = new FakeCleanupService { ScanTargets = [target] };
        var vm = new TempCleanupViewModel(fake);

        await vm.ScanCommand.ExecuteAsync(null);
        vm.HasCleanupConsent = true;
        Assert.True(vm.CanStartCleanup);

        await vm.CleanupCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.CleanupCalls);
        Assert.Equal(1, fake.SaveCalls);
        Assert.True(fake.ListResultsCalls >= 1);
        Assert.True(vm.HasResult);
    }

    [Fact]
    public async Task Cleanup_OpenResultsFolder_CreatesDirectoryAndCallsOpener()
    {
        using var temp = new TempDirectory();
        string? opened = null;
        var vm = new TempCleanupViewModel(new FakeCleanupService(), temp.Path, directory => opened = directory);

        await vm.OpenResultsFolderCommand.ExecuteAsync(null);

        var expected = Path.Combine(temp.Path, "data", "cleanup-results");
        Assert.True(Directory.Exists(expected));
        Assert.Equal(Path.GetFullPath(expected), opened);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task Cleanup_OpenResultsFolder_WhenOpenerFails_ShowsGenericError()
    {
        using var temp = new TempDirectory();
        var vm = new TempCleanupViewModel(
            new FakeCleanupService(),
            temp.Path,
            _ => throw new IOException("PRIVATE " + temp.Path));

        await vm.OpenResultsFolderCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("carpeta de resultados", vm.ErrorMessage);
        Assert.DoesNotContain(temp.Path, vm.ErrorMessage);
        Assert.DoesNotContain("PRIVATE", vm.ErrorMessage);
    }

    // =====================
    // Contratos de XAML (contenido técnico visible)
    // =====================

    [Fact]
    public void SmartView_ExposesAttributeTabSessionsAndTechnicalActions()
    {
        var xaml = ReadView("SmartDiskView.xaml");
        var xml = XDocument.Parse(xaml);

        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("Header") == "Atributos");
        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("Header") == "Sesiones self-test");
        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("ItemsSource") == "{Binding SelectedAttributeRows}");
        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("ItemsSource") == "{Binding TestSessions}");

        Assert.Contains("Copiar informe", xaml);
        Assert.Contains("Exportar CSV", xaml);
        Assert.Contains("Abrir carpeta reports", xaml);
        Assert.Contains("Abrir config smartctl", xaml);
        Assert.Contains("CopySmartReportCommand", xaml);
        Assert.Contains("ExportCsvCommand", xaml);
        Assert.Contains("RefreshSessionsCommand", xaml);
        Assert.Contains("Consolas", xaml);
    }

    [Fact]
    public void StartupView_BindsFilteredGridAndDetailActions()
    {
        var xaml = ReadView("StartupAnalysisView.xaml");
        var xml = XDocument.Parse(xaml);

        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("ItemsSource") == "{Binding FilteredSelectableEntries}");
        Assert.Contains(xml.Descendants(), element =>
            ((string?)element.Attribute("SelectedItem"))?.StartsWith(
                "{Binding SelectedSelectableEntry", StringComparison.Ordinal) == true);
        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("Header") == "Ubicación");

        Assert.Contains("Copiar comando", xaml);
        Assert.Contains("Copiar entrada", xaml);
        Assert.Contains("Abrir ubicación", xaml);
        Assert.Contains("CopyCommand", xaml);
        Assert.Contains("CopyEntryCommand", xaml);
        Assert.Contains("OpenLocationCommand", xaml);
        Assert.Contains("Consolas", xaml);
    }

    [Fact]
    public void CleanupView_ShowsHistoryPanelAndResultsFolderButton()
    {
        var xaml = ReadView("TempCleanupView.xaml");
        var xml = XDocument.Parse(xaml);

        Assert.Contains("HISTORIAL TÉCNICO", xaml);
        Assert.Contains("Abrir carpeta de resultados", xaml);
        Assert.Contains("LoadHistoryCommand", xaml);
        Assert.Contains("OpenResultsFolderCommand", xaml);
        Assert.Contains(xml.Descendants(), element =>
            (string?)element.Attribute("ItemsSource") == "{Binding HistoryRows}");
        Assert.Contains("FontFamily=\"Consolas\"", xaml);
        // El detalle técnico de la ruta usa fuente monoespaciada y tooltip completo.
        Assert.Contains("ToolTip=\"{Binding Path}\"", xaml);
    }

    // =====================
    // Helpers
    // =====================

    private static string ReadView(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.UI/Views", fileName));
        return File.ReadAllText(path);
    }

    private static (SmartDiskViewModel ViewModel, FakeSmartTestService TestService) CreateSmartViewModel(
        FakeSmartDiskService? diskService = null)
    {
        var testService = new FakeSmartTestService
        {
            Sessions =
            [
                new SmartTestSession
                {
                    Id = "TST00001",
                    Device = "/dev/sda",
                    ModelName = "Synthetic SSD",
                    TestType = SmartTestType.Extended,
                    Status = SmartTestStatus.InProgress,
                    ProgressPercent = 55,
                    RequestedAt = new DateTime(2026, 1, 15, 9, 0, 0),
                    LastCheckedAt = new DateTime(2026, 1, 15, 9, 30, 0)
                }
            ]
        };
        var vm = new SmartDiskViewModel(
            new FakeSmartctlRunner(),
            diskService ?? new FakeSmartDiskService(),
            testService,
            Path.Combine(Path.GetTempPath(), "cattech-advancedux-" + Guid.NewGuid().ToString("N")[..8]));
        return (vm, testService);
    }

    private static SmartDiskReport SyntheticReport() => new()
    {
        Device = "/dev/sda",
        DeviceName = "/dev/sda [SAT]",
        DeviceType = "SSD",
        Protocol = "SATA",
        ModelName = "Synthetic SSD",
        SerialNumber = "SER123",
        FirmwareVersion = "FW01",
        CapacityBytes = 500107862016,
        TemperatureCelsius = 35,
        PowerOnHours = 1234,
        PowerCycleCount = 42,
        HealthStatus = SmartHealthStatus.Warning,
        HealthSummary = "Synthetic warning",
        IsAnalysisSuccessful = true,
        OverallHealthPassed = true,
        NvmePercentageUsed = 85,
        NvmeMediaErrors = 2,
        ImportantAttributes =
        [
            new SmartAttribute
            {
                Id = 5,
                Name = "Reallocated_Sector_Ct",
                Value = 100,
                Worst = 99,
                Threshold = 10,
                RawValue = 42,
                Flags = "0x0033",
                Description = "Sectores reasignados",
                Severity = SmartSeverity.Warning
            }
        ]
    };

    private static async Task<StartupAnalysisViewModel> CreateAnalyzedStartupViewModel()
    {
        var entries = new List<StartupEntry>
        {
            SyntheticStartup("Alpha", @"C:\Synthetic\alpha.exe", "HKCU\\Software\\Synthetic\\Run",
                StartupRecommendation.Keep),
            SyntheticStartup("Beta", @"C:\Synthetic\beta.exe", "HKCU\\Software\\Synthetic\\Run",
                StartupRecommendation.Review),
            SyntheticStartup("Gamma", @"C:\Synthetic\gamma.exe", "HKCU\\Software\\Synthetic\\RunOnce",
                StartupRecommendation.PossibleDisable),
            SyntheticStartup("Microsoft OneDrive", @"C:\Program Files\\OneDrive\\OneDrive.exe",
                "HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run",
                StartupRecommendation.Keep, isMicrosoft: true),
            SyntheticStartup("Broken", @"C:\Synthetic\missing.exe", "HKLM\\Software\\Synthetic\\Run",
                StartupRecommendation.Keep, status: StartupEntryStatus.PathNotFound)
        };

        var vm = new StartupAnalysisViewModel(new FakeStartupService(entries));
        await vm.AnalyzeCommand.ExecuteAsync(null);
        return vm;
    }

    private static StartupEntry SyntheticStartup(
        string name,
        string command,
        string location,
        StartupRecommendation recommendation,
        bool isMicrosoft = false,
        StartupEntryStatus status = StartupEntryStatus.Active) => new()
        {
            Name = name,
            Command = command,
            Location = location,
            SourceType = StartupSourceType.RegistryRun,
            Publisher = "Synthetic Ltd.",
            IsMicrosoft = isMicrosoft,
            Status = status,
            Risk = status == StartupEntryStatus.PathNotFound ? RiskLevel.High : RiskLevel.Low,
            Recommendation = recommendation
        };

    private static TempCleanupTarget SyntheticCleanupTarget() => new()
    {
        Id = "USER_TEMP",
        DisplayName = "Temporales del usuario",
        Path = @"C:\Synthetic\Temp",
        Description = "Synthetic",
        IsScanned = true,
        IsAccessible = true,
        IsSelected = true,
        RiskLevel = CleanupRiskLevel.Low
    };

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "cattech-advancedux-" + Guid.NewGuid().ToString("N")[..8]);

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Un handle transitorio puede demorar el borrado.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // =====================
    // Fakes
    // =====================

    private sealed class FakeSmartctlRunner : ISmartctlRunner
    {
        public Task<SmartctlAvailability> CheckAvailabilityAsync() =>
            Task.FromResult(new SmartctlAvailability
            {
                IsAvailable = true,
                SmartctlPath = @"C:\Synthetic\smartmontools\smartctl.exe",
                Version = "smartctl 7.4",
                SupportsJson = true
            });

        public Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout) =>
            Task.FromResult(new SmartctlCommandResult());

        public Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync() =>
            Task.FromResult<IReadOnlyList<SmartDiskDevice>>(Array.Empty<SmartDiskDevice>());
    }

    private sealed class FakeSmartDiskService : ISmartDiskService
    {
        public Task<SmartAnalysisResult> AnalyzeAllDisksAsync() =>
            Task.FromResult(new SmartAnalysisResult());

        public Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device) =>
            Task.FromResult(new SmartDiskReport());

        public Task<string> SaveResultAsync(SmartAnalysisResult result) =>
            Task.FromResult("analysis-sintetico.json");

        public Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(int maxResults = 20) =>
            Task.FromResult<IReadOnlyList<SmartAnalysisResult>>(Array.Empty<SmartAnalysisResult>());
    }

    private sealed class FakeSmartTestService : ISmartTestService
    {
        public IReadOnlyList<SmartTestSession> Sessions { get; set; } = [];

        public int ListSessionsCalls { get; private set; }

        public Task<SmartTestSession> StartShortTestAsync(SmartDiskDevice device) =>
            Task.FromResult(new SmartTestSession());

        public Task<SmartTestSession> StartExtendedTestAsync(SmartDiskDevice device) =>
            Task.FromResult(new SmartTestSession());

        public Task<SmartTestSession> CheckStatusAsync(SmartTestSession session) =>
            Task.FromResult(session);

        public Task<SmartTestResult?> GetLatestResultAsync(SmartDiskDevice device) =>
            Task.FromResult<SmartTestResult?>(null);

        public Task<string> SaveSessionAsync(SmartTestSession session) =>
            Task.FromResult("session-sintetica.json");

        public Task<IReadOnlyList<SmartTestSession>> ListSessionsAsync(int maxResults = 20)
        {
            ListSessionsCalls++;
            return Task.FromResult(Sessions);
        }
    }

    private sealed class FakeStartupService : IStartupService
    {
        private readonly IReadOnlyList<StartupEntry> _entries;

        public FakeStartupService(IReadOnlyList<StartupEntry> entries) => _entries = entries;

        public Task<StartupAnalysis> AnalyzeStartupAsync() =>
            Task.FromResult(new StartupAnalysis { Entries = _entries.ToList() });

        public bool CanDisableStartupEntry(StartupEntry entry) => !entry.IsMicrosoft;

        public Task<StartupDisableSummary> DisableSelectedAsync(
            IEnumerable<StartupEntry> selected, string reason = "") =>
            Task.FromResult(new StartupDisableSummary());

        public Task<StartupActionResult> RestoreAsync(StartupBackupRecord backup) =>
            Task.FromResult(StartupActionResult.Success);

        public Task<List<StartupBackupRecord>> ListBackupsAsync() =>
            Task.FromResult(new List<StartupBackupRecord>());

        public Task<StartupBackupRecord?> LoadBackupAsync(string backupId) =>
            Task.FromResult<StartupBackupRecord?>(null);

        public Task<string> SaveAnalysisAsync(StartupAnalysis analysis) =>
            Task.FromResult("startup-sintetico.json");

        public Task<StartupAnalysis?> LoadAnalysisAsync(string analysisId) =>
            Task.FromResult<StartupAnalysis?>(null);

        public Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20) =>
            Task.FromResult(new List<StartupAnalysisSummary>());

        public Task<bool> DeleteAnalysisAsync(string analysisId) => Task.FromResult(true);
    }

    private sealed class FakeCleanupService : ITempCleanupService
    {
        public IReadOnlyList<TempCleanupTarget> ScanTargets { get; set; } = [];

        public List<TempCleanupResultSummary> History { get; set; } = [];

        public bool ThrowOnListResults { get; set; }

        public int ScanCalls { get; private set; }

        public int CleanupCalls { get; private set; }

        public int SaveCalls { get; private set; }

        public int ListResultsCalls { get; private set; }

        public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null)
        {
            ScanCalls++;
            return Task.FromResult(ScanTargets.ToList());
        }

        public Task<TempCleanupResult> CleanupAsync(
            IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null)
        {
            CleanupCalls++;
            return Task.FromResult(new TempCleanupResult
            {
                StartedAt = DateTime.Now.AddSeconds(-2),
                FinishedAt = DateTime.Now,
                DeletedFiles = 3,
                DeletedBytes = 1024 * 1024
            });
        }

        public Task<string> SaveResultAsync(TempCleanupResult result)
        {
            SaveCalls++;
            return Task.FromResult("cleanup-result-sintetico.json");
        }

        public Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20)
        {
            ListResultsCalls++;
            if (ThrowOnListResults)
                throw new IOException(@"Synthetic PRIVATE C:\Users\PRIVATE\cleanup-results");
            return Task.FromResult(History.ToList());
        }
    }
}
