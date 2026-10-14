using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class TempCleanupViewModelSelectionTests
{
    [Fact]
    public async Task BoundCheckBoxChange_IsNotInvertedByCommand()
    {
        var model = Target("USER_TEMP");
        var vm = new TempCleanupViewModel(new SyntheticCleanupService(model));
        await vm.ScanCommand.ExecuteAsync(null);
        var bound = Assert.Single(vm.Targets);

        var notifications = new List<string?>();
        bound.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        // WPF TwoWay binding has already set IsSelected when it calls Command.
        bound.IsSelected = true;
        vm.ToggleTargetCommand.Execute(bound);
        Assert.True(bound.IsSelected);
        Assert.Equal(1, vm.SelectedTargetsCount);

        bound.IsSelected = false;
        vm.ToggleTargetCommand.Execute(bound);
        Assert.False(bound.IsSelected);
        Assert.Equal(0, vm.SelectedTargetsCount);
        Assert.Equal(2, notifications.Count(n =>
            n == nameof(TempCleanupTarget.IsSelected)));
    }

    [Fact]
    public async Task SelectAllLowRisk_NeverSelectsSystemOrUnavailableTargets()
    {
        var user = Target("USER_TEMP");
        var system = Target("WINDOWS_TEMP");
        system.IsSystemLocation = true;
        var unavailable = Target("RECYCLE_BIN");
        unavailable.IsAccessible = false;
        unavailable.RiskLevel = CleanupRiskLevel.Medium;

        var vm = new TempCleanupViewModel(
            new SyntheticCleanupService(user, system, unavailable));
        await vm.ScanCommand.ExecuteAsync(null);
        vm.SelectAllLowRiskCommand.Execute(null);

        Assert.True(user.IsSelected);
        Assert.False(system.IsSelected);
        Assert.False(unavailable.IsSelected);
        Assert.Equal(1, vm.SelectedTargetsCount);
    }

    [Fact]
    public async Task UnavailableCheckBox_IsRejectedEvenIfBindingSetsSelected()
    {
        var model = Target("RECYCLE_BIN");
        model.IsAccessible = false;
        var vm = new TempCleanupViewModel(new SyntheticCleanupService(model));
        await vm.ScanCommand.ExecuteAsync(null);

        model.IsSelected = true; // synthetic UI bypass
        vm.ToggleTargetCommand.Execute(model);

        Assert.False(model.IsSelected);
        Assert.Equal(0, vm.SelectedTargetsCount);
    }

    [Fact]
    public async Task RunningCleanup_BlocksOverlappingActions_AndRestoresAvailability()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var service = new SyntheticCleanupService(target);
        var vm = new TempCleanupViewModel(service);
        await vm.ScanCommand.ExecuteAsync(null);
        Assert.False(vm.CanStartCleanup);
        Assert.True(vm.CanReviewCleanupConsent);
        vm.HasCleanupConsent = true;
        Assert.True(vm.CanStartCleanup);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        var pending = new TaskCompletionSource<TempCleanupResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        service.PendingCleanup = pending;
        var operation = vm.CleanupCommand.ExecuteAsync(null);

        Assert.True(vm.IsRunning);
        Assert.True(vm.IsCleaning);
        Assert.False(vm.CanStartCleanup);
        Assert.False(vm.CleanupCommand.CanExecute(null));

        await vm.ScanCommand.ExecuteAsync(null);
        Assert.Equal(1, service.ScanCalls); // no concurrent rescan
        Assert.Equal(1, service.CleanupCalls);

        // The selection already existed and must remain frozen while active.
        vm.DeselectAllCommand.Execute(null);
        Assert.True(target.IsSelected);

        pending.SetResult(new TempCleanupResult
        {
            StartedAt = DateTime.Now.AddSeconds(-1),
            FinishedAt = DateTime.Now
        });
        await operation;

        Assert.False(vm.IsRunning);
        Assert.False(vm.IsCleaning);
        Assert.False(vm.HasCleanupConsent);
        Assert.False(vm.CanStartCleanup);
        Assert.True(vm.CanReviewCleanupConsent);
        Assert.Contains(nameof(vm.CanStartCleanup), changed);
    }


    [Fact]
    public async Task DirectCleanupCommand_WithoutConsent_NeverCallsService()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var service = new SyntheticCleanupService(target);
        var vm = new TempCleanupViewModel(service);
        await vm.ScanCommand.ExecuteAsync(null);

        Assert.False(vm.HasCleanupConsent);
        Assert.False(vm.CanStartCleanup);
        await vm.CleanupCommand.ExecuteAsync(null);

        Assert.Equal(0, service.CleanupCalls);
        Assert.True(vm.HasError);
        Assert.Contains("Confirmá", vm.ErrorMessage);
    }

    [Fact]
    public async Task SelectionChange_InvalidatesPriorApproval()
    {
        var first = Target("USER_TEMP");
        first.IsSelected = true;
        var second = Target("THUMBNAILS");
        var service = new SyntheticCleanupService(first, second);
        var vm = new TempCleanupViewModel(service);
        await vm.ScanCommand.ExecuteAsync(null);

        vm.HasCleanupConsent = true;
        Assert.True(vm.CanStartCleanup);

        second.IsSelected = true;
        vm.ToggleTargetCommand.Execute(second);
        Assert.False(vm.HasCleanupConsent);
        Assert.False(vm.CanStartCleanup);
        await vm.CleanupCommand.ExecuteAsync(null);
        Assert.Equal(0, service.CleanupCalls);
    }

    [Fact]
    public async Task ForgedScopeChange_AfterApproval_RejectsDirectCommand()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var service = new SyntheticCleanupService(target);
        var vm = new TempCleanupViewModel(service);
        await vm.ScanCommand.ExecuteAsync(null);
        vm.HasCleanupConsent = true;
        Assert.True(vm.CanStartCleanup);

        // Bypass selection controls and rewrite a target after approval.
        target.Path = "forged-different-root";
        Assert.False(vm.CanStartCleanup);
        await vm.CleanupCommand.ExecuteAsync(null);

        Assert.Equal(0, service.CleanupCalls);
        Assert.False(vm.HasCleanupConsent);
        Assert.Contains("Confirmá", vm.ErrorMessage);
    }

    [Fact]
    public async Task NewScan_ResetsApprovalAndDisablesCleanup()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var service = new SyntheticCleanupService(target);
        var vm = new TempCleanupViewModel(service);
        await vm.ScanCommand.ExecuteAsync(null);
        vm.HasCleanupConsent = true;
        Assert.True(vm.CanStartCleanup);

        await vm.ScanCommand.ExecuteAsync(null);
        Assert.False(vm.HasCleanupConsent);
        Assert.False(vm.CanStartCleanup);
        Assert.Equal(2, service.ScanCalls);
    }


    [Fact]
    public async Task SkippedFiles_AreShownAsWarnings_NotFullSuccess()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var fake = new SyntheticCleanupService(target);
        var record = new TempCleanupResult
        {
            StartedAt = DateTime.Now.AddSeconds(-2),
            FinishedAt = DateTime.Now,
            SkippedFiles = 2
        };
        fake.PendingCleanup = new TaskCompletionSource<TempCleanupResult>();
        fake.PendingCleanup.SetResult(record);
        var vm = new TempCleanupViewModel(fake);
        await vm.ScanCommand.ExecuteAsync(null);
        vm.HasCleanupConsent = true;

        await vm.CleanupCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.CleanupCalls);
        Assert.Equal(1, fake.SaveCalls);
        Assert.True(vm.HasResult);
        Assert.False(vm.IsSuccess);
        Assert.False(vm.HasError); // Skips only; no critical error.
        Assert.Equal("Limpieza completada con advertencias", vm.StatusText);
        Assert.Contains("2 omitidos", vm.ResultSummary);
    }

    [Fact]
    public async Task ReportedCleanupErrors_AreDisplayedAsIncomplete()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var fake = new SyntheticCleanupService(target);
        var record = new TempCleanupResult
        {
            StartedAt = DateTime.Now.AddSeconds(-2),
            FinishedAt = DateTime.Now
        };
        record.Errors.Add(new CleanupError
        {
            FilePath = "USER_TEMP", Message = "Synthetic", ErrorType = "Test"
        });
        fake.PendingCleanup = new TaskCompletionSource<TempCleanupResult>();
        fake.PendingCleanup.SetResult(record);
        var vm = new TempCleanupViewModel(fake);
        await vm.ScanCommand.ExecuteAsync(null);
        vm.HasCleanupConsent = true;

        await vm.CleanupCommand.ExecuteAsync(null);

        Assert.True(vm.HasResult);
        Assert.False(vm.IsSuccess);
        Assert.True(vm.HasError);
        Assert.Equal("Limpieza incompleta", vm.StatusText);
        Assert.Contains("revisá los errores", vm.ErrorMessage);
    }

    [Fact]
    public async Task FailedAuditSave_KeepsActualDeletionResultVisible()
    {
        var target = Target("USER_TEMP");
        target.IsSelected = true;
        var fake = new SyntheticCleanupService(target) { ThrowOnSave = true };
        fake.PendingCleanup = new TaskCompletionSource<TempCleanupResult>();
        fake.PendingCleanup.SetResult(new TempCleanupResult
        {
            StartedAt = DateTime.Now.AddSeconds(-2),
            FinishedAt = DateTime.Now,
            DeletedFiles = 3,
            DeletedBytes = 512
        });
        var vm = new TempCleanupViewModel(fake);
        await vm.ScanCommand.ExecuteAsync(null);
        vm.HasCleanupConsent = true;

        await vm.CleanupCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.CleanupCalls);
        Assert.Equal(1, fake.SaveCalls);
        Assert.True(vm.HasResult);
        Assert.Equal(3, vm.ResultDeletedFiles);
        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Equal("Limpieza ejecutada; registro no guardado", vm.StatusText);
        Assert.Contains("NO pudo guardarse", vm.ResultSummary);
        Assert.Contains("no se pudo guardar su historial", vm.ErrorMessage);
        Assert.DoesNotContain("PRIVATE", vm.ErrorMessage);
    }

    [Fact]
    public void CleanupXaml_UsesBooleanEnabledBinding()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.UI/Views/TempCleanupView.xaml"));
        var xaml = File.ReadAllText(path);

        Assert.Contains("IsEnabled=\"{Binding CanStartCleanup}\"", xaml);
        Assert.DoesNotContain(
            "IsEnabled=\"{Binding HasScanned, Converter={StaticResource BoolToVis}}\"", xaml);
        Assert.Contains("IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"", xaml);
        Assert.Contains("DataContext.IsRunning", xaml);
        Assert.Contains("IsChecked=\"{Binding HasCleanupConsent, Mode=TwoWay}\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanReviewCleanupConsent}\"", xaml);
        Assert.Contains("no crea una copia de seguridad", xaml);
    }


    [Fact]
    public void CleanupXaml_ShowsStatusAndHonestSafetyWarnings()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../src/Cattech.Optimizer.Pro.UI/Views/TempCleanupView.xaml"));
        var xaml = File.ReadAllText(path);

        Assert.NotNull(System.Xml.Linq.XDocument.Parse(xaml).Root);
        Assert.Contains("Text=\"{Binding StatusText}\"", xaml);
        Assert.Contains("La Papelera está deshabilitada por seguridad", xaml);
        Assert.Contains("No se crea respaldo", xaml);
        Assert.DoesNotContain("Se omite Papelera si no fue seleccionada", xaml);
        Assert.DoesNotContain("No se eliminan perfiles de navegador", xaml);
    }

    private static TempCleanupTarget Target(string id) => new()
    {
        Id = id,
        DisplayName = "Synthetic " + id,
        Path = "synthetic-not-for-cleaning",
        IsScanned = true,
        IsAccessible = true,
        RiskLevel = CleanupRiskLevel.Low
    };

    private sealed class SyntheticCleanupService : ITempCleanupService
    {
        private readonly List<TempCleanupTarget> _targets;

        public SyntheticCleanupService(params TempCleanupTarget[] targets)
            => _targets = targets.ToList();

        public int ScanCalls { get; private set; }
        public int CleanupCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public bool ThrowOnSave { get; set; }
        public TaskCompletionSource<TempCleanupResult>? PendingCleanup { get; set; }

        public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null)
        {
            ScanCalls++;
            return Task.FromResult(_targets);
        }

        public Task<TempCleanupResult> CleanupAsync(
            IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null)
        {
            CleanupCalls++;
            return PendingCleanup?.Task ?? Task.FromResult(new TempCleanupResult
            {
                StartedAt = DateTime.Now,
                FinishedAt = DateTime.Now
            });
        }

        public Task<string> SaveResultAsync(TempCleanupResult result)
        {
            SaveCalls++;
            if (ThrowOnSave)
                throw new IOException(@"Synthetic PRIVATE path C:\Users\PRIVATE\audit.json");
            return Task.FromResult("synthetic.json");
        }

        public Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20)
            => Task.FromResult(new List<TempCleanupResultSummary>());
    }
}
