using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Reports;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ReportViewModelTests
{
    // ===== TEST A: HtmlReportService directo =====

    [Fact]
    public async Task HtmlReportService_WithClientAndDiagnostic_RendersBothSections()
    {
        using var tempDir = new TempDirectory();

        var service = new HtmlReportService();

        var serviceReport = new ServiceReport
        {
            Client = new ClientInfo { Name = "Cliente QA" },
            Equipment = new EquipmentInfo { Brand = "CATTECH", Model = "Notebook QA" },
            Service = new ServiceInfo { Reason = "Smoke Report Test" }
        };

        var diagnosticReport = new DiagnosticReport
        {
            OsName = "Microsoft Windows 11 Pro"
        };

        var options = new ReportGenerationOptions
        {
            IncludeCompany = false,
            IncludeClient = true,
            ServiceReport = serviceReport,
            IncludeDiagnostic = true,
            DiagnosticReport = diagnosticReport,
            IncludeStartup = false,
            IncludeCleanup = false,
            IncludeVisualOptimization = false,
            IncludeRestorePoint = false,
            IncludeSmart = false,
            IncludeSmartTests = false,
            IncludeRecommendations = false
        };

        var filePath = await service.GenerateHtmlReportAsync(options);

        Assert.False(string.IsNullOrEmpty(filePath));
        Assert.False(filePath.Contains("SinCliente"), $"Filename should not contain 'SinCliente': {filePath}");

        var html = await File.ReadAllTextAsync(filePath);

        Assert.Contains("Cliente QA", html);
        Assert.Contains("Notebook QA", html);
        Assert.Contains("CATTECH", html);
        Assert.Contains("Microsoft Windows 11 Pro", html);
    }

    // ===== TEST B: ReportViewModel Fresh Load→Generate =====

    [Fact]
    public async Task ReportViewModel_LoadThenGenerate_PassesCurrentSelections()
    {
        var (vm, fakeReportService) = CreateViewModel();

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasClientData);
        Assert.NotNull(vm.SelectedServiceReport);
        Assert.Equal("Cliente QA", vm.SelectedServiceReport.Client.Name);
        Assert.True(vm.IncludeClient);

        Assert.True(vm.HasDiagnosticData);
        Assert.NotNull(vm.SelectedDiagnosticReport);
        Assert.Equal("Microsoft Windows 11 Pro", vm.SelectedDiagnosticReport.OsName);
        Assert.True(vm.IncludeDiagnostic);

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.NotNull(fakeReportService.LastCapturedOptions);
        Assert.NotNull(fakeReportService.LastCapturedOptions.ServiceReport);
        Assert.Equal("Cliente QA", fakeReportService.LastCapturedOptions.ServiceReport.Client.Name);
        Assert.NotNull(fakeReportService.LastCapturedOptions.DiagnosticReport);
        Assert.Equal("Microsoft Windows 11 Pro", fakeReportService.LastCapturedOptions.DiagnosticReport.OsName);
        Assert.True(fakeReportService.LastCapturedOptions.IncludeClient);
        Assert.True(fakeReportService.LastCapturedOptions.IncludeDiagnostic);
    }

    // ===== TEST C: Stale invalidation =====

    [Fact]
    public async Task ReportViewModel_LoadData_InvalidatesStaleGeneratedArtifacts()
    {
        var (vm, _) = CreateViewModel();

        // Simular estado stale: un HTML anterior generado
        vm.LastReportPath = @"C:\fake\Informe_Tecnico_CATTECH_SinCliente_old.html";
        vm.HasGeneratedReport = true;
        vm.LastPdfPath = @"C:\fake\Informe_Tecnico_CATTECH_SinCliente_old.pdf";
        vm.HasGeneratedPdf = true;
        vm.PdfStatusText = "PDF generado";

        // Ejecutar LoadData (debe invalidar artifacts stale)
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, vm.LastReportPath);
        Assert.False(vm.HasGeneratedReport);
        Assert.Equal(string.Empty, vm.LastPdfPath);
        Assert.False(vm.HasGeneratedPdf);
        Assert.Equal(string.Empty, vm.PdfStatusText);
    }

    // ===== TEST D: ExportPdf always uses fresh HTML =====

    [Fact]
    public async Task ReportViewModel_ExportPdf_AfterReload_UsesFreshHtml()
    {
        var (vm, fakeReportService) = CreateViewModel();

        // Cargar datos primero
        await vm.LoadDataCommand.ExecuteAsync(null);

        // Simular estado stale después del load
        vm.LastReportPath = @"C:\fake\Informe_Tecnico_CATTECH_SinCliente_old.html";
        vm.HasGeneratedReport = true;

        // ExportPdf debe regenerar HTML con opciones actuales
        await vm.ExportPdfCommand.ExecuteAsync(null);

        // Verificar que GenerateHtmlReportAsync fue llamado
        Assert.True(fakeReportService.GenerateCallCount >= 1,
            "GenerateHtmlReportAsync should have been called at least once");

        // Verificar que el HTML generado NO es el stale path
        Assert.NotEqual(@"C:\fake\Informe_Tecnico_CATTECH_SinCliente_old.html", vm.LastReportPath);
        Assert.True(vm.HasGeneratedReport);

        // Verificar que las opciones capturadas tienen los datos actuales
        Assert.NotNull(fakeReportService.LastCapturedOptions);
        Assert.NotNull(fakeReportService.LastCapturedOptions.ServiceReport);
        Assert.Equal("Cliente QA", fakeReportService.LastCapturedOptions.ServiceReport.Client.Name);
    }

    // ===== TEST E: Missing selection guard (GenerateReport) =====

    [Fact]
    public async Task ReportViewModel_GenerateReport_WithNullServiceReport_ShowsError()
    {
        var (vm, fakeReportService) = CreateViewModel();

        // Simular estado donde IncludeClient=true pero no hay ServiceReport seleccionado
        vm.IncludeClient = true;
        vm.SelectedServiceReport = null;
        vm.HasClientData = true;

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.True(vm.HasError, "Should show error when IncludeClient=true but ServiceReport is null");
        Assert.Contains("cliente", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // No se debe haber llamado a GenerateHtmlReportAsync
        Assert.Equal(0, fakeReportService.GenerateCallCount);
    }

    // ===== TEST F: Missing selection guard (ExportPdf) =====

    [Fact]
    public async Task ReportViewModel_ExportPdf_WithNullDiagnosticReport_ShowsError()
    {
        var (vm, fakeReportService) = CreateViewModel();

        // Simular estado donde IncludeDiagnostic=true pero no hay DiagnosticReport seleccionado
        // IncludeClient=false para que el guard de cliente no se active primero
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = true;
        vm.SelectedDiagnosticReport = null;
        vm.HasDiagnosticData = true;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.True(vm.HasError, "Should show error when IncludeDiagnostic=true but DiagnosticReport is null");
        Assert.Contains("diagnóstico", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // No se debe haber llamado a GenerateHtmlReportAsync
        Assert.Equal(0, fakeReportService.GenerateCallCount);
    }

    // ===== Helper =====

    private static (ReportViewModel vm, FakeReportGenerationService fakeReportService) CreateViewModel()
    {
        var fakeReportService = new FakeReportGenerationService();
        var vm = new ReportViewModel(
            fakeReportService,
            new FakePdfExportService(),
            new FakeSettingsService(),
            new FakeServiceReportService(),
            new FakeDiagnosticService(),
            new FakeStartupService(),
            new FakeCleanupService(),
            new FakeVisualOptimizationService(),
            new FakeRestorePointService(),
            new FakeSmartDiskService(),
            new FakeSmartTestService());
        return (vm, fakeReportService);
    }

    // ===== Fakes =====

    private class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cattech-test-" + Guid.NewGuid().ToString("N")[..8]);

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, true); } catch { }
        }
    }

    private class FakeReportGenerationService : IReportGenerationService
    {
        public ReportGenerationOptions? LastCapturedOptions { get; private set; }
        public int GenerateCallCount { get; private set; }

        public Task<string> GenerateHtmlReportAsync(ReportGenerationOptions options)
        {
            LastCapturedOptions = options;
            GenerateCallCount++;

            var clientName = options.ServiceReport?.Client?.Name ?? "SinCliente";
            var fileName = $"Informe_Tecnico_CATTECH_{clientName}_{DateTime.Now:yyyyMMdd-HHmmss}.html";
            var filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, $"<html><body>Test report for {clientName}</body></html>");
            return Task.FromResult(filePath);
        }

        public Task SaveReportInfoAsync(GeneratedReportInfo info) => Task.CompletedTask;
        public Task<List<GeneratedReportInfo>> ListGeneratedReportsAsync(int maxResults = 20) => Task.FromResult(new List<GeneratedReportInfo>());
        public Task OpenReportAsync(string htmlPath) => Task.CompletedTask;
        public Task OpenReportsFolderAsync() => Task.CompletedTask;
    }

    private class FakePdfExportService : IPdfExportService
    {
        public Task<PdfExporterInfo> CanExportAsync() => Task.FromResult(new PdfExporterInfo { IsAvailable = true });
        public Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath)
        {
            File.WriteAllText(outputPdfPath, "%PDF-1.4 fake");
            return Task.FromResult(true);
        }
        public string GetPdfOutputPath(string htmlPath) => htmlPath.Replace(".html", ".pdf");
        public Task OpenPdfAsync(string pdfPath) => Task.CompletedTask;
    }

    private class FakeSettingsService : ISettingsService
    {
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(new AppSettings
        {
            Company = new CompanyInfo { Name = "CATTECH QA" },
            Technician = new TechnicianInfo { Name = "Técnico QA" }
        });
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public AppSettings CurrentSettings => new()
        {
            Company = new CompanyInfo { Name = "CATTECH QA" },
            Technician = new TechnicianInfo { Name = "Técnico QA" }
        };
        public event EventHandler<AppSettings>? SettingsChanged;
    }

    private class FakeServiceReportService : IServiceReportService
    {
        public Task<List<ServiceReportSummary>> ListReportsAsync(int maxResults = 50)
        {
            return Task.FromResult(new List<ServiceReportSummary>
            {
                new() { Id = "1", ClientName = "Cliente QA", EquipmentBrand = "CATTECH", EquipmentModel = "Notebook QA" }
            });
        }

        public Task<ServiceReport?> LoadReportAsync(string reportId)
        {
            return Task.FromResult<ServiceReport?>(new ServiceReport
            {
                Id = reportId,
                Client = new ClientInfo { Name = "Cliente QA" },
                Equipment = new EquipmentInfo { Brand = "CATTECH", Model = "Notebook QA" },
            Service = new ServiceInfo { Reason = "Smoke Report Test" }
            });
        }

        public Task<string> SaveReportAsync(ServiceReport report) => Task.FromResult(report.Id);
        public Task<bool> DeleteReportAsync(string reportId) => Task.FromResult(true);
    }

    private class FakeDiagnosticService : IDiagnosticService
    {
        public Task<List<DiagnosticSummary>> ListDiagnosticsAsync(int maxResults = 20)
        {
            return Task.FromResult(new List<DiagnosticSummary>
            {
                new() { Id = "1", OsName = "Microsoft Windows 11 Pro" }
            });
        }

        public Task<DiagnosticReport?> LoadDiagnosticAsync(string diagnosticId)
        {
            return Task.FromResult<DiagnosticReport?>(new DiagnosticReport
            {
                Id = diagnosticId,
                OsName = "Microsoft Windows 11 Pro"
            });
        }

        public Task<DiagnosticReport> RunQuickDiagnosticAsync(IProgress<int>? progress = null) =>
            Task.FromResult(new DiagnosticReport { OsName = "Microsoft Windows 11 Pro" });
        public Task<string> SaveDiagnosticAsync(DiagnosticReport report) => Task.FromResult(report.Id);
        public Task<bool> DeleteDiagnosticAsync(string diagnosticId) => Task.FromResult(true);
    }

    private class FakeStartupService : IStartupService
    {
        public Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20) => Task.FromResult(new List<StartupAnalysisSummary>());
        public Task<StartupAnalysis?> LoadAnalysisAsync(string analysisId) => Task.FromResult<StartupAnalysis?>(null);
        public Task<StartupAnalysis> AnalyzeStartupAsync() => Task.FromResult(new StartupAnalysis());
        public Task<string> SaveAnalysisAsync(StartupAnalysis analysis) => Task.FromResult("1");
        public Task<bool> DeleteAnalysisAsync(string analysisId) => Task.FromResult(true);
        public bool CanDisableStartupEntry(StartupEntry entry) => false;
        public Task<StartupDisableSummary> DisableSelectedAsync(IEnumerable<StartupEntry> entries, string reason = "") => Task.FromResult(new StartupDisableSummary());
        public Task<StartupActionResult> RestoreAsync(StartupBackupRecord backup) => Task.FromResult(new StartupActionResult());
        public Task<List<StartupBackupRecord>> ListBackupsAsync() => Task.FromResult(new List<StartupBackupRecord>());
        public Task<StartupBackupRecord?> LoadBackupAsync(string backupId) => Task.FromResult<StartupBackupRecord?>(null);
    }

    private class FakeCleanupService : ITempCleanupService
    {
        public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null) => Task.FromResult(new List<TempCleanupTarget>());
        public Task<TempCleanupResult> CleanupAsync(IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null) => Task.FromResult(new TempCleanupResult());
        public Task<string> SaveResultAsync(TempCleanupResult result) => Task.FromResult("1");
        public Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20) => Task.FromResult(new List<TempCleanupResultSummary>());
    }

    private class FakeVisualOptimizationService : IVisualOptimizationService
    {
        public Task<List<VisualOptimizationSetting>> AnalyzeAsync() => Task.FromResult(new List<VisualOptimizationSetting>());
        public Task<VisualOptimizationResult> ApplyAsync(IEnumerable<VisualOptimizationSetting> settings, string reason = "") => Task.FromResult(new VisualOptimizationResult());
        public Task<List<VisualOptimizationBackup>> ListBackupsAsync() => Task.FromResult(new List<VisualOptimizationBackup>());
        public Task<bool> RestoreAsync(VisualOptimizationBackup backup) => Task.FromResult(true);
        public Task<string> SaveResultAsync(VisualOptimizationResult result) => Task.FromResult("1");
    }

    private class FakeRestorePointService : IRestorePointService
    {
        public Task<RestorePointStatus> CheckStatusAsync() => Task.FromResult(new RestorePointStatus());
        public Task<RestorePointResult> CreateRestorePointAsync(string name) => Task.FromResult(new RestorePointResult());
        public string GenerateRestorePointName() => "Test Restore Point";
        public Task<string> SaveResultAsync(RestorePointResult result) => Task.FromResult("1");
        public Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20) => Task.FromResult(new List<RestorePointResultSummary>());
    }

    private class FakeSmartDiskService : ISmartDiskService
    {
        public Task<SmartAnalysisResult> AnalyzeAllDisksAsync() => Task.FromResult(new SmartAnalysisResult());
        public Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device) => Task.FromResult(new SmartDiskReport());
        public Task<string> SaveResultAsync(SmartAnalysisResult result) => Task.FromResult("1");
        public Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(int maxResults = 20) => Task.FromResult<IReadOnlyList<SmartAnalysisResult>>(new List<SmartAnalysisResult>());
    }

    private class FakeSmartTestService : ISmartTestService
    {
        public Task<SmartTestSession> StartShortTestAsync(SmartDiskDevice device) => Task.FromResult(new SmartTestSession());
        public Task<SmartTestSession> StartExtendedTestAsync(SmartDiskDevice device) => Task.FromResult(new SmartTestSession());
        public Task<SmartTestSession> CheckStatusAsync(SmartTestSession session) => Task.FromResult(new SmartTestSession());
        public Task<SmartTestResult?> GetLatestResultAsync(SmartDiskDevice device) => Task.FromResult<SmartTestResult?>(null);
        public Task<string> SaveSessionAsync(SmartTestSession session) => Task.FromResult("1");
        public Task<IReadOnlyList<SmartTestSession>> ListSessionsAsync(int maxResults = 20) => Task.FromResult<IReadOnlyList<SmartTestSession>>(new List<SmartTestSession>());
    }
}
