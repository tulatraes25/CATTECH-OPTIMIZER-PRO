using System.Xml.Linq;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Reports;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Regresiones de las correcciones de SMART e Informes:
/// - el análisis sin reportes no muestra un falso éxito e informa los errores;
/// - la consulta de estado no informa éxito cuando la consulta falló;
/// - la carpeta de informes se crea la primera vez para que el botón funcione;
/// - abrir un informe/PDF inexistente muestra un aviso;
/// - los comandos de CrashEvidence se deshabilitan mientras hay una consulta.
/// Todos los tests usan fakes: no se ejecuta smartctl, DumpChk ni el Explorador.
/// </summary>
public class SmartReportFixTests
{
    // =====================
    // A) SMART: análisis sin reportes
    // =====================

    [Fact]
    public async Task AnalyzeSmart_ZeroReports_ShowsServiceErrorsInsteadOfFalseSuccess()
    {
        var diskService = new FakeSmartDiskService
        {
            Analysis = new SmartAnalysisResult
            {
                Errors = { "No se pudo listar dispositivos: acceso denegado" }
            }
        };
        var vm = CreateSmartViewModel(diskService);

        await vm.AnalyzeSmartCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("No se pudo completar el análisis SMART", vm.ErrorMessage);
        Assert.Contains("acceso denegado", vm.ErrorMessage);
        Assert.Empty(vm.Reports);
        Assert.False(vm.HasResults);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task AnalyzeSmart_ZeroReports_WithoutServiceErrors_StillShowsError()
    {
        var vm = CreateSmartViewModel(new FakeSmartDiskService
        {
            Analysis = new SmartAnalysisResult()
        });

        await vm.AnalyzeSmartCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("ningún disco", vm.ErrorMessage);
        Assert.False(vm.HasResults);
    }

    [Fact]
    public async Task AnalyzeSmart_PartialServiceErrors_ShowsErrorAndKeepsReports()
    {
        var diskService = new FakeSmartDiskService
        {
            Analysis = new SmartAnalysisResult
            {
                DevicesAnalyzed = 2,
                Reports = { new SmartDiskReport { Device = "/dev/sda", HealthStatus = SmartHealthStatus.Good } },
                Errors = { "Error al analizar /dev/sdb: timeout" }
            }
        };
        var vm = CreateSmartViewModel(diskService);

        await vm.AnalyzeSmartCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("timeout", vm.ErrorMessage);
        Assert.True(vm.HasResults);
        Assert.Single(vm.Reports);
    }

    [Fact]
    public async Task AnalyzeSmart_AllGoodReports_StillShowsSuccess()
    {
        var diskService = new FakeSmartDiskService
        {
            Analysis = new SmartAnalysisResult
            {
                DevicesAnalyzed = 1,
                Reports = { new SmartDiskReport { ModelName = "Synthetic SSD", HealthStatus = SmartHealthStatus.Good } }
            }
        };
        var vm = CreateSmartViewModel(diskService);

        await vm.AnalyzeSmartCommand.ExecuteAsync(null);

        Assert.True(vm.IsSuccess);
        Assert.False(vm.HasError);
        Assert.Contains("buen estado", vm.StatusMessage);
        Assert.Single(vm.Reports);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task AnalyzeSmart_WhileRunning_IsRejectedWithoutASecondExecution()
    {
        var diskService = new FakeSmartDiskService();
        var vm = CreateSmartViewModel(diskService);
        vm.IsRunning = true;

        Assert.False(vm.CanAnalyzeSmart);

        await vm.AnalyzeSmartCommand.ExecuteAsync(null);

        Assert.Equal(0, diskService.AnalyzeCalls);
        Assert.True(vm.HasError);
        Assert.Contains("operación en curso", vm.ErrorMessage);
    }

    [Fact]
    public async Task CheckTestStatus_WhenServiceCheckFails_ShowsVisibleError()
    {
        var vm = CreateSmartViewModel(new FakeSmartDiskService());
        vm.CurrentTestSession = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress
        };

        await vm.CheckTestStatusCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("No se pudo consultar el estado del test", vm.ErrorMessage);
        Assert.Contains("No se pudo consultar el estado", vm.TestStatusText);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task EnsureInitialized_RunsSmartctlCheck_SoActionsAreNotDisabledOnOpen()
    {
        var runner = new FakeSmartctlRunner();
        var vm = new SmartDiskViewModel(runner, new FakeSmartDiskService(), new FakeSmartTestService());

        Assert.False(vm.SmartctlAvailable);
        Assert.False(vm.CanDetectDevices);
        Assert.False(vm.CanAnalyzeSmart);

        await vm.EnsureInitializedAsync();

        Assert.Equal(1, runner.AvailabilityCalls);
        Assert.True(vm.SmartctlAvailable);
        Assert.True(vm.CanDetectDevices);
        Assert.True(vm.CanAnalyzeSmart);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void SmartDiskView_BindsStatusTextAndBusyIndicator()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.UI/Views/SmartDiskView.xaml"));
        var xml = XDocument.Load(path);

        Assert.Contains(xml.Descendants(), element =>
            (element.Attribute("Text")?.Value ?? string.Empty)
                .Contains("{Binding StatusText}", StringComparison.Ordinal));
        Assert.Contains(xml.Descendants(), element =>
            element.Name.LocalName == "ProgressBar");
    }

    // =====================
    // B) Informes: carpeta de informes
    // =====================

    [Fact]
    public async Task OpenReportsFolder_CreatesReportsDirectory_OnFirstUse()
    {
        using var temp = new TempDirectory();
        string? openedDirectory = null;
        var service = new HtmlReportService(temp.Path, path => openedDirectory = path);
        var expected = Path.Combine(temp.Path, "reports", "html");
        Assert.False(Directory.Exists(expected));

        await service.OpenReportsFolderAsync();

        Assert.True(Directory.Exists(expected));
        Assert.Equal(expected, openedDirectory);
    }

    [Fact]
    public async Task OpenReportsFolder_WhenShellLaunchFails_ThrowsVisibleError()
    {
        using var temp = new TempDirectory();
        var service = new HtmlReportService(
            temp.Path, _ => throw new System.ComponentModel.Win32Exception());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.OpenReportsFolderAsync());

        Assert.Contains("carpeta de informes", exception.Message);
        Assert.True(Directory.Exists(Path.Combine(temp.Path, "reports", "html")));
    }

    [Fact]
    public async Task OpenReportsFolderCommand_WhenServiceFails_ShowsVisibleError()
    {
        var reportService = new FakeReportGenerationService { FailOpenReportsFolder = true };
        var vm = CreateReportViewModel(reportService);

        await vm.OpenReportsFolderCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("carpeta de informes", vm.ErrorMessage);
    }

    [Fact]
    public async Task OpenReportsFolderCommand_WhenServiceSucceeds_ShowsConfirmation()
    {
        var vm = CreateReportViewModel(new FakeReportGenerationService());

        await vm.OpenReportsFolderCommand.ExecuteAsync(null);

        Assert.True(vm.IsSuccess);
        Assert.False(vm.HasError);
        Assert.Contains("Carpeta de informes", vm.StatusMessage);
    }

    [Fact]
    public async Task OpenReport_WhenGeneratedFileNoLongerExists_ShowsVisibleError()
    {
        var reportService = new FakeReportGenerationService();
        var vm = CreateReportViewModel(reportService);
        vm.HasGeneratedReport = true;
        vm.LastReportPath = Path.Combine(Path.GetTempPath(),
            "cattech-missing-" + Guid.NewGuid().ToString("N") + ".html");

        await vm.OpenReportCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("ya no existe", vm.ErrorMessage);
        Assert.Equal(0, reportService.OpenReportCalls);
    }

    [Fact]
    public async Task OpenPdf_WhenGeneratedFileNoLongerExists_ShowsVisibleError()
    {
        var pdf = new FakePdfExportService();
        var vm = CreateReportViewModel(new FakeReportGenerationService(), pdf);
        vm.HasGeneratedPdf = true;
        vm.LastPdfPath = Path.Combine(Path.GetTempPath(),
            "cattech-missing-" + Guid.NewGuid().ToString("N") + ".pdf");

        await vm.OpenPdfCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("ya no existe", vm.ErrorMessage);
        Assert.Equal(0, pdf.OpenPdfCalls);
    }

    [Fact]
    public async Task OpenReport_WhenFileExists_DelegatesToService()
    {
        var reportService = new FakeReportGenerationService();
        var vm = CreateReportViewModel(reportService);
        var path = Path.Combine(Path.GetTempPath(),
            "cattech-open-" + Guid.NewGuid().ToString("N") + ".html");
        File.WriteAllText(path, "<html><body>sintético</body></html>");

        try
        {
            vm.HasGeneratedReport = true;
            vm.LastReportPath = path;

            await vm.OpenReportCommand.ExecuteAsync(null);

            Assert.Equal(1, reportService.OpenReportCalls);
            Assert.False(vm.HasError);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    // =====================
    // C) CrashEvidence: CanExecute ligado a IsBusy
    // =====================

    [Fact]
    public void CrashEvidence_RefreshCommand_IsDisabledWhileBusy()
    {
        var vm = new CrashEvidenceViewModel(
            new FakeCrashDumpDiscovery(), new FakeCrashEventDiscovery(), new FakeBugCheckDecoder());

        Assert.True(vm.RefreshCommand.CanExecute(null));

        var notifications = 0;
        vm.RefreshCommand.CanExecuteChanged += (_, _) => notifications++;

        vm.IsBusy = true;

        Assert.False(vm.RefreshCommand.CanExecute(null));
        Assert.True(notifications > 0);

        vm.IsBusy = false;

        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task CrashEvidence_RefreshWhileBusy_DoesNotQueryTheMachine()
    {
        var dumps = new FakeCrashDumpDiscovery();
        var events = new FakeCrashEventDiscovery();
        var vm = new CrashEvidenceViewModel(dumps, events, new FakeBugCheckDecoder());
        vm.IsBusy = true;

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, dumps.Calls);
        Assert.Equal(0, events.Calls);
    }

    // =====================
    // Helpers
    // =====================

    private static SmartDiskViewModel CreateSmartViewModel(FakeSmartDiskService diskService)
    {
        return new SmartDiskViewModel(
            new FakeSmartctlRunner(),
            diskService,
            new FakeSmartTestService())
        {
            SmartctlAvailable = true
        };
    }

    private static ReportViewModel CreateReportViewModel(
        IReportGenerationService reportService,
        IPdfExportService? pdfExportService = null)
    {
        // Los servicios restantes no participan en los comandos de apertura.
        return new ReportViewModel(
            reportService,
            pdfExportService ?? new FakePdfExportService(),
            null!, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "cattech-smartreport-" + Guid.NewGuid().ToString("N")[..8]);

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Un handle transitorio del sistema puede demorar el borrado.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FakeSmartctlRunner : ISmartctlRunner
    {
        public int AvailabilityCalls { get; private set; }

        public Task<SmartctlAvailability> CheckAvailabilityAsync()
        {
            AvailabilityCalls++;
            return Task.FromResult(new SmartctlAvailability
            {
                IsAvailable = true,
                SmartctlPath = "smartctl.exe",
                Version = "smartctl 7.4",
                SupportsJson = true
            });
        }

        public Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout) =>
            Task.FromResult(new SmartctlCommandResult());

        public Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync() =>
            Task.FromResult<IReadOnlyList<SmartDiskDevice>>(Array.Empty<SmartDiskDevice>());
    }

    private sealed class FakeSmartDiskService : ISmartDiskService
    {
        public SmartAnalysisResult Analysis { get; set; } = new();

        public int AnalyzeCalls { get; private set; }

        public Task<SmartAnalysisResult> AnalyzeAllDisksAsync()
        {
            AnalyzeCalls++;
            return Task.FromResult(Analysis);
        }

        public Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device) =>
            Task.FromResult(new SmartDiskReport());

        public Task<string> SaveResultAsync(SmartAnalysisResult result) =>
            Task.FromResult("analysis-sintetico.json");

        public Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(int maxResults = 20) =>
            Task.FromResult<IReadOnlyList<SmartAnalysisResult>>(Array.Empty<SmartAnalysisResult>());
    }

    private sealed class FakeSmartTestService : ISmartTestService
    {
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

        public Task<IReadOnlyList<SmartTestSession>> ListSessionsAsync(int maxResults = 20) =>
            Task.FromResult<IReadOnlyList<SmartTestSession>>(Array.Empty<SmartTestSession>());
    }

    private sealed class FakeCrashDumpDiscovery : ICrashDumpDiscoveryService
    {
        public int Calls { get; private set; }

        public CrashDumpDiscoveryResult Discover()
        {
            Calls++;
            return new CrashDumpDiscoveryResult { Status = CrashDumpDiscoveryStatus.NoDumps };
        }
    }

    private sealed class FakeCrashEventDiscovery : ICrashEventDiscoveryService
    {
        public int Calls { get; private set; }

        public CrashEventDiscoveryResult Discover()
        {
            Calls++;
            return new CrashEventDiscoveryResult { Status = CrashEventDiscoveryStatus.NoEvents };
        }
    }

    private sealed class FakeBugCheckDecoder : IBugCheckDecoder
    {
        public BugCheckDecodeResult Decode(uint code) => new() { Code = code, IsKnown = false };
    }

    private sealed class FakeReportGenerationService : IReportGenerationService
    {
        public bool FailOpenReportsFolder { get; set; }

        public int OpenReportCalls { get; private set; }

        public Task<string> GenerateHtmlReportAsync(ReportGenerationOptions options) =>
            Task.FromResult("informe-sintetico.html");

        public Task SaveReportInfoAsync(GeneratedReportInfo info) => Task.CompletedTask;

        public Task<List<GeneratedReportInfo>> ListGeneratedReportsAsync(int maxResults = 20) =>
            Task.FromResult(new List<GeneratedReportInfo>());

        public Task OpenReportAsync(string htmlPath)
        {
            OpenReportCalls++;
            return Task.CompletedTask;
        }

        public Task OpenReportsFolderAsync()
        {
            if (FailOpenReportsFolder)
                throw new InvalidOperationException(
                    "No se pudo abrir la carpeta de informes. Permisos insuficientes.");
            return Task.CompletedTask;
        }
    }

    private sealed class FakePdfExportService : IPdfExportService
    {
        public int OpenPdfCalls { get; private set; }

        public Task<PdfExporterInfo> CanExportAsync() =>
            Task.FromResult(new PdfExporterInfo { IsAvailable = true });

        public Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath) =>
            Task.FromResult(false);

        public string GetPdfOutputPath(string htmlPath) =>
            System.IO.Path.ChangeExtension(htmlPath, ".pdf");

        public Task OpenPdfAsync(string pdfPath)
        {
            OpenPdfCalls++;
            return Task.CompletedTask;
        }
    }
}
