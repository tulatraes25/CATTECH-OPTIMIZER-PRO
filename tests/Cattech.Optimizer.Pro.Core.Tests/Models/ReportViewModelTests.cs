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

    // Private Windows paths and customer identifiers may arrive in exception
    // messages from IO and headless PDF renderers. None belongs on the UI.
    private const string PrivateFailureMarker =
        @"C:\Users\SyntheticClient\Documents\PRIVATE_CUSTOMER_PATH_SENTINEL";

    [Fact]
    public async Task HtmlGenerationFailure_NeverShowsPrivatePathInUi()
    {
        var (vm, generator) = CreateViewModel();
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;
        generator.GenerateFailure = new IOException(PrivateFailureMarker);

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedReport);
        Assert.Equal(1, generator.GenerateCallCount);
        Assert.DoesNotContain("PRIVATE_CUSTOMER_PATH_SENTINEL", vm.ErrorMessage);
        Assert.DoesNotContain(@"C:\Users", vm.ErrorMessage);
        Assert.Contains("informe", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedReportIndexSave_NeverShowsPrivatePathOrClaimsCompletion()
    {
        var (vm, generator) = CreateViewModel();
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;
        generator.SaveInfoFailure = new IOException(PrivateFailureMarker);

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedReport);
        Assert.Equal(1, generator.GenerateCallCount);
        Assert.DoesNotContain("PRIVATE_CUSTOMER_PATH_SENTINEL", vm.ErrorMessage);
        Assert.Contains("historial", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PdfHtmlPreparationFailure_NeverShowsPrivatePathInUi()
    {
        var (vm, generator) = CreateViewModel();
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;
        generator.GenerateFailure = new IOException(PrivateFailureMarker);

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedPdf);
        Assert.DoesNotContain("PRIVATE_CUSTOMER_PATH_SENTINEL", vm.ErrorMessage);
        Assert.Contains("HTML", vm.ErrorMessage);
    }

    [Fact]
    public async Task PdfRendererFailure_NeverShowsPrivatePathInUi()
    {
        var exporter = new FakePdfExportService
        {
            ExportFailure = new IOException(PrivateFailureMarker)
        };
        var (vm, generator) = CreateViewModel(pdfExporter: exporter);
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedPdf);
        Assert.Equal(1, generator.GenerateCallCount);
        Assert.DoesNotContain("PRIVATE_CUSTOMER_PATH_SENTINEL", vm.ErrorMessage);
        Assert.Contains("PDF", vm.ErrorMessage);
    }

    [Fact]
    public async Task FailedDataLoad_RevokesStaleClientSelectionAndHidesPrivatePath()
    {
        var settings = new FakeSettingsService
        {
            LoadFailure = new IOException(PrivateFailureMarker)
        };
        var (vm, _) = CreateViewModel(settings: settings);
        vm.SelectedServiceReport = new ServiceReport();
        vm.HasClientData = true;
        vm.IncludeClient = true;
        vm.LastReportPath = "previous-synthetic.html";
        vm.HasGeneratedReport = true;

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.False(vm.HasClientData);
        Assert.False(vm.IncludeClient);
        Assert.Null(vm.SelectedServiceReport);
        Assert.False(vm.HasGeneratedReport);
        Assert.Equal(string.Empty, vm.LastReportPath);
        Assert.DoesNotContain("PRIVATE_CUSTOMER_PATH_SENTINEL", vm.ErrorMessage);
        Assert.DoesNotContain("PRIVATE_CUSTOMER_PATH_SENTINEL", vm.ReportInventoryMessage);
    }

    private static (TaskCompletionSource<bool> entered, TaskCompletionSource<bool> release)
        PauseNextCustomerReload(FakeServiceReportService store)
    {
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        store.LoadEntered = entered;
        store.ResumeLoad = release;
        return (entered, release);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerOptOutDuringRevalidation_NeverGeneratesPrivateHtmlOrPdf(bool pdf)
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.IncludeClient);
        var (entered, release) = PauseNextCustomerReload(store);

        var export = pdf
            ? vm.ExportPdfCommand.ExecuteAsync(null)
            : vm.GenerateReportCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.IncludeClient = false;
        release.TrySetResult(true);
        await export.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedReport);
        Assert.False(vm.HasGeneratedPdf);
        Assert.Contains("selección", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerSelectionAbaDuringRevalidation_IsNotAccidentallyAccepted()
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        var original = Assert.IsType<ServiceReport>(vm.SelectedServiceReport);
        var (entered, release) = PauseNextCustomerReload(store);

        var export = vm.GenerateReportCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        vm.SelectedServiceReport = new ServiceReport
        {
            Client = new ClientInfo { Name = "Other synthetic customer" }
        };
        vm.SelectedServiceReport = original;
        release.TrySetResult(true);
        await export.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedReport);
    }

    [Fact]
    public async Task SelectedCustomerMutationDuringRevalidation_IsRejected()
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        var original = Assert.IsType<ServiceReport>(vm.SelectedServiceReport);
        var (entered, release) = PauseNextCustomerReload(store);

        var export = vm.GenerateReportCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        original.Client.Phone = "synthetic-mutated-while-waiting";
        release.TrySetResult(true);
        await export.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.True(vm.HasError);
        Assert.False(vm.HasGeneratedReport);
    }

    [Fact]
    public async Task UnchangedCustomerRevalidation_ExportsDetachedSnapshot()
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        var original = Assert.IsType<ServiceReport>(vm.SelectedServiceReport);
        var (entered, release) = PauseNextCustomerReload(store);

        var export = vm.GenerateReportCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        release.TrySetResult(true);
        await export.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(vm.HasError);
        Assert.True(vm.HasGeneratedReport);
        var copied = Assert.IsType<ServiceReport>(generator.LastCapturedOptions!.ServiceReport);
        Assert.NotSame(original, copied);
        Assert.Equal(original.Id, copied.Id);
        original.Client.Phone = "synthetic-later-form-edit";
        Assert.NotEqual(original.Client.Phone, copied.Client.Phone);

        if (!string.IsNullOrWhiteSpace(vm.LastReportPath)
            && File.Exists(vm.LastReportPath))
            File.Delete(vm.LastReportPath); // Unique synthetic test file only.
    }

    [Fact]
    public async Task PdfMetadataUsesOriginalHtml_WhenLastReportUiChangesDuringExport()
    {
        var exporter = new FakePdfExportService();
        var (vm, generator) = CreateViewModel(pdfExporter: exporter);
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;

        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        exporter.ExportEntered = entered;
        exporter.ResumeExport = release;

        var operation = vm.ExportPdfCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var originalHtml = Assert.IsType<string>(exporter.LastExportedHtmlPath);
            vm.LastReportPath = Path.Combine(Path.GetTempPath(),
                "another-synthetic-report-" + Guid.NewGuid().ToString("N") + ".html");
            release.TrySetResult(true);
            await operation.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(vm.HasGeneratedPdf);
            Assert.False(vm.HasError);
            Assert.NotNull(generator.LastSavedInfo);
            Assert.Equal(originalHtml, generator.LastSavedInfo.HtmlPath);
            Assert.NotEqual(vm.LastReportPath, generator.LastSavedInfo.HtmlPath);
            Assert.Equal(Path.ChangeExtension(originalHtml, ".pdf"),
                generator.LastSavedInfo.PdfPath);
        }
        finally
        {
            release.TrySetResult(true);
            if (!string.IsNullOrEmpty(exporter.LastExportedHtmlPath)
                && File.Exists(exporter.LastExportedHtmlPath))
                File.Delete(exporter.LastExportedHtmlPath);
            if (!string.IsNullOrEmpty(vm.LastPdfPath) && File.Exists(vm.LastPdfPath))
                File.Delete(vm.LastPdfPath);
        }
    }

    [Fact]
    public async Task FailedHtmlRegeneration_RevokesPreviouslyGeneratedHtmlAndPdfButtons()
    {
        var exporter = new FakePdfExportService();
        var (vm, generator) = CreateViewModel(pdfExporter: exporter);
        await vm.LoadDataCommand.ExecuteAsync(null);
        await vm.ExportPdfCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedPdf);
        Assert.True(vm.HasGeneratedReport);
        var previousHtml = vm.LastReportPath;
        var previousPdf = vm.LastPdfPath;

        try
        {
            generator.GenerateFailure = new IOException("Synthetic retry failure");
            await vm.GenerateReportCommand.ExecuteAsync(null);

            Assert.True(vm.HasError);
            Assert.False(vm.HasGeneratedReport);
            Assert.False(vm.HasGeneratedPdf);
            Assert.Empty(vm.LastReportPath);
            Assert.Empty(vm.LastPdfPath);
            await vm.OpenReportCommand.ExecuteAsync(null);
            await vm.OpenPdfCommand.ExecuteAsync(null);
            Assert.Equal(0, generator.OpenReportCalls);
            Assert.Equal(0, exporter.OpenPdfCalls);
        }
        finally
        {
            if (File.Exists(previousHtml)) File.Delete(previousHtml);
            if (File.Exists(previousPdf)) File.Delete(previousPdf);
        }
    }

    [Fact]
    public async Task PdfRetryFailure_RevokesPreviouslyGeneratedPdf()
    {
        var exporter = new FakePdfExportService();
        var (vm, _) = CreateViewModel(pdfExporter: exporter);
        await vm.LoadDataCommand.ExecuteAsync(null);
        await vm.ExportPdfCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedPdf);
        var previousHtml = vm.LastReportPath;
        var previousPdf = vm.LastPdfPath;

        try
        {
            exporter.ExportFailure = new IOException("Synthetic PDF retry failure");
            await vm.ExportPdfCommand.ExecuteAsync(null);

            Assert.True(vm.HasError);
            Assert.False(vm.HasGeneratedPdf);
            Assert.Empty(vm.LastPdfPath);
            await vm.OpenPdfCommand.ExecuteAsync(null);
            Assert.Equal(0, exporter.OpenPdfCalls);
        }
        finally
        {
            if (File.Exists(previousHtml)) File.Delete(previousHtml);
            if (File.Exists(previousPdf)) File.Delete(previousPdf);
            if (File.Exists(vm.LastReportPath)) File.Delete(vm.LastReportPath);
        }
    }

    [Fact]
    public async Task ReportSelectionsAndNotes_RevokeLatestGeneratedArtifacts()
    {
        var (vm, _) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        await vm.GenerateReportCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedReport);
        var previousHtml = vm.LastReportPath;

        try
        {
            vm.FinalObservations = "New synthetic observation";
            Assert.False(vm.HasGeneratedReport);
            Assert.Empty(vm.LastReportPath);

            await vm.GenerateReportCommand.ExecuteAsync(null);
            Assert.True(vm.HasGeneratedReport);
            var laterHtml = vm.LastReportPath;
            try
            {
                vm.IncludeClient = false;
                Assert.False(vm.HasGeneratedReport);
                Assert.False(vm.HasGeneratedPdf);
                Assert.Empty(vm.LastReportPath);
            }
            finally
            {
                if (File.Exists(laterHtml)) File.Delete(laterHtml);
            }
        }
        finally
        {
            if (File.Exists(previousHtml)) File.Delete(previousHtml);
        }
    }

    [Fact]
    public async Task PdfHistoryUsesSameIncludedSectionsAndNotesAsHtmlHistory()
    {
        var (vm, generator) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.IncludeRecommendations = false;
        vm.FinalObservations = "Synthetic controlled observation";
        await vm.GenerateReportCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedReport);
        var html = Assert.IsType<GeneratedReportInfo>(generator.LastSavedInfo);
        var previousPath = vm.LastReportPath;
        try
        {
            await vm.ExportPdfCommand.ExecuteAsync(null);

            Assert.True(vm.HasGeneratedPdf);
            var pdf = Assert.IsType<GeneratedReportInfo>(generator.LastSavedInfo);
            Assert.Equal(html.IncludedSections, pdf.IncludedSections);
            Assert.Contains("Cliente", pdf.IncludedSections);
            Assert.DoesNotContain("Recomendaciones", pdf.IncludedSections);
            Assert.Equal("Synthetic controlled observation", pdf.Notes);
            Assert.Equal(html.Notes, pdf.Notes);
            Assert.NotEmpty(pdf.HtmlPath);
            Assert.NotEmpty(pdf.PdfPath);
        }
        finally
        {
            if (File.Exists(previousPath)) File.Delete(previousPath);
            if (File.Exists(vm.LastReportPath)) File.Delete(vm.LastReportPath);
            if (File.Exists(vm.LastPdfPath)) File.Delete(vm.LastPdfPath);
        }
    }

    [Fact]
    public async Task SmartTestCheckboxEdit_RevokesPreviousHtmlWithoutDeletingHistory()
    {
        var (vm, _) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        var selection = new SmartTestSessionSelectionItem
        {
            Session = new SmartTestSession(),
            IsSelected = false
        };
        vm.AvailableSmartTestSessions.Add(selection);
        await vm.GenerateReportCommand.ExecuteAsync(null);
        Assert.True(vm.HasGeneratedReport);
        var previousHtml = vm.LastReportPath;

        try
        {
            selection.IsSelected = true;

            Assert.False(vm.HasGeneratedReport);
            Assert.False(vm.HasGeneratedPdf);
            Assert.Empty(vm.LastReportPath);
            Assert.Empty(vm.LastPdfPath);
            // UI revocation does NOT delete historic user files.
            Assert.True(File.Exists(previousHtml));
        }
        finally
        {
            if (File.Exists(previousHtml)) File.Delete(previousHtml);
        }
    }

    [Fact]
    public async Task SmartTestCollectionChangesDetachOldListenersAndRevokeOutputs()
    {
        var (vm, _) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        var selection = new SmartTestSessionSelectionItem
        {
            Session = new SmartTestSession(),
            IsSelected = false
        };
        vm.AvailableSmartTestSessions.Add(selection);
        vm.AvailableSmartTestSessions.Remove(selection);

        await vm.GenerateReportCommand.ExecuteAsync(null);
        var previousHtml = vm.LastReportPath;
        try
        {
            // The removed child is no longer subscribed to the active report.
            selection.IsSelected = true;
            Assert.True(vm.HasGeneratedReport);
            Assert.Equal(previousHtml, vm.LastReportPath);

            vm.AvailableSmartTestSessions.Add(selection);
            Assert.False(vm.HasGeneratedReport);
            Assert.Empty(vm.LastReportPath);

            await vm.GenerateReportCommand.ExecuteAsync(null);
            var nextHtml = vm.LastReportPath;
            try
            {
                vm.AvailableSmartTestSessions.Clear();
                Assert.False(vm.HasGeneratedReport);
                Assert.Empty(vm.LastReportPath);
            }
            finally
            {
                if (File.Exists(nextHtml)) File.Delete(nextHtml);
            }
        }
        finally
        {
            if (File.Exists(previousHtml)) File.Delete(previousHtml);
        }
    }

    [Fact]
    public async Task FormEditedWhileHtmlGeneratorAwaits_CannotClaimCurrentReport()
    {
        var (vm, generator) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        generator.GenerateEntered = entered;
        generator.ResumeGenerate = release;

        var operation = vm.GenerateReportCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.FinalObservations = "Changed synthetic observations while writing";
            release.TrySetResult(true);
            await operation.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(vm.HasError);
            Assert.False(vm.IsSuccess);
            Assert.False(vm.HasGeneratedReport);
            Assert.False(vm.HasGeneratedPdf);
            Assert.Empty(vm.LastReportPath);
            Assert.Empty(vm.LastPdfPath);
            Assert.Contains("cambiaron", vm.ErrorMessage,
                StringComparison.OrdinalIgnoreCase);
            // The earlier file may exist; do not claim it was rolled back.
            Assert.True(File.Exists(generator.LastCreatedFilePath));
            Assert.Null(generator.LastSavedInfo);
        }
        finally
        {
            release.TrySetResult(true);
            if (File.Exists(generator.LastCreatedFilePath))
                File.Delete(generator.LastCreatedFilePath);
        }
    }

    [Fact]
    public async Task FormEditedDuringHeadlessPdfConversion_NeverShowsStaleSuccess()
    {
        var exporter = new FakePdfExportService();
        var (vm, generator) = CreateViewModel(pdfExporter: exporter);
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        exporter.ExportEntered = entered;
        exporter.ResumeExport = release;

        var operation = vm.ExportPdfCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            vm.FinalObservations = "Different synthetic PDF notes";
            release.TrySetResult(true);
            await operation.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(vm.HasError);
            Assert.False(vm.IsSuccess);
            Assert.False(vm.HasGeneratedReport);
            Assert.False(vm.HasGeneratedPdf);
            Assert.Empty(vm.LastReportPath);
            Assert.Empty(vm.LastPdfPath);
            Assert.Contains("cambiaron", vm.ErrorMessage,
                StringComparison.OrdinalIgnoreCase);
            // The file is recorded truthfully under the original options,
            // but it cannot be presented as the current form's output.
            Assert.NotNull(generator.LastSavedInfo);
            Assert.Equal(string.Empty, generator.LastSavedInfo.Notes);
        }
        finally
        {
            release.TrySetResult(true);
            if (File.Exists(exporter.LastExportedHtmlPath))
                File.Delete(exporter.LastExportedHtmlPath);
            if (!string.IsNullOrEmpty(exporter.LastExportedHtmlPath))
            {
                var pdf = exporter.GetPdfOutputPath(exporter.LastExportedHtmlPath);
                if (File.Exists(pdf)) File.Delete(pdf);
            }
        }
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

    [Fact]
    public async Task ReportViewModel_InvalidStore_FlagsWarningAndSuppressesCustomerSelection()
    {
        var store = new FakeServiceReportService
        {
            Inventory = new ServiceReportInventory(2, 1, 0, 1, false)
        };
        var (vm, _) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasReportInventoryWarning);
        Assert.Contains("1 archivos inválidos", vm.ReportInventoryMessage);
        Assert.Empty(vm.AvailableServiceReports);
        Assert.Null(vm.SelectedServiceReport);
        Assert.False(vm.HasClientData);
        Assert.False(vm.IncludeClient);
        Assert.True(vm.HasDiagnosticData);
    }

    [Fact]
    public async Task ReportViewModel_DuplicateStore_DoesNotDisplayAmbiguousCustomers()
    {
        var store = new FakeServiceReportService
        {
            Inventory = new ServiceReportInventory(2, 0, 2, 0, false)
        };
        var (vm, _) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.Contains("2 registros", vm.ReportInventoryMessage);
        Assert.Empty(vm.AvailableServiceReports);
    }

    [Fact]
    public async Task ReportViewModel_ReloadAfterCorruption_RevokesPreviouslyLoadedDataAndArtifacts()
    {
        var store = new FakeServiceReportService();
        var (vm, _) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.HasClientData);
        vm.LastReportPath = "previous.html";
        vm.HasGeneratedReport = true;

        store.Inventory = new ServiceReportInventory(1, 0, 0, 1, false);
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasReportInventoryWarning);
        Assert.Null(vm.SelectedServiceReport);
        Assert.False(vm.HasClientData);
        Assert.False(vm.IncludeClient);
        Assert.False(vm.HasGeneratedReport);
        Assert.Equal(string.Empty, vm.LastReportPath);
    }

    [Fact]
    public async Task ReportViewModel_StoreChangedDuringLoad_SuppressesPartialSelection()
    {
        var store = new FakeServiceReportService { ReturnNullOnLoad = true };
        var (vm, _) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasReportInventoryWarning);
        Assert.Contains("cambió", vm.ReportInventoryMessage);
        Assert.Empty(vm.AvailableServiceReports);
        Assert.Null(vm.SelectedServiceReport);
    }

    [Fact]
    public async Task ReportViewModel_AmbiguityAfterLoad_BlocksHtmlOutputAndRevokesCustomer()
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.IncludeClient);

        store.Inventory = new ServiceReportInventory(2, 0, 2, 0, false);
        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.True(vm.HasError);
        Assert.Null(vm.SelectedServiceReport);
        Assert.False(vm.IncludeClient);
        Assert.False(vm.HasGeneratedReport);
    }

    [Fact]
    public async Task ReportViewModel_ReportDisappearsAfterLoad_BlocksPdfOutput()
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        store.ReturnNullOnLoad = true;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.False(vm.HasGeneratedPdf);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.Null(vm.SelectedServiceReport);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task ReportViewModel_ClientIdentityReplacedAfterLoad_BlocksHtmlOutput()
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        store.ClientNameOverride = "Different Synthetic Customer";

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.False(vm.HasClientData);
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task ReportViewModel_ExcludedClient_DoesNotReachHtmlOptionsOrHistory()
    {
        var (vm, generator) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.NotNull(vm.SelectedServiceReport);
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.NotNull(generator.LastCapturedOptions);
        Assert.False(generator.LastCapturedOptions.IncludeClient);
        Assert.Null(generator.LastCapturedOptions.ServiceReport);
        Assert.NotNull(generator.LastSavedInfo);
        Assert.Equal("Sin cliente", generator.LastSavedInfo.ClientName);
        Assert.Equal(string.Empty, generator.LastSavedInfo.EquipmentName);
    }

    [Fact]
    public async Task ReportViewModel_ExcludedClient_DoesNotReachPdfHistory()
    {
        var (vm, generator) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.NotNull(generator.LastCapturedOptions);
        Assert.Null(generator.LastCapturedOptions.ServiceReport);
        Assert.NotNull(generator.LastSavedInfo);
        Assert.Equal("Sin cliente", generator.LastSavedInfo.ClientName);
        Assert.Equal(string.Empty, generator.LastSavedInfo.EquipmentName);
    }

    [Theory]
    [InlineData("phone")]
    [InlineData("service-reason")]
    [InlineData("service-action")]
    [InlineData("technician")]
    public async Task ReportViewModel_ChangedNonIdentityField_BlocksStaleHtml(string changedField)
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.IncludeClient);

        switch (changedField)
        {
            case "phone": store.ClientPhoneOverride = "synthetic-new-phone"; break;
            case "service-reason": store.ServiceReasonOverride = "Synthetic revised repair"; break;
            case "service-action": store.ServiceActionOverride = "Synthetic replaced action"; break;
            case "technician": store.TechnicianOverride = "Other Synthetic Technician"; break;
        }

        await vm.GenerateReportCommand.ExecuteAsync(null);

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.Null(vm.SelectedServiceReport);
        Assert.False(vm.HasClientData);
        Assert.False(vm.HasGeneratedReport);
    }

    [Theory]
    [InlineData("phone")]
    [InlineData("service-reason")]
    public async Task ReportViewModel_ChangedNonIdentityField_BlocksStalePdf(string changedField)
    {
        var store = new FakeServiceReportService();
        var (vm, generator) = CreateViewModel(store);
        await vm.LoadDataCommand.ExecuteAsync(null);

        if (changedField == "phone")
            store.ClientPhoneOverride = "synthetic-updated-contact";
        else
            store.ServiceReasonOverride = "Synthetic updated service reason";

        await vm.ExportPdfCommand.ExecuteAsync(null);

        Assert.Equal(0, generator.GenerateCallCount);
        Assert.False(vm.HasGeneratedPdf);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.Null(vm.SelectedServiceReport);
    }

    [Fact]
    public async Task ReportInventory_WhenMoreThanTenReports_ExplainsVisibleLimitWithoutDataLoss()
    {
        var store = new FakeServiceReportService
        {
            SyntheticReportCount = 14,
            Inventory = new ServiceReportInventory(14, 14, 0, 0, false)
        };
        var (vm, _) = CreateViewModel(store);

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.False(vm.HasReportInventoryWarning);
        Assert.True(vm.HasClientData);
        Assert.Equal(10, vm.AvailableServiceReports.Count);
        Assert.Contains("14 informes", vm.ReportInventoryMessage);
        Assert.Contains("máximo los 10", vm.ReportInventoryMessage);
        Assert.Contains("siguen guardados", vm.ReportInventoryMessage);
        Assert.NotNull(vm.SelectedServiceReport);
    }

    [Fact]
    public async Task ReportInventory_WhenWithinLimit_DoesNotClaimHiddenReports()
    {
        var store = new FakeServiceReportService
        {
            SyntheticReportCount = 3,
            Inventory = new ServiceReportInventory(3, 3, 0, 0, false)
        };
        var (vm, _) = CreateViewModel(store);

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.False(vm.HasReportInventoryWarning);
        Assert.Equal(3, vm.AvailableServiceReports.Count);
        Assert.Contains("3 informes", vm.ReportInventoryMessage);
        Assert.DoesNotContain("máximo los 10", vm.ReportInventoryMessage);
    }

    [Fact]
    public async Task SummaryOnlyHistory_DoesNotOfferPhantomCleanupOrRestoreSections()
    {
        var (vm, writer) = CreateViewModel(
            cleanup: new FakeCleanupService { SummaryCount = 2 },
            restore: new FakeRestorePointService { SummaryCount = 1 });
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.Empty(vm.AvailableCleanupResults);
        Assert.Empty(vm.AvailableRestorePointResults);
        Assert.Null(vm.SelectedCleanupResult);
        Assert.Null(vm.SelectedRestorePointResult);
        Assert.False(vm.HasCleanupData);
        Assert.False(vm.HasRestorePointData);
        Assert.False(vm.IncludeCleanup);
        Assert.False(vm.IncludeRestorePoint);
        Assert.Contains("no exportables", vm.StatusText);

        await vm.GenerateReportCommand.ExecuteAsync(null);
        var options = Assert.IsType<ReportGenerationOptions>(writer.LastCapturedOptions);
        Assert.False(options.IncludeCleanup);
        Assert.False(options.IncludeRestorePoint);
        Assert.Null(options.CleanupResult);
        Assert.Null(options.RestorePointResult);
        Assert.NotNull(writer.LastSavedInfo);
        Assert.DoesNotContain("Limpieza", writer.LastSavedInfo.IncludedSections);
        Assert.DoesNotContain("Restauración", writer.LastSavedInfo.IncludedSections);
    }

    [Fact]
    public async Task ForgedFullHistoryObjectsAndCheckboxes_CannotReachExport()
    {
        var (vm, writer) = CreateViewModel(
            cleanup: new FakeCleanupService { SummaryCount = 1 },
            restore: new FakeRestorePointService { SummaryCount = 1 });
        await vm.LoadDataCommand.ExecuteAsync(null);
        vm.HasCleanupData = true;
        vm.HasRestorePointData = true;
        vm.SelectedCleanupResult = new TempCleanupResult();
        vm.SelectedRestorePointResult = new RestorePointResult();
        vm.IncludeCleanup = true;
        vm.IncludeRestorePoint = true;

        await vm.GenerateReportCommand.ExecuteAsync(null);
        var options = Assert.IsType<ReportGenerationOptions>(writer.LastCapturedOptions);
        Assert.False(options.IncludeCleanup);
        Assert.False(options.IncludeRestorePoint);
        Assert.Null(options.CleanupResult);
        Assert.Null(options.RestorePointResult);
        Assert.NotNull(writer.LastSavedInfo);
        Assert.DoesNotContain("Limpieza", writer.LastSavedInfo.IncludedSections);
        Assert.DoesNotContain("Restauración", writer.LastSavedInfo.IncludedSections);
    }

    [Fact]
    public async Task RefreshWithSummaryOnlyHistory_RevokesPriorSelections()
    {
        var (vm, _) = CreateViewModel(
            cleanup: new FakeCleanupService { SummaryCount = 1 },
            restore: new FakeRestorePointService { SummaryCount = 2 });
        vm.SelectedCleanupResult = new TempCleanupResult();
        vm.SelectedRestorePointResult = new RestorePointResult();
        vm.HasCleanupData = true;
        vm.HasRestorePointData = true;
        vm.IncludeCleanup = true;
        vm.IncludeRestorePoint = true;

        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedCleanupResult);
        Assert.Null(vm.SelectedRestorePointResult);
        Assert.False(vm.HasCleanupData);
        Assert.False(vm.HasRestorePointData);
        Assert.False(vm.IncludeCleanup);
        Assert.False(vm.IncludeRestorePoint);
        Assert.Empty(vm.AvailableCleanupResults);
        Assert.Empty(vm.AvailableRestorePointResults);
    }

    [Fact]
    public async Task ExcludedDiagnostic_RemovesItsRecordFromGeneratorOptions()
    {
        var (vm, generator) = CreateViewModel();
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.NotNull(vm.SelectedDiagnosticReport);

        vm.IncludeClient = false;
        vm.IncludeDiagnostic = false;
        await vm.GenerateReportCommand.ExecuteAsync(null);

        var options = Assert.IsType<ReportGenerationOptions>(generator.LastCapturedOptions);
        Assert.False(options.IncludeDiagnostic);
        Assert.Null(options.DiagnosticReport);
    }

    [Fact]
    public async Task FailedSettingsRefresh_RevokesPriorDiagnosticAndReportNotes()
    {
        var settings = new FakeSettingsService();
        var (vm, _) = CreateViewModel(settings: settings);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.HasClientData);
        Assert.True(vm.HasDiagnosticData);
        Assert.NotNull(vm.SelectedDiagnosticReport);

        vm.FinalObservations = "SYNTHETIC-OLD-CLIENT-PRIVATE";
        vm.IncludeStartup = true;
        vm.SelectedStartupAnalysis = new StartupAnalysis();
        settings.LoadFailure = new IOException(@"C:\SYNTHETIC-OLD-CLIENT\settings.json");

        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.Null(vm.SelectedServiceReport);
        Assert.Null(vm.SelectedDiagnosticReport);
        Assert.Null(vm.SelectedStartupAnalysis);
        Assert.False(vm.HasClientData);
        Assert.False(vm.HasDiagnosticData);
        Assert.False(vm.HasStartupData);
        Assert.False(vm.IncludeClient);
        Assert.False(vm.IncludeDiagnostic);
        Assert.False(vm.IncludeStartup);
        Assert.False(vm.IncludeCompany);
        Assert.False(vm.HasCompanyData);
        Assert.Empty(vm.AvailableServiceReports);
        Assert.Empty(vm.AvailableDiagnosticReports);
        Assert.Empty(vm.AvailableStartupAnalyses);
        Assert.Empty(vm.FinalObservations);
        Assert.DoesNotContain("SYNTHETIC-OLD-CLIENT", vm.ErrorMessage);
    }

    [Fact]
    public async Task LateSmartRefreshFailure_DoesNotRetainAnyPriorSelectionAndCanRecover()
    {
        var smart = new FakeSmartDiskService { ReturnOne = true };
        var (vm, _) = CreateViewModel(smart: smart);
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.True(vm.HasSmartData);
        Assert.True(vm.HasDiagnosticData);
        Assert.NotNull(vm.SelectedSmartAnalysis);
        Assert.NotNull(vm.SelectedDiagnosticReport);
        vm.FinalObservations = "SYNTHETIC-CASE-SECRET";

        smart.FailList = true;
        await vm.LoadDataCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.True(vm.HasReportInventoryWarning);
        Assert.Null(vm.SelectedServiceReport);
        Assert.Null(vm.SelectedDiagnosticReport);
        Assert.Null(vm.SelectedSmartAnalysis);
        Assert.Null(vm.SelectedStartupAnalysis);
        Assert.Null(vm.SelectedCleanupResult);
        Assert.Null(vm.SelectedRestorePointResult);
        Assert.False(vm.IncludeClient);
        Assert.False(vm.IncludeDiagnostic);
        Assert.False(vm.IncludeStartup);
        Assert.False(vm.IncludeSmart);
        Assert.False(vm.IncludeSmartTests);
        Assert.False(vm.IncludeCleanup);
        Assert.False(vm.IncludeRestorePoint);
        Assert.False(vm.HasCompanyData);
        Assert.False(vm.HasSmartData);
        Assert.False(vm.HasDiagnosticData);
        Assert.Empty(vm.AvailableSmartAnalyses);
        Assert.Empty(vm.AvailableSmartTestSessions);
        Assert.Empty(vm.AvailableDiagnosticReports);
        Assert.Empty(vm.FinalObservations);
        Assert.DoesNotContain("SYNTHETIC-CASE-SECRET", vm.ErrorMessage);

        smart.FailList = false;
        await vm.LoadDataCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.False(vm.HasReportInventoryWarning);
        Assert.True(vm.HasSmartData);
        Assert.True(vm.HasDiagnosticData);
        Assert.NotNull(vm.SelectedSmartAnalysis);
        Assert.NotNull(vm.SelectedDiagnosticReport);
        Assert.Empty(vm.FinalObservations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupersededSettingsCompletion_CannotOverwriteNewerLoad(bool throwOld)
    {
        var settings = new FakeSettingsService
        {
            FirstLoadGate = new TaskCompletionSource<AppSettings>(
                TaskCreationOptions.RunContinuationsAsynchronously),
            FirstLoadEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var (vm, _) = CreateViewModel(settings: settings);
        var oldLoad = InvokeDirectReloadForRaceTest(vm);
        await settings.FirstLoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var newLoad = InvokeDirectReloadForRaceTest(vm);
        await newLoad.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(vm.HasError);
        Assert.True(vm.HasClientData);
        Assert.True(vm.HasDiagnosticData);
        Assert.True(vm.HasCompanyData);
        Assert.False(vm.IsRunning);

        if (throwOld)
            settings.FirstLoadGate.SetException(new IOException(
                @"C:\SYNTHETIC-OLD-REPORT\private.json"));
        else
            settings.FirstLoadGate.SetResult(new AppSettings
            {
                Company = new CompanyInfo { Name = string.Empty }
            });
        await oldLoad.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(2, settings.LoadCallCount);
        Assert.False(vm.HasError);
        Assert.False(vm.HasReportInventoryWarning);
        Assert.True(vm.HasClientData);
        Assert.True(vm.HasDiagnosticData);
        Assert.True(vm.HasCompanyData);
        Assert.NotNull(vm.SelectedServiceReport);
        Assert.NotNull(vm.SelectedDiagnosticReport);
        Assert.False(vm.IsRunning);
        Assert.DoesNotContain("SYNTHETIC-OLD-REPORT", vm.ErrorMessage);
    }

    [Fact]
    public async Task SupersededInventoryResult_CannotReplaceHealthyNewerInventory()
    {
        var reports = new FakeServiceReportService
        {
            FirstInventoryGate = new TaskCompletionSource<ServiceReportInventory>(
                TaskCreationOptions.RunContinuationsAsynchronously),
            FirstInventoryEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var (vm, _) = CreateViewModel(storedReports: reports);
        var oldLoad = InvokeDirectReloadForRaceTest(vm);
        await reports.FirstInventoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await InvokeDirectReloadForRaceTest(vm).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(vm.HasClientData);
        Assert.False(vm.HasReportInventoryWarning);

        // Old inventory comes back "corrupt", but cannot replace the newer
        // verified inventory notice nor clear current client selection.
        reports.FirstInventoryGate.SetResult(
            new ServiceReportInventory(2, 0, 2, 0, false));
        await oldLoad.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(vm.HasReportInventoryWarning);
        Assert.True(vm.HasClientData);
        Assert.NotNull(vm.SelectedServiceReport);
        Assert.DoesNotContain("duplicado", vm.ReportInventoryMessage);
    }

    [Fact]
    public async Task SupersededLateSmartResult_CannotEraseNewerSelectedDiagnosis()
    {
        var smart = new FakeSmartDiskService
        {
            ReturnOne = true,
            FirstListGate = new TaskCompletionSource<IReadOnlyList<SmartAnalysisResult>>(
                TaskCreationOptions.RunContinuationsAsynchronously),
            FirstListEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var (vm, _) = CreateViewModel(smart: smart);
        var oldLoad = InvokeDirectReloadForRaceTest(vm);
        await smart.FirstListEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await InvokeDirectReloadForRaceTest(vm).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(vm.HasSmartData);
        Assert.NotNull(vm.SelectedSmartAnalysis);
        Assert.False(vm.HasReportInventoryWarning);
        Assert.True(vm.HasDiagnosticData);
        Assert.False(vm.IsRunning);

        smart.FirstListGate.SetResult(Array.Empty<SmartAnalysisResult>());
        await oldLoad.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.True(vm.HasSmartData);
        Assert.NotNull(vm.SelectedSmartAnalysis);
        Assert.True(vm.HasDiagnosticData);
        Assert.NotNull(vm.SelectedDiagnosticReport);
        Assert.True(vm.IncludeSmart);
        Assert.False(vm.IsRunning);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SupersededClientHistoryList_DoesNotClearNewerLoadedReport(bool failOld)
    {
        var store = new FakeServiceReportService
        {
            FirstListGate = new TaskCompletionSource<List<ServiceReportSummary>>(
                TaskCreationOptions.RunContinuationsAsynchronously),
            FirstListEntered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var (vm, _) = CreateViewModel(storedReports: store);
        var oldLoad = InvokeDirectReloadForRaceTest(vm);
        await store.FirstListEntered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await InvokeDirectReloadForRaceTest(vm).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.False(vm.HasReportInventoryWarning);
        Assert.True(vm.HasClientData);
        Assert.NotNull(vm.SelectedServiceReport);
        Assert.True(vm.HasDiagnosticData);

        if (failOld)
            store.FirstListGate.SetException(new IOException(
                @"C:\QA-OLDER-REPORT\history.json"));
        else
            store.FirstListGate.SetResult(new List<ServiceReportSummary>());

        await oldLoad.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.False(vm.HasReportInventoryWarning);
        Assert.False(vm.HasError);
        Assert.True(vm.HasClientData);
        Assert.NotNull(vm.SelectedServiceReport);
        Assert.Equal("Cliente QA", vm.SelectedServiceReport.Client.Name);
        Assert.True(vm.HasDiagnosticData);
        Assert.NotNull(vm.SelectedDiagnosticReport);
        Assert.False(vm.IsRunning);
        Assert.DoesNotContain("QA-OLDER-REPORT", vm.ReportInventoryMessage);
    }

    private static Task InvokeDirectReloadForRaceTest(ReportViewModel vm)
    {
        // Exercise out-of-order invocations even if the current WPF command
        // already serializes clicks: future programmatic callers must not
        // be able to reinstate stale case data.
        var method = typeof(ReportViewModel).GetMethod("LoadDataAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(vm, null));
    }

    // ===== Helper =====

    private static (ReportViewModel vm, FakeReportGenerationService fakeReportService) CreateViewModel(FakeServiceReportService? storedReports = null, FakePdfExportService? pdfExporter = null, FakeSettingsService? settings = null, FakeCleanupService? cleanup = null, FakeRestorePointService? restore = null, FakeSmartDiskService? smart = null)
    {
        var fakeReportService = new FakeReportGenerationService();
        var vm = new ReportViewModel(
            fakeReportService,
            pdfExporter ?? new FakePdfExportService(),
            settings ?? new FakeSettingsService(),
            storedReports ?? new FakeServiceReportService(),
            new FakeDiagnosticService(),
            new FakeStartupService(),
            cleanup ?? new FakeCleanupService(),
            new FakeVisualOptimizationService(),
            restore ?? new FakeRestorePointService(),
            smart ?? new FakeSmartDiskService(),
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
        public Exception? GenerateFailure { get; set; }
        public Exception? SaveInfoFailure { get; set; }
        public TaskCompletionSource<bool>? GenerateEntered { get; set; }
        public TaskCompletionSource<bool>? ResumeGenerate { get; set; }
        public string? LastCreatedFilePath { get; private set; }
        public int OpenReportCalls { get; private set; }

        public async Task<string> GenerateHtmlReportAsync(ReportGenerationOptions options)
        {
            LastCapturedOptions = options;
            GenerateCallCount++;
            if (GenerateFailure is not null)
                throw GenerateFailure;
            var gate = ResumeGenerate;
            if (GenerateEntered is not null)
            {
                GenerateEntered.TrySetResult(true);
                if (gate is not null)
                    await gate.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }

            var clientName = options.ServiceReport?.Client?.Name ?? "SinCliente";
            var fileName = $"Informe_Tecnico_CATTECH_{clientName}_{DateTime.Now:yyyyMMdd-HHmmss}_{Guid.NewGuid():N}.html";
            var filePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, $"<html><body>Test report for {clientName}</body></html>");
            LastCreatedFilePath = filePath;
            return filePath;
        }

        public GeneratedReportInfo? LastSavedInfo { get; private set; }
        public Task SaveReportInfoAsync(GeneratedReportInfo info)
        {
            if (SaveInfoFailure is not null)
                throw SaveInfoFailure;
            LastSavedInfo = info;
            return Task.CompletedTask;
        }
        public Task<List<GeneratedReportInfo>> ListGeneratedReportsAsync(int maxResults = 20) => Task.FromResult(new List<GeneratedReportInfo>());
        public Task OpenReportAsync(string htmlPath)
        {
            OpenReportCalls++;
            return Task.CompletedTask;
        }
        public Task OpenReportsFolderAsync() => Task.CompletedTask;
    }

    private class FakePdfExportService : IPdfExportService
    {
        public Exception? ExportFailure { get; set; }
        public int OpenPdfCalls { get; private set; }
        public TaskCompletionSource<bool>? ExportEntered { get; set; }
        public TaskCompletionSource<bool>? ResumeExport { get; set; }
        public string? LastExportedHtmlPath { get; private set; }
        public Task<PdfExporterInfo> CanExportAsync() => Task.FromResult(new PdfExporterInfo { IsAvailable = true });
        public async Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath)
        {
            if (ExportFailure is not null)
                throw ExportFailure;
            LastExportedHtmlPath = htmlPath;
            if (ExportEntered is not null)
            {
                ExportEntered.TrySetResult(true);
                if (ResumeExport is not null)
                    await ResumeExport.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            File.WriteAllText(outputPdfPath, "%PDF-1.4 fake");
            return true;
        }
        public string GetPdfOutputPath(string htmlPath) => htmlPath.Replace(".html", ".pdf");
        public Task OpenPdfAsync(string pdfPath)
        {
            OpenPdfCalls++;
            return Task.CompletedTask;
        }
    }

    private class FakeSettingsService : ISettingsService
    {
        public Exception? LoadFailure { get; set; }
        public TaskCompletionSource<AppSettings>? FirstLoadGate { get; set; }
        public TaskCompletionSource<bool>? FirstLoadEntered { get; set; }
        public int LoadCallCount { get; private set; }
        public Task<AppSettings> LoadSettingsAsync()
        {
            if (++LoadCallCount == 1 && FirstLoadGate is not null)
            {
                FirstLoadEntered?.TrySetResult(true);
                return FirstLoadGate.Task;
            }
            if (LoadFailure is not null)
                throw LoadFailure;
            return Task.FromResult(new AppSettings
            {
                Company = new CompanyInfo { Name = "CATTECH QA" },
                Technician = new TechnicianInfo { Name = "Técnico QA" }
            });
        }
        public Task SaveSettingsAsync(AppSettings settings) => Task.CompletedTask;
        public AppSettings CurrentSettings => new()
        {
            Company = new CompanyInfo { Name = "CATTECH QA" },
            Technician = new TechnicianInfo { Name = "Técnico QA" }
        };
        // This settings fake is immutable and never raises changes. Explicit
        // accessors avoid an unused-event warning without changing behavior.
        public event EventHandler<AppSettings>? SettingsChanged
        {
            add { }
            remove { }
        }
    }

    private class FakeServiceReportService : IServiceReportService, IServiceReportInventoryReader
    {
        public ServiceReportInventory Inventory { get; set; } =
            new(1, 1, 0, 0, false);
        public int SyntheticReportCount { get; set; } = 1;
        public bool ReturnNullOnLoad { get; set; }
        public string ClientNameOverride { get; set; } = "Cliente QA";
        public string ClientPhoneOverride { get; set; } = string.Empty;
        public string ServiceReasonOverride { get; set; } = "Smoke Report Test";
        public string? ServiceActionOverride { get; set; }
        public string TechnicianOverride { get; set; } = string.Empty;
        private static readonly DateTime StableDate =
            new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        public TaskCompletionSource<ServiceReportInventory>? FirstInventoryGate { get; set; }
        public TaskCompletionSource<bool>? FirstInventoryEntered { get; set; }
        private int _inspectionCount;
        public Task<ServiceReportInventory> InspectReportInventoryAsync()
        {
            if (Interlocked.Increment(ref _inspectionCount) == 1
                && FirstInventoryGate is not null)
            {
                FirstInventoryEntered?.TrySetResult(true);
                return FirstInventoryGate.Task;
            }
            return Task.FromResult(Inventory);
        }

        public TaskCompletionSource<List<ServiceReportSummary>>? FirstListGate { get; set; }
        public TaskCompletionSource<bool>? FirstListEntered { get; set; }
        private int _listCallCount;
        public Task<List<ServiceReportSummary>> ListReportsAsync(int maxResults = 50)
        {
            if (Interlocked.Increment(ref _listCallCount) == 1
                && FirstListGate is not null)
            {
                FirstListEntered?.TrySetResult(true);
                return FirstListGate.Task;
            }
            var count = Math.Max(0, Math.Min(SyntheticReportCount, maxResults));
            return Task.FromResult(Enumerable.Range(1, count).Select(index =>
                new ServiceReportSummary
                {
                    Id = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ClientName = "Cliente QA",
                    EquipmentBrand = "CATTECH",
                    EquipmentModel = "Notebook QA"
                }).ToList());
        }

        public TaskCompletionSource<bool>? LoadEntered { get; set; }
        public TaskCompletionSource<bool>? ResumeLoad { get; set; }

        public async Task<ServiceReport?> LoadReportAsync(string reportId)
        {
            if (LoadEntered is not null)
            {
                LoadEntered.TrySetResult(true);
                if (ResumeLoad is not null)
                    await ResumeLoad.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            if (ReturnNullOnLoad)
                return null;

            return new ServiceReport
            {
                Id = reportId,
                CreatedAt = StableDate,
                TechnicianName = TechnicianOverride,
                Client = new ClientInfo
                {
                    Name = ClientNameOverride,
                    Phone = ClientPhoneOverride
                },
                Equipment = new EquipmentInfo { Brand = "CATTECH", Model = "Notebook QA" },
                Service = new ServiceInfo
                {
                    ServiceDate = StableDate,
                    Reason = ServiceReasonOverride,
                    ActionsPerformed = ServiceActionOverride is null
                        ? new List<string>() : new List<string> { ServiceActionOverride }
                }
            };
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
        public int SummaryCount { get; set; }
        public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null) => Task.FromResult(new List<TempCleanupTarget>());
        public Task<TempCleanupResult> CleanupAsync(IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null) => Task.FromResult(new TempCleanupResult());
        public Task<string> SaveResultAsync(TempCleanupResult result) => Task.FromResult("1");
        public Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20) =>
            Task.FromResult(Enumerable.Range(0, Math.Min(SummaryCount, maxResults))
                .Select(i => new TempCleanupResultSummary { Id = $"synthetic-cleanup-{i}" }).ToList());
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
        public int SummaryCount { get; set; }
        public Task<RestorePointStatus> CheckStatusAsync() => Task.FromResult(new RestorePointStatus());
        public Task<RestorePointResult> CreateRestorePointAsync(string name) => Task.FromResult(new RestorePointResult());
        public string GenerateRestorePointName() => "Test Restore Point";
        public Task<string> SaveResultAsync(RestorePointResult result) => Task.FromResult("1");
        public Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20) =>
            Task.FromResult(Enumerable.Range(0, Math.Min(SummaryCount, maxResults))
                .Select(i => new RestorePointResultSummary { Id = $"synthetic-restore-{i}" }).ToList());
    }

    private class FakeSmartDiskService : ISmartDiskService
    {
        public bool ReturnOne { get; set; }
        public bool FailList { get; set; }
        public TaskCompletionSource<IReadOnlyList<SmartAnalysisResult>>? FirstListGate { get; set; }
        public TaskCompletionSource<bool>? FirstListEntered { get; set; }
        private int _listCount;
        public Task<SmartAnalysisResult> AnalyzeAllDisksAsync() => Task.FromResult(new SmartAnalysisResult());
        public Task<SmartDiskReport> AnalyzeDiskAsync(SmartDiskDevice device) => Task.FromResult(new SmartDiskReport());
        public Task<string> SaveResultAsync(SmartAnalysisResult result) => Task.FromResult("1");
        public Task<IReadOnlyList<SmartAnalysisResult>> ListResultsAsync(int maxResults = 20)
        {
            if (Interlocked.Increment(ref _listCount) == 1
                && FirstListGate is not null)
            {
                FirstListEntered?.TrySetResult(true);
                return FirstListGate.Task;
            }
            if (FailList)
                throw new IOException(@"C:\SYNTHETIC-PRIVATE\smart-results.json");
            IReadOnlyList<SmartAnalysisResult> data = ReturnOne
                ? new[] { new SmartAnalysisResult() }
                : Array.Empty<SmartAnalysisResult>();
            return Task.FromResult(data);
        }
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
