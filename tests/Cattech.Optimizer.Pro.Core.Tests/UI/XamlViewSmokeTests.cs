using Cattech.Optimizer.Pro.UI.ViewModels;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.Core.Tests.UI;

/// <summary>
/// XAML load smoke tests: build the real WPF views on an STA thread to catch
/// StaticResource resolution failures (XamlParseException) that unit tests
/// with fakes cannot detect. Regression for SMOKE-B1-001.
/// </summary>
public class XamlViewSmokeTests
{
    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        if (failure != null)
        {
            throw failure;
        }
    }

    [Fact]
    public void ClientEquipmentView_LoadsOnSta_WithoutXamlParseException()
    {
        RunOnSta(() =>
        {
            var view = new ClientEquipmentView();
            Assert.NotNull(view);
        });
    }

    [Fact]
    public void NavigationToClientEquipment_LoadsView_WithoutXamlParseException()
    {
        RunOnSta(() =>
        {
            var vm = new MainViewModel();
            vm.NavigateCommand.Execute("ClientEquipment");

            Assert.Equal("ClientEquipment", vm.CurrentSection);
            Assert.IsType<ClientEquipmentView>(vm.CurrentView);
        });
    }

    [Fact]
    public void RealMainClientNavigation_RotatesLogicalCaseOnlyOnClientSelectionChanges()
    {
        RunOnSta(() =>
        {
            var main = new MainViewModel();
            Assert.Null(main.ActiveLogicalCase);
            main.NavigateCommand.Execute("ClientEquipment");

            var initial = Assert.IsType<Cattech.Optimizer.Pro.Core.Models.Evidence.DiagnosticContextId>(
                main.ActiveLogicalCase);
            var initialLease = main.LogicalCaseCoordinator.CaptureLeaseFor(initial);
            Assert.NotNull(initialLease);
            var form = Assert.IsType<ClientEquipmentViewModel>(
                Assert.IsType<ClientEquipmentView>(main.CurrentView).DataContext);

            form.ClientName = "Synthetic Selected Customer";
            Assert.False(initialLease.IsCurrent);
            var revised = Assert.IsType<Cattech.Optimizer.Pro.Core.Models.Evidence.DiagnosticContextId>(
                main.ActiveLogicalCase);
            var revisedLease = main.LogicalCaseCoordinator.CaptureLeaseFor(revised);
            Assert.NotNull(revisedLease);
            Assert.True(revisedLease.IsCurrent);

            main.NavigateCommand.Execute("Home");
            main.NavigateCommand.Execute("ClientEquipment");
            Assert.True(revisedLease.IsCurrent); // Navigation alone is not case reassignment.

            form.NewRecordCommand.Execute(null);
            Assert.False(revisedLease.IsCurrent);
            Assert.NotEqual(revised, main.ActiveLogicalCase);
        });
    }

    [Fact]
    public void NavigationToBlueScreens_LoadsView_WithoutXamlParseException()
    {
        RunOnSta(() =>
        {
            var vm = new MainViewModel();
            vm.NavigateCommand.Execute("BlueScreens");

            Assert.Equal("BlueScreens", vm.CurrentSection);
            Assert.IsType<CrashEvidenceView>(vm.CurrentView);
        });
    }

    [Fact]
    public void NavigationToRepairCenter_LoadsView_WithoutXamlParseException()
    {
        RunOnSta(() =>
        {
            var vm = new MainViewModel();
            vm.NavigateCommand.Execute("RepairCenter");
            Assert.Equal("RepairCenter", vm.CurrentSection);
            Assert.IsType<RepairCenterView>(vm.CurrentView);
        });
    }

    [Fact]
    public void AllSidebarViews_LoadOnSta_WithoutXamlParseException()
    {
        var viewFactories = new (string Name, Func<object> Factory)[]
        {
            ("ClientEquipment", () => new ClientEquipmentView()),
            ("CompanySettings", () => new CompanySettingsView()),
            ("SmartDisk", () => new SmartDiskView()),
            ("Hardware", () => new HardwareView()),
            ("QuickDiagnostic", () => new QuickDiagnosticView()),
            ("CrashEvidence", () => new CrashEvidenceView()),
            ("RepairCenter", () => new RepairCenterView()),
            ("StartupAnalysis", () => new StartupAnalysisView()),
            ("TempCleanup", () => new TempCleanupView()),
            ("VisualOptimization", () => new VisualOptimizationView()),
            ("RestorePoint", () => new RestorePointView()),
            ("Reports", () => new ReportView())
        };

        foreach (var (name, factory) in viewFactories)
        {
            RunOnSta(() =>
            {
                var view = factory();
                Assert.NotNull(view);
            });
        }
    }
}
