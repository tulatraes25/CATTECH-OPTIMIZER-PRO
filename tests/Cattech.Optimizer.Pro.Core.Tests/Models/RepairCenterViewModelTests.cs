using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Repair;
using Cattech.Optimizer.Pro.Infrastructure.Repair;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class RepairCenterViewModelTests
{
    private sealed class FakeWindowsUpdateInspection : IWindowsUpdateReadinessService
    {
        public int Calls { get; private set; }
        public Func<WindowsUpdateReadinessResult> Next { get; set; } = () => new()
        {
            Status = WindowsUpdateReadinessStatus.Observed,
            Services = new[]
            {
                Service("wuauserv", WindowsServiceRunState.Stopped),
                Service("BITS", WindowsServiceRunState.Running),
                Service("cryptsvc", WindowsServiceRunState.Running),
                Service("UsoSvc", WindowsServiceRunState.Stopped)
            }
        };

        public WindowsUpdateReadinessResult Inspect()
        {
            Calls++;
            return Next();
        }
    }

    private sealed class FakePreflight : ISystemRepairPreflightService
    {
        public int Calls { get; private set; }
        public SystemRepairOperation? Requested { get; private set; }
        public Func<SystemRepairOperation, SystemRepairPreflightResult> Next { get; set; }
            = operation => new()
            {
                Operation = operation,
                Status = SystemRepairPreflightStatus.ReviewRequired
            };

        public SystemRepairPreflightResult Inspect(SystemRepairOperation operation)
        {
            Calls++;
            Requested = operation;
            return Next(operation);
        }
    }

    private static WindowsUpdateServiceObservation Service(
        string name, WindowsServiceRunState state) => new()
    {
        ServiceName = name,
        State = state,
        StartMode = "Manual"
    };

    private static RepairCenterViewModel NewVm(
        FakeWindowsUpdateInspection update, FakePreflight preflight)
        => new(update, new SystemRepairPlanService(), preflight);

    [Fact]
    public void Construction_ShowsPlansWithoutInspectingWindowsOrPreflight()
    {
        var update = new FakeWindowsUpdateInspection();
        var preflight = new FakePreflight();
        var vm = NewVm(update, preflight);

        Assert.Equal(5, vm.Plans.Count);
        Assert.Equal(2, vm.Plans.Count(x => x.IsModifying));
        Assert.All(vm.Plans.Where(x => x.IsModifying), plan =>
        {
            Assert.Contains("MODIFICA WINDOWS", plan.ImpactText);
            Assert.Contains("NO DISPONIBLE", plan.ImpactText);
        });
        Assert.Empty(vm.UpdateServices);
        Assert.Equal(0, update.Calls);
        Assert.Equal(0, preflight.Calls);
        Assert.False(vm.InspectSelectedPlanCommand.CanExecute(null));
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task WindowsUpdateInspection_LabelsStoppedAsPotentiallyNormal()
    {
        var update = new FakeWindowsUpdateInspection();
        var preflight = new FakePreflight();
        var vm = NewVm(update, preflight);

        await vm.InspectWindowsUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, update.Calls);
        Assert.Equal(0, preflight.Calls);
        Assert.Equal(4, vm.UpdateServices.Count);
        Assert.Equal("Detenido (puede ser normal)", vm.UpdateServices[0].StateText);
        Assert.Equal("Manual", vm.UpdateServices[0].StartModeText);
        Assert.Contains("NO confirma", vm.UpdateStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task WindowsUpdateInspection_PartialDataIsNotCalledHealthy()
    {
        var update = new FakeWindowsUpdateInspection
        {
            Next = () => new WindowsUpdateReadinessResult
            {
                Status = WindowsUpdateReadinessStatus.Partial,
                Services = new[]
                {
                    Service("wuauserv", WindowsServiceRunState.Stopped),
                    Service("UsoSvc", WindowsServiceRunState.NotFound)
                }
            }
        };
        var vm = NewVm(update, new FakePreflight());

        await vm.InspectWindowsUpdateCommand.ExecuteAsync(null);

        Assert.Contains("parcial", vm.UpdateStatusText);
        Assert.Equal("No encontrado", vm.UpdateServices[1].StateText);
        Assert.DoesNotContain("saludable", vm.UpdateStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateRefreshFailure_RemovesOldEvidenceAndSanitizesException()
    {
        var update = new FakeWindowsUpdateInspection();
        var vm = NewVm(update, new FakePreflight());

        await vm.InspectWindowsUpdateCommand.ExecuteAsync(null);
        Assert.Equal(4, vm.UpdateServices.Count);

        update.Next = () => throw new IOException(@"C:\Users\SensitivePerson\secret");
        await vm.InspectWindowsUpdateCommand.ExecuteAsync(null);

        Assert.Equal(2, update.Calls);
        Assert.Empty(vm.UpdateServices);
        Assert.Contains("no se pudo completar", vm.UpdateStatusText,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SensitivePerson", vm.UpdateStatusText);
    }

    [Fact]
    public async Task UpdateOnlyDisplaysAllowlistedServicesAndModes()
    {
        var update = new FakeWindowsUpdateInspection
        {
            Next = () => new WindowsUpdateReadinessResult
            {
                Status = WindowsUpdateReadinessStatus.Observed,
                Services = new[]
                {
                    Service("wuauserv", WindowsServiceRunState.Running),
                    new WindowsUpdateServiceObservation
                    {
                        ServiceName = @"C:\Users\SensitivePerson",
                        State = WindowsServiceRunState.Other,
                        StartMode = "Secret"
                    }
                }
            }
        };
        var vm = NewVm(update, new FakePreflight());

        await vm.InspectWindowsUpdateCommand.ExecuteAsync(null);

        Assert.Single(vm.UpdateServices);
        Assert.Equal("wuauserv", vm.UpdateServices[0].Name);
        Assert.DoesNotContain("SensitivePerson", vm.UpdateStatusText);
    }

    [Fact]
    public async Task SelectedPlan_PreflightNeverAuthorizesExecution()
    {
        var update = new FakeWindowsUpdateInspection();
        var preflight = new FakePreflight();
        var vm = NewVm(update, preflight);

        vm.SelectedPlan = vm.Plans.Single(x =>
            x.Operation == SystemRepairOperation.DismRestoreHealth);
        Assert.True(vm.InspectSelectedPlanCommand.CanExecute(null));
        Assert.Contains("/RestoreHealth", vm.SelectedPlan.CommandPreview);
        Assert.True(vm.SelectedPlan.IsModifying);

        await vm.InspectSelectedPlanCommand.ExecuteAsync(null);

        Assert.Equal(1, preflight.Calls);
        Assert.Equal(SystemRepairOperation.DismRestoreHealth, preflight.Requested);
        Assert.Equal(0, update.Calls);
        Assert.Contains("SIGUE DESHABILITADA", vm.PreflightStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ChangeSelection_ClearsPreviousPreflightResult()
    {
        var vm = NewVm(new FakeWindowsUpdateInspection(), new FakePreflight());
        vm.SelectedPlan = vm.Plans[0];
        await vm.InspectSelectedPlanCommand.ExecuteAsync(null);
        Assert.Contains("Requisitos inspeccionados", vm.PreflightStatusText);

        vm.SelectedPlan = vm.Plans[1];

        Assert.Contains("Sin revisar este plan", vm.PreflightStatusText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanAtoBtoA_DuringPreflight_NeverReusesStaleResult(bool providerFails)
    {
        var entered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var preflight = new FakePreflight
        {
            Next = operation =>
            {
                entered.TrySetResult(true);
                if (!release.Wait(TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Synthetic preflight reader not released.");

                if (providerFails)
                    throw new IOException("synthetic private preflight failure");
                return new SystemRepairPreflightResult
                {
                    Operation = operation,
                    Status = SystemRepairPreflightStatus.ReviewRequired
                };
            }
        };
        var vm = NewVm(new FakeWindowsUpdateInspection(), preflight);
        var planA = vm.Plans[0];
        var planB = vm.Plans[1];
        vm.SelectedPlan = planA;

        var pending = vm.InspectSelectedPlanCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

            vm.SelectedPlan = planB;
            vm.SelectedPlan = planA;
            Assert.Contains("Sin revisar este plan", vm.PreflightStatusText);
        }
        finally
        {
            release.Set();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(1, preflight.Calls);
        Assert.Equal(planA.Operation, preflight.Requested);
        Assert.Contains("Sin revisar este plan", vm.PreflightStatusText);
        Assert.DoesNotContain("Requisitos inspeccionados", vm.PreflightStatusText);
        Assert.DoesNotContain("private preflight", vm.PreflightStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task UnrecognizedPlanCannotBeInspected()
    {
        var preflight = new FakePreflight();
        var vm = NewVm(new FakeWindowsUpdateInspection(), preflight);
        vm.SelectedPlan = new RepairPlanDisplayItem
        {
            Operation = (SystemRepairOperation)int.MaxValue,
            DisplayName = "Injected plan"
        };

        Assert.False(vm.InspectSelectedPlanCommand.CanExecute(null));
        await vm.InspectSelectedPlanCommand.ExecuteAsync(null);
        Assert.Equal(0, preflight.Calls);
    }

    [Fact]
    public async Task MismatchedPreflightResponseDoesNotLookApproved()
    {
        var preflight = new FakePreflight
        {
            Next = _ => new SystemRepairPreflightResult
            {
                Operation = SystemRepairOperation.SfcScanNow,
                Status = SystemRepairPreflightStatus.ReviewRequired
            }
        };
        var vm = NewVm(new FakeWindowsUpdateInspection(), preflight);
        vm.SelectedPlan = vm.Plans.Single(x =>
            x.Operation == SystemRepairOperation.DismCheckHealth);

        await vm.InspectSelectedPlanCommand.ExecuteAsync(null);

        Assert.Contains("no corresponde", vm.PreflightStatusText);
        Assert.DoesNotContain("Requisitos inspeccionados", vm.PreflightStatusText);
    }

    [Fact]
    public async Task PreflightExceptionDoesNotExposeHostDetails()
    {
        var preflight = new FakePreflight
        {
            Next = _ => throw new InvalidOperationException(
                @"C:\Users\SensitivePerson\secrets")
        };
        var vm = NewVm(new FakeWindowsUpdateInspection(), preflight);
        vm.SelectedPlan = vm.Plans[0];

        await vm.InspectSelectedPlanCommand.ExecuteAsync(null);

        Assert.Contains("no se pudieron verificar", vm.PreflightStatusText);
        Assert.DoesNotContain("SensitivePerson", vm.PreflightStatusText);
        Assert.False(vm.IsBusy);
    }
}
