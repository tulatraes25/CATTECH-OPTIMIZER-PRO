using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Infrastructure.Startup;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Regresiones de la desactivación real de programas de inicio.
/// Solo se usan valores HKCU únicos y reversibles; nunca HKLM ni servicios.
/// </summary>
public class StartupServiceFixTests
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledRunKey =
        @"Software\CATTECH\OptimizerPro\DisabledStartup\Run";

    private static string NewFixtureDir()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "cattech-startup-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch { }
    }

    private static void DeleteHkcuValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
        catch { }
    }

    [Fact]
    public async Task DisableRegistryEntry_RemovesAndRestoresRealHkcuValue()
    {
        var valueName = "CATTECH_QA_" + Guid.NewGuid().ToString("N")[..8];
        var baseDir = NewFixtureDir();
        const string entryId = "QATEST01";
        try
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true))
            {
                Assert.NotNull(run);
                run!.SetValue(valueName, "\"notepad.exe\"", RegistryValueKind.String);
            }

            var service = new StartupService(baseDir);
            var entry = new StartupEntry
            {
                Id = entryId,
                Name = valueName,
                Command = "\"notepad.exe\"",
                Location = "HKCU\\" + RunKey,
                SourceType = StartupSourceType.RegistryRun
            };

            // La desactivación DEBE borrar el valor: la regresión abría la
            // clave en solo-lectura y DeleteValue fallaba siempre.
            var summary = await service.DisableSelectedAsync(new[] { entry });

            Assert.Equal(1, summary.SuccessCount);
            Assert.Equal(0, summary.FailedCount);
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey))
                Assert.Null(run?.GetValue(valueName));

            var backups = await service.ListBackupsAsync();
            var backup = Assert.Single(backups);
            Assert.True(backup.CanRestore);
            Assert.Equal("String", backup.OriginalValueKind);
            Assert.Equal("\"notepad.exe\"", backup.BackupValue);

            var restore = await service.RestoreAsync(backup);
            Assert.Equal(StartupActionResult.Success, restore);
            using (var run = Registry.CurrentUser.OpenSubKey(RunKey))
                Assert.Equal("\"notepad.exe\"", run?.GetValue(valueName));
        }
        finally
        {
            DeleteHkcuValue(RunKey, valueName);
            DeleteHkcuValue(DisabledRunKey, $"CATTECH_{entryId}_{valueName}");
            TryDeleteDirectory(baseDir);
        }
    }

    [Fact]
    public async Task DisableRegistryEntry_PreservesExpandStringKind()
    {
        var valueName = "CATTECH_QA_EXP_" + Guid.NewGuid().ToString("N")[..6];
        var baseDir = NewFixtureDir();
        const string entryId = "QAEXP001";
        const string originalValue = @"%TEMP%\cattech-qa-startup.exe";
        try
        {
            using (var run = Registry.CurrentUser.CreateSubKey(RunKey, writable: true))
            {
                Assert.NotNull(run);
                run!.SetValue(valueName, originalValue, RegistryValueKind.ExpandString);
            }

            var service = new StartupService(baseDir);
            var entry = new StartupEntry
            {
                Id = entryId,
                Name = valueName,
                Command = originalValue,
                Location = "HKCU\\" + RunKey,
                SourceType = StartupSourceType.RegistryRun
            };

            var summary = await service.DisableSelectedAsync(new[] { entry });
            Assert.Equal(1, summary.SuccessCount);

            var backup = Assert.Single(await service.ListBackupsAsync());
            Assert.Equal("ExpandString", backup.OriginalValueKind);

            Assert.Equal(StartupActionResult.Success, await service.RestoreAsync(backup));

            using var restored = Registry.CurrentUser.OpenSubKey(RunKey);
            Assert.NotNull(restored);
            Assert.Equal(RegistryValueKind.ExpandString, restored!.GetValueKind(valueName));
            Assert.Equal(originalValue, restored.GetValue(
                valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames));
        }
        finally
        {
            DeleteHkcuValue(RunKey, valueName);
            DeleteHkcuValue(DisabledRunKey, $"CATTECH_{entryId}_{valueName}");
            TryDeleteDirectory(baseDir);
        }
    }

    [Theory]
    [InlineData("LibreOffice Quickstarter",
        @"C:\Program Files\LibreOffice\program\soffice.exe --quickstart", false)]
    [InlineData("Microsoft OneDrive",
        @"C:\Program Files\Microsoft OneDrive\OneDrive.exe /silent", true)]
    [InlineData("Windows Defender notification icon",
        @"C:\Program Files\Windows Defender\MSASCuiL.exe", true)]
    [InlineData("Steam Client Bootstrapper", @"D:\Steam\steam.exe -silent", false)]
    [InlineData("Seguridad Antivirus Pro",
        @"C:\Program Files\Seguridad\av.exe --background", false)]
    public void Classifier_DoesNotBlockNormalThirdPartyEntries(
        string name, string command, bool expectedMicrosoft)
    {
        var entry = new StartupEntry
        {
            Name = name,
            Command = command,
            Location = "HKCU\\" + RunKey,
            SourceType = StartupSourceType.RegistryRun
        };

        StartupService.ClassifyEntry(entry);

        Assert.Equal(expectedMicrosoft, entry.IsMicrosoft);
        Assert.Equal(!expectedMicrosoft,
            new StartupService().CanDisableStartupEntry(entry));
    }

    [Fact]
    public void ComputeEntryId_IsStableAcrossAnalyses()
    {
        var first = new StartupEntry
        {
            Name = "Synthetic App",
            Command = @"C:\synthetic\app.exe",
            Location = "HKCU\\" + RunKey,
            SourceType = StartupSourceType.RegistryRun
        };
        var second = new StartupEntry
        {
            Name = "Synthetic App",
            Command = @"C:\synthetic\app.exe",
            Location = "HKCU\\" + RunKey,
            SourceType = StartupSourceType.RegistryRun
        };
        var otherLocation = new StartupEntry
        {
            Name = "Synthetic App",
            Command = @"C:\synthetic\app.exe",
            Location = "HKLM\\" + RunKey,
            SourceType = StartupSourceType.RegistryRun
        };

        Assert.Equal(8, StartupService.ComputeEntryId(first).Length);
        Assert.Equal(StartupService.ComputeEntryId(first), StartupService.ComputeEntryId(second));
        Assert.NotEqual(StartupService.ComputeEntryId(first),
            StartupService.ComputeEntryId(otherLocation));
    }

    [Fact]
    public async Task SelectAll_SelectsEveryDisableableEntry_AndToggleFlipsOnce()
    {
        var fake = new FakeStartupService(
        [
            Synthetic("ThirdPartyKeep", StartupRecommendation.Keep),
            Synthetic("ThirdPartyReview", StartupRecommendation.Review),
            Synthetic("MicrosoftThing", StartupRecommendation.Keep, isMicrosoft: true)
        ]);
        var viewModel = new StartupAnalysisViewModel(fake);
        await viewModel.AnalyzeCommand.ExecuteAsync(null);

        viewModel.SelectAllPossibleDisableCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedCount);

        // Un click en el checkbox = un solo cambio de estado (la regresión
        // TwoWay + Command lo invertía dos veces y nada quedaba seleccionado).
        var item = viewModel.SelectableEntries.First(entry => !entry.Entry.IsMicrosoft);
        item.IsSelected = false;
        viewModel.ToggleSelectionCommand.Execute(item);
        Assert.True(item.IsSelected);
        Assert.Equal(2, viewModel.SelectedCount);
    }

    [Fact]
    public async Task DisableWithoutSelection_ShowsVisibleError()
    {
        var fake = new FakeStartupService([Synthetic("ThirdParty", StartupRecommendation.Review)]);
        var viewModel = new StartupAnalysisViewModel(fake);
        await viewModel.AnalyzeCommand.ExecuteAsync(null);

        await viewModel.DisableSelectedCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
        Assert.Contains("seleccionadas", viewModel.ErrorMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    private static StartupEntry Synthetic(
        string name, StartupRecommendation recommendation, bool isMicrosoft = false) => new()
    {
        Name = name,
        Command = $@"C:\synthetic\{name}.exe",
        Location = "HKCU\\" + RunKey,
        SourceType = StartupSourceType.RegistryRun,
        IsMicrosoft = isMicrosoft,
        Recommendation = recommendation
    };

    private sealed class FakeStartupService(IReadOnlyList<StartupEntry> entries)
        : IStartupService
    {
        public Task<StartupAnalysis> AnalyzeStartupAsync() =>
            Task.FromResult(new StartupAnalysis { Entries = entries.ToList() });

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
            Task.FromResult("synthetic.json");

        public Task<StartupAnalysis?> LoadAnalysisAsync(string analysisId) =>
            Task.FromResult<StartupAnalysis?>(null);

        public Task<List<StartupAnalysisSummary>> ListAnalysesAsync(int maxResults = 20) =>
            Task.FromResult(new List<StartupAnalysisSummary>());

        public Task<bool> DeleteAnalysisAsync(string analysisId) => Task.FromResult(false);
    }
}
