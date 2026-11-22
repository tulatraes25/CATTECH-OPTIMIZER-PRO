using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;
using Cattech.Optimizer.Pro.Infrastructure.Data;
using Cattech.Optimizer.Pro.Infrastructure.Reports;
using Cattech.Optimizer.Pro.Infrastructure.VisualOptimization;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Regresiones de la pantalla Informes y de la robustez de lectura de fuentes:
/// - un archivo hermano corrupto/ilegible ya no aborta la resolución de un Id válido;
/// - un informe dañado no oculta los clientes válidos del inventario y se lista;
/// - las secciones de limpieza/optimización visual/restauración se habilitan con
///   datos persistidos reales (sin placeholders ni datos inventados);
/// - la optimización visual persiste su resultado al aplicar;
/// - "Verificar diagnóstico" informa el motivo técnico concreto del fallo.
///
/// Seguridad: todos los datos son sintéticos y descartables (directorios
/// temporales); no se ejecutan exportaciones PDF/Edge ni reparaciones reales.
/// </summary>
public sealed class ReportUxTests
{
    private static readonly DateTime FixedDate = new(2026, 11, 20, 10, 30, 0, DateTimeKind.Utc);

    // ===================== 1) Resolución de fuentes =====================

    [Fact]
    public async Task ResolveUnique_UnrelatedCorruptSibling_StillResolvesValidTarget()
    {
        var root = NewTempRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/diagnostic-target.json",
                "{\"id\":\"ABCDEF12\",\"osName\":\"Windows QA\"}");
            await WriteSourceAsync(root, "diagnostics/diagnostic-damaged.json",
                "{ esto no es JSON válido");
            await WriteSourceAsync(root, "diagnostics/diagnostic-other.json",
                "{\"id\":\"DEADBEEF\"}");

            var result = await new ReportSourceFingerprintReader(root)
                .ResolveUniqueAsync("Diagnostic", "ABCDEF12");

            Assert.Equal(SourceIdResolutionStatus.FoundUnique, result.Status);
            Assert.Equal("diagnostics/diagnostic-target.json",
                result.Fingerprint!.RelativePath);
            Assert.Equal("ABCDEF12", result.Fingerprint.SourceRecordId);
            Assert.Equal(1, result.OmittedUnreadableFiles);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ResolveUnique_TwoReadableSameId_IsAmbiguousAndNeverPicksOne()
    {
        var root = NewTempRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/diagnostic-a.json",
                "{\"id\":\"ABCDEF12\",\"value\":1}");
            await WriteSourceAsync(root, "diagnostics/diagnostic-b.json",
                "{\"id\":\"ABCDEF12\",\"value\":2}");

            var result = await new ReportSourceFingerprintReader(root)
                .ResolveUniqueAsync("Diagnostic", "ABCDEF12");

            Assert.Equal(SourceIdResolutionStatus.Ambiguous, result.Status);
            Assert.Null(result.Fingerprint);
        }
        finally { DeleteRoot(root); }
    }

    [Fact]
    public async Task ResolveUnique_CorruptStoreWithoutReadableTarget_ReturnsNotFound()
    {
        var root = NewTempRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/diagnostic-damaged.json",
                "not json at all");
            await WriteSourceAsync(root, "diagnostics/diagnostic-other.json",
                "{\"id\":\"DEADBEEF\"}");

            var result = await new ReportSourceFingerprintReader(root)
                .ResolveUniqueAsync("Diagnostic", "ABCDEF12");

            // Nunca se inventa un match a partir de un archivo ilegible.
            Assert.Equal(SourceIdResolutionStatus.NotFound, result.Status);
            Assert.Null(result.Fingerprint);
            Assert.Equal(1, result.OmittedUnreadableFiles);
        }
        finally { DeleteRoot(root); }
    }

    // ===================== 2) Inventario de informes =====================

    [Fact]
    public async Task Inventory_WithDamagedFile_ListsItAndKeepsValidRecordsReadable()
    {
        var root = NewTempRoot();
        try
        {
            var store = new JsonServiceReportService(root);
            var first = new ServiceReport
            {
                Id = "QA000001",
                Client = new ClientInfo { Name = "Cliente QA Uno" }
            };
            var second = new ServiceReport
            {
                Id = "QA000002",
                Client = new ClientInfo { Name = "Cliente QA Dos" }
            };
            await store.SaveReportAsync(first);
            await store.SaveReportAsync(second);

            var directory = Path.Combine(root, "data", "service-reports");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "service-report-damaged.json"), "{oops");

            var detail = await ((IServiceReportInventoryDetailReader)store)
                .InspectReportInventoryDetailedAsync();

            Assert.Equal(3, detail.Inventory.TotalFiles);
            Assert.Equal(2, detail.Inventory.UniqueEntries);
            Assert.Equal(1, detail.Inventory.InvalidEntries);
            Assert.True(detail.Inventory.RequiresReview);
            Assert.True(detail.HasDamagedFiles);
            var damaged = Assert.Single(detail.DamagedFiles);
            Assert.Equal("service-report-damaged.json", damaged);
            Assert.DoesNotContain("\\", damaged);
            Assert.DoesNotContain("/", damaged);

            // El archivo dañado no oculta los informes válidos restantes.
            Assert.NotNull(await store.LoadReportAsync(first.Id));
            Assert.NotNull(await store.LoadReportAsync(second.Id));
            Assert.Equal(2, (await store.ListReportsAsync()).Count);
            // Ningún archivo dañado fue eliminado por la inspección.
            Assert.True(File.Exists(Path.Combine(directory, "service-report-damaged.json")));
        }
        finally { DeleteRoot(root); }
    }

    // ===================== 3) ReportViewModel: inventario dañado =====================

    [Fact]
    public async Task ReportViewModel_DamagedInventory_ShowsFileNamesAndKeepsValidClients()
    {
        var store = new FakeServiceReportService
        {
            Inventory = new ServiceReportInventory(2, 1, 0, 1, false),
            DamagedFiles = new[] { "service-report-damaged.json" }
        };
        var (vm, _) = CreateViewModel(store: store);

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasDamagedReportFiles);
        var damaged = Assert.Single(vm.DamagedReportFiles);
        Assert.Equal("service-report-damaged.json", damaged);
        Assert.Contains("service-report-damaged.json", vm.ReportInventoryMessage);
        // Los clientes válidos siguen disponibles pese al hermano dañado.
        Assert.True(vm.HasClientData);
        Assert.True(vm.IncludeClient);
        Assert.NotNull(vm.SelectedServiceReport);
        Assert.Equal(1, vm.ClientCount);
        Assert.Equal(1, vm.InventoryReportCount);
    }

    // ===================== 4) Secciones antes muertas =====================

    [Fact]
    public async Task ReportViewModel_CleanupAndRestoreSummaries_EnableSectionsWithRealTooltips()
    {
        var cleanup = new FakeCleanupService
        {
            Summaries =
            {
                new TempCleanupResultSummary
                {
                    Id = "CLN00001",
                    StartedAt = FixedDate,
                    DeletedMB = 12.5,
                    DeletedFiles = 33,
                    HasWarnings = true,
                    FileName = "cleanup-result-20261120-103000.json"
                }
            }
        };
        var restore = new FakeRestorePointService
        {
            Summaries =
            {
                new RestorePointResultSummary
                {
                    Id = "RST00001",
                    RequestedAt = FixedDate,
                    RestorePointName = "Punto QA",
                    Success = true,
                    FileName = "restore-point-result-20261120-103000.json"
                }
            }
        };
        var (vm, generator) = CreateViewModel(cleanup: cleanup, restore: restore);

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasCleanupHistory);
        Assert.False(vm.HasCleanupData); // no hay resultado completo exportable
        Assert.Contains("MB liberados", vm.CleanupSectionToolTip);
        Assert.Contains("33 archivos", vm.LastCleanupRecordText);
        Assert.Contains("Limitación", vm.CleanupSectionToolTip);
        Assert.Contains("cleanup-result-20261120-103000.json", vm.LastCleanupRecordText);

        Assert.True(vm.HasRestorePointHistory);
        Assert.False(vm.HasRestorePointData);
        Assert.Contains("Punto QA", vm.RestorePointSectionToolTip);
        Assert.Contains("creado", vm.LastRestorePointRecordText);
        Assert.Contains("Limitación", vm.RestorePointSectionToolTip);

        // No se inventa HTML: el modelo de informe no soporta solo-resumen.
        await vm.GenerateReportCommand.ExecuteAsync(null);
        var options = Assert.IsType<ReportGenerationOptions>(generator.LastCapturedOptions);
        Assert.False(options.IncludeCleanup);
        Assert.False(options.IncludeRestorePoint);
        Assert.Null(options.CleanupResult);
        Assert.Null(options.RestorePointResult);
        Assert.NotNull(generator.LastSavedInfo);
        Assert.DoesNotContain("Limpieza", generator.LastSavedInfo.IncludedSections);
        Assert.DoesNotContain("Restauración", generator.LastSavedInfo.IncludedSections);
    }

    [Fact]
    public async Task ReportViewModel_NoHistory_ShowsSinRegistrosTooltips()
    {
        var (vm, _) = CreateViewModel();

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.False(vm.HasCleanupHistory);
        Assert.False(vm.HasRestorePointHistory);
        Assert.Equal("sin registros", vm.CleanupSectionToolTip);
        Assert.Equal("sin registros", vm.RestorePointSectionToolTip);
        Assert.Equal("sin registros", vm.VisualOptimizationSectionToolTip);
        Assert.Equal("sin registros", vm.LastCleanupRecordText);
        Assert.False(vm.IncludeCleanup);
        Assert.False(vm.IncludeRestorePoint);
    }

    [Fact]
    public async Task ReportViewModel_VisualOptimizationResults_LoadAndPreselectOnlyClientFree()
    {
        var visualResult = new VisualOptimizationResult
        {
            Id = "VISO0001",
            StartedAt = FixedDate,
            AppliedCount = 3,
            FailedCount = 1,
            RequiresRestart = true
        };
        var visual = new FakeVisualOptimizationService();
        visual.Results.Add(visualResult);

        // Con cliente: los datos se cargan, pero la sección no se preselecciona
        // (política de atribución: no se mezcla con un informe nominal).
        var (withClient, _) = CreateViewModel(store: new FakeServiceReportService(), visual: visual);
        await withClient.LoadDataCommand.ExecuteAsync(null);
        Assert.True(withClient.HasVisualOptimizationData);
        Assert.NotNull(withClient.SelectedVisualOptimizationResult);
        Assert.False(withClient.IncludeVisualOptimization);
        Assert.Contains("3 aplicados", withClient.LastVisualOptimizationRecordText);
        Assert.Contains("Último resultado persistido",
            withClient.VisualOptimizationSectionToolTip);

        // Sin cliente: se preselecciona y se exporta el resultado real al HTML.
        var (clientFree, generator) = CreateViewModel(
            store: new FakeServiceReportService
            {
                ReportCount = 0,
                Inventory = new ServiceReportInventory(0, 0, 0, 0, false)
            },
            visual: visual);
        await clientFree.LoadDataCommand.ExecuteAsync(null);

        Assert.False(clientFree.HasClientData);
        Assert.False(clientFree.IncludeClient);
        Assert.True(clientFree.HasVisualOptimizationData);
        Assert.True(clientFree.IncludeVisualOptimization);

        await clientFree.GenerateReportCommand.ExecuteAsync(null);
        var options = Assert.IsType<ReportGenerationOptions>(generator.LastCapturedOptions);
        Assert.True(options.IncludeVisualOptimization);
        Assert.Same(visualResult, options.VisualOptimizationResult);
        Assert.NotNull(generator.LastSavedInfo);
        Assert.Contains("Optimización", generator.LastSavedInfo.IncludedSections);
    }

    // ===================== 5) Motivo técnico de la verificación =====================

    [Theory]
    [InlineData(SourceReviewCoordinationStatus.CaseUnavailable, "sin caso")]
    [InlineData(SourceReviewCoordinationStatus.AmbiguousSource, "ambiguo")]
    [InlineData(SourceReviewCoordinationStatus.StorageFailure, "lectura")]
    [InlineData(SourceReviewCoordinationStatus.SourceUnavailable, "JSON inválido")]
    public async Task ReportViewModel_VerifyDiagnosticFailure_ReportsSpecificCategory(
        SourceReviewCoordinationStatus status, string expectedCategory)
    {
        var coordinator = new FakeSourceReviewCoordinator { PrepareStatus = status };
        var (vm, _) = CreateViewModel(review: coordinator);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.IncludeClient);
        Assert.NotNull(vm.SelectedDiagnosticReport);

        await vm.PreviewDiagnosticReviewCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains(expectedCategory, vm.ReviewFailureCategory,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Motivo", vm.ErrorMessage);
        Assert.Contains(expectedCategory, vm.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.HasPendingSourceReview);
    }

    [Fact]
    public async Task ReportViewModel_TechnicalSummary_ShowsCountsAndIncludedSections()
    {
        var (vm, _) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.ClientCount);
        Assert.Equal(1, vm.DiagnosticCount);
        Assert.NotEmpty(vm.SectionRows);
        Assert.Contains(vm.SectionRows, row => row.Fuente == "Cliente/equipo");
        Assert.Contains(vm.SectionRows, row => row.Fuente == "Limpieza (historial)");
        Assert.False(string.IsNullOrWhiteSpace(vm.ReportsFolderPath));
        Assert.True(vm.IncludedSectionsCount >= 1);
        Assert.Contains("Cliente", vm.IncludedSectionsSummary);
        Assert.Contains("Recomendaciones", vm.IncludedSectionsSummary);
    }

    // ===================== 6) Persistencia de optimización visual =====================

    [Fact]
    public async Task VisualOptimizationViewModel_Apply_PersistsResultWithSaveResultAsync()
    {
        var service = new FakeVisualOptimizationService
        {
            SettingsFactory = () => new List<VisualOptimizationSetting>
            {
                new()
                {
                    Id = "QA_LOW",
                    DisplayName = "Ajuste QA",
                    RiskLevel = VisualRiskLevel.Low,
                    RegistryPath = @"Software\CATTECH\QA\Fake",
                    RegistryValueName = "QA",
                    CurrentValue = "1",
                    RecommendedValue = "0",
                    ValueType = "String",
                    IsSupported = true
                }
            },
            ApplyResult = new VisualOptimizationResult { AppliedCount = 1 }
        };
        var vm = new VisualOptimizationViewModel(service);

        await vm.AnalyzeCommand.ExecuteAsync(null);
        vm.Settings[0].IsSelected = true;
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(1, service.SaveCalls);
        Assert.Same(service.ApplyResult, service.SavedResult);
        Assert.Contains("Historial guardado", vm.ResultDetails);
        Assert.True(vm.IsSuccess);
    }

    [Fact]
    public async Task VisualOptimizationService_ListResults_SkipsCorruptAndOrdersNewestFirst()
    {
        var root = NewTempRoot();
        try
        {
            var service = new VisualOptimizationService(root);
            var older = new VisualOptimizationResult
            {
                Id = "OLD00001",
                StartedAt = FixedDate.AddHours(-2),
                AppliedCount = 1
            };
            var newer = new VisualOptimizationResult
            {
                Id = "NEW00002",
                StartedAt = FixedDate,
                AppliedCount = 2
            };
            await service.SaveResultAsync(older);
            await service.SaveResultAsync(newer);

            var resultsDirectory = Path.Combine(
                root, "data", "visual-optimization-results");
            await File.WriteAllTextAsync(
                Path.Combine(resultsDirectory, "visual-optimization-result-corrupt.json"),
                "no es json");

            var listed = await service.ListResultsAsync(5);
            Assert.Equal(2, listed.Count);
            Assert.Equal("NEW00002", listed[0].Id);
            Assert.Equal("OLD00001", listed[1].Id);

            var latest = await service.LoadLatestResultAsync();
            Assert.NotNull(latest);
            Assert.Equal("NEW00002", latest.Id);
        }
        finally { DeleteRoot(root); }
    }

    // ===================== Helpers =====================

    private static (ReportViewModel vm, FakeReportGenerationService generator)
        CreateViewModel(
            FakeServiceReportService? store = null,
            FakeCleanupService? cleanup = null,
            FakeRestorePointService? restore = null,
            FakeVisualOptimizationService? visual = null,
            IReportSourceReviewCoordinator? review = null)
    {
        var generator = new FakeReportGenerationService();
        var vm = new ReportViewModel(
            generator,
            new FakePdfExportService(),
            new FakeSettingsService(),
            store ?? new FakeServiceReportService(),
            new FakeDiagnosticService(),
            new FakeStartupService(),
            cleanup ?? new FakeCleanupService(),
            visual ?? new FakeVisualOptimizationService(),
            restore ?? new FakeRestorePointService(),
            new FakeSmartDiskService(),
            new FakeSmartTestService(),
            review);
        return (vm, generator);
    }

    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(),
            "cattech-report-ux-" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static async Task WriteSourceAsync(
        string root, string relative, string content)
    {
        var path = Path.Combine(root, "data",
            relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    // ===================== Fakes de prueba =====================

    private sealed class FakeReportGenerationService : IReportGenerationService
    {
        public ReportGenerationOptions? LastCapturedOptions { get; private set; }
        public GeneratedReportInfo? LastSavedInfo { get; private set; }

        public Task<string> GenerateHtmlReportAsync(ReportGenerationOptions options)
        {
            LastCapturedOptions = options;
            return Task.FromResult(Path.Combine(Path.GetTempPath(),
                "cattech-fake-report-" + Guid.NewGuid().ToString("N") + ".html"));
        }

        public Task SaveReportInfoAsync(GeneratedReportInfo info)
        {
            LastSavedInfo = info;
            return Task.CompletedTask;
        }

        public Task<List<GeneratedReportInfo>> ListGeneratedReportsAsync(
            int maxResults = 20) => Task.FromResult(new List<GeneratedReportInfo>());

        public Task OpenReportAsync(string htmlPath) => Task.CompletedTask;

        public Task OpenReportsFolderAsync() => Task.CompletedTask;
    }

    private sealed class FakePdfExportService : IPdfExportService
    {
        public Task<PdfExporterInfo> CanExportAsync() =>
            Task.FromResult(new PdfExporterInfo { IsAvailable = true });
        public Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath) =>
            Task.FromResult(false);
        public string GetPdfOutputPath(string htmlPath) =>
            htmlPath.Replace(".html", ".pdf", StringComparison.Ordinal);
        public Task OpenPdfAsync(string pdfPath) => Task.CompletedTask;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(
            new AppSettings
            {
                Company = new CompanyInfo { Name = "CATTECH QA" },
                Technician = new TechnicianInfo { Name = "Técnico QA" }
            });
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public AppSettings CurrentSettings => new()
        {
            Company = new CompanyInfo { Name = "CATTECH QA" }
        };
        public event EventHandler<AppSettings>? SettingsChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class FakeServiceReportService : IServiceReportService,
        IServiceReportInventoryReader, IServiceReportInventoryDetailReader
    {
        public ServiceReportInventory Inventory { get; set; } =
            new(1, 1, 0, 0, false);
        public IReadOnlyList<string> DamagedFiles { get; set; } =
            Array.Empty<string>();
        public int ReportCount { get; set; } = 1;

        public Task<ServiceReportInventory> InspectReportInventoryAsync() =>
            Task.FromResult(Inventory);

        public Task<ServiceReportInventoryDetail> InspectReportInventoryDetailedAsync() =>
            Task.FromResult(new ServiceReportInventoryDetail(Inventory, DamagedFiles));

        public Task<List<ServiceReportSummary>> ListReportsAsync(int maxResults = 50) =>
            Task.FromResult(Enumerable.Range(1, Math.Min(ReportCount, maxResults))
                .Select(index => new ServiceReportSummary
                {
                    Id = index.ToString(),
                    ClientName = "Cliente QA",
                    CreatedAt = FixedDate
                }).ToList());

        public Task<ServiceReport?> LoadReportAsync(string reportId) =>
            Task.FromResult<ServiceReport?>(new ServiceReport
            {
                Id = reportId,
                CreatedAt = FixedDate,
                Client = new ClientInfo { Name = "Cliente QA" },
                Equipment = new EquipmentInfo
                {
                    Brand = "CATTECH",
                    Model = "Notebook QA"
                },
                Service = new ServiceInfo
                {
                    ServiceDate = FixedDate,
                    Reason = "QA sintético"
                }
            });

        public Task<string> SaveReportAsync(ServiceReport report) =>
            Task.FromResult(report.Id);
        public Task<bool> DeleteReportAsync(string reportId) =>
            Task.FromResult(false);
    }

    private sealed class FakeDiagnosticService : IDiagnosticService
    {
        public Task<List<DiagnosticSummary>> ListDiagnosticsAsync(int maxResults = 20) =>
            Task.FromResult(new List<DiagnosticSummary>
            {
                new() { Id = "1", OsName = "Microsoft Windows 11 Pro", DiagnosisDate = FixedDate }
            });

        public Task<DiagnosticReport?> LoadDiagnosticAsync(string diagnosticId) =>
            Task.FromResult<DiagnosticReport?>(new DiagnosticReport
            {
                Id = diagnosticId,
                OsName = "Microsoft Windows 11 Pro",
                DiagnosisDate = FixedDate
            });

        public Task<DiagnosticReport> RunQuickDiagnosticAsync(
            IProgress<int>? progress = null) =>
            Task.FromResult(new DiagnosticReport { OsName = "Microsoft Windows 11 Pro" });
        public Task<string> SaveDiagnosticAsync(DiagnosticReport report) =>
            Task.FromResult(report.Id);
        public Task<bool> DeleteDiagnosticAsync(string diagnosticId) =>
            Task.FromResult(false);
    }

    private sealed class FakeStartupService : IStartupService
    {
        public Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20) =>
            Task.FromResult(new List<StartupAnalysisSummary>());
        public Task<StartupAnalysis?> LoadAnalysisAsync(string analysisId) =>
            Task.FromResult<StartupAnalysis?>(null);
        public Task<StartupAnalysis> AnalyzeStartupAsync() =>
            Task.FromResult(new StartupAnalysis());
        public Task<string> SaveAnalysisAsync(StartupAnalysis analysis) =>
            Task.FromResult("1");
        public Task<bool> DeleteAnalysisAsync(string analysisId) =>
            Task.FromResult(false);
        public bool CanDisableStartupEntry(StartupEntry entry) => false;
        public Task<StartupDisableSummary> DisableSelectedAsync(
            IEnumerable<StartupEntry> entries, string reason = "") =>
            Task.FromResult(new StartupDisableSummary());
        public Task<StartupActionResult> RestoreAsync(StartupBackupRecord backup) =>
            Task.FromResult(new StartupActionResult());
        public Task<List<StartupBackupRecord>> ListBackupsAsync() =>
            Task.FromResult(new List<StartupBackupRecord>());
        public Task<StartupBackupRecord?> LoadBackupAsync(string backupId) =>
            Task.FromResult<StartupBackupRecord?>(null);
    }

    private sealed class FakeCleanupService : ITempCleanupService
    {
        public List<TempCleanupResultSummary> Summaries { get; } = new();

        public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null) =>
            Task.FromResult(new List<TempCleanupTarget>());
        public Task<TempCleanupResult> CleanupAsync(
            IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null) =>
            Task.FromResult(new TempCleanupResult());
        public Task<string> SaveResultAsync(TempCleanupResult result) =>
            Task.FromResult("1");
        public Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20) =>
            Task.FromResult(Summaries.Take(maxResults).ToList());
    }

    private sealed class FakeRestorePointService : IRestorePointService
    {
        public List<RestorePointResultSummary> Summaries { get; } = new();

        public Task<RestorePointStatus> CheckStatusAsync() =>
            Task.FromResult(new RestorePointStatus());
        public Task<RestorePointResult> CreateRestorePointAsync(string name) =>
            Task.FromResult(new RestorePointResult());
        public string GenerateRestorePointName() => "Punto QA";
        public Task<string> SaveResultAsync(RestorePointResult result) =>
            Task.FromResult("1");
        public Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20) =>
            Task.FromResult(Summaries.Take(maxResults).ToList());
    }

    private sealed class FakeVisualOptimizationService : IVisualOptimizationService
    {
        public List<VisualOptimizationResult> Results { get; } = new();
        public Func<List<VisualOptimizationSetting>> SettingsFactory { get; set; } = () => [];
        public VisualOptimizationResult ApplyResult { get; set; } = new();
        public int SaveCalls { get; private set; }
        public VisualOptimizationResult? SavedResult { get; private set; }

        public Task<List<VisualOptimizationSetting>> AnalyzeAsync() =>
            Task.FromResult(SettingsFactory());
        public Task<VisualOptimizationResult> ApplyAsync(
            IEnumerable<VisualOptimizationSetting> settings, string reason = "") =>
            Task.FromResult(ApplyResult);
        public Task<List<VisualOptimizationBackup>> ListBackupsAsync() =>
            Task.FromResult(new List<VisualOptimizationBackup>());
        public Task<bool> RestoreAsync(VisualOptimizationBackup backup) =>
            Task.FromResult(true);
        public Task<string> SaveResultAsync(VisualOptimizationResult result)
        {
            SaveCalls++;
            SavedResult = result;
            return Task.FromResult("saved");
        }
        public Task<IReadOnlyList<VisualOptimizationResult>> ListResultsAsync(
            int maxResults = 20) =>
            Task.FromResult<IReadOnlyList<VisualOptimizationResult>>(
                Results.Take(maxResults).ToList());
    }

    private sealed class FakeSmartDiskService : ISmartDiskService
    {
        public Task<SmartAnalysisResult> AnalyzeAllDisksAsync() =>
            Task.FromResult(new SmartAnalysisResult());
        public Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device) =>
            Task.FromResult(new SmartDiskReport());
        public Task<string> SaveResultAsync(SmartAnalysisResult result) =>
            Task.FromResult("1");
        public Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(
            int maxResults = 20) =>
            Task.FromResult<IReadOnlyList<SmartAnalysisResult>>(
                Array.Empty<SmartAnalysisResult>());
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
            Task.FromResult("1");
        public Task<IReadOnlyList<SmartTestSession>> ListSessionsAsync(
            int maxResults = 20) =>
            Task.FromResult<IReadOnlyList<SmartTestSession>>(
                Array.Empty<SmartTestSession>());
    }

    private sealed class FakeSourceReviewCoordinator : IReportSourceReviewCoordinator
    {
        public SourceReviewCoordinationStatus PrepareStatus { get; set; } =
            SourceReviewCoordinationStatus.CaseUnavailable;

        public Task<SourceReviewCoordinationResult> PrepareAsync(
            ServiceReport? selectedCase, string sourceKind, string sourceRecordId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SourceReviewCoordinationResult(PrepareStatus));

        public Task<SourceReviewCoordinationResult> ConfirmAsync(
            SourceReviewPreview? preview, string reviewerAlias, bool explicitConsent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SourceReviewCoordinationResult(
                SourceReviewCoordinationStatus.ReviewRequired));
    }
}
