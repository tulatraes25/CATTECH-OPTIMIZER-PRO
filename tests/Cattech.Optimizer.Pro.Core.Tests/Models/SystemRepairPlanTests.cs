using Cattech.Optimizer.Pro.Core.Models.Repair;
using Cattech.Optimizer.Pro.Infrastructure.Repair;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SystemRepairPlanTests
{
    private static readonly SystemRepairPlanService Plans = new();

    [Fact]
    public void HasExactlyFiveFixedAndRecognizedOperations()
    {
        var plans = Plans.GetAvailablePlans();
        Assert.Equal(5, plans.Count);
        Assert.Equal(5, plans.Select(x => x.Operation).Distinct().Count());
        Assert.All(plans, x => Assert.NotEmpty(x.Arguments));
        Assert.All(plans, x => Assert.True(x.RequiresExplicitConfirmation));
        Assert.All(plans, x => Assert.True(x.RequiresAdministrator));
        Assert.All(plans, x => Assert.False(x.ExecutionAvailable));
    }

    [Theory]
    [InlineData(SystemRepairOperation.DismCheckHealth)]
    [InlineData(SystemRepairOperation.DismScanHealth)]
    [InlineData(SystemRepairOperation.SfcVerifyOnly)]
    public void InspectionPlans_AreReadOnly(SystemRepairOperation operation)
    {
        var plan = Plans.CreatePlan(operation);
        Assert.Equal(SystemRepairImpact.InspectionOnly, plan.Impact);
        Assert.False(plan.MayUseNetwork);
        Assert.DoesNotContain(plan.Arguments, arg =>
            arg.Equals("/RestoreHealth", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("/scannow", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(SystemRepairOperation.DismRestoreHealth)]
    [InlineData(SystemRepairOperation.SfcScanNow)]
    public void RepairPlans_AreFlaggedAsModifying(SystemRepairOperation operation)
    {
        var plan = Plans.CreatePlan(operation);
        Assert.Equal(SystemRepairImpact.ModifiesWindows, plan.Impact);
        Assert.True(plan.RequiresExplicitConfirmation);
        Assert.False(plan.ExecutionAvailable);
        Assert.Contains("repar", plan.SafetyNote, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestoreHealth_CanReachWindowsUpdateUnlessExplicitSourceConfigured()
    {
        var plan = Plans.CreatePlan(SystemRepairOperation.DismRestoreHealth);
        Assert.True(plan.MayUseNetwork);
        Assert.Contains("/RestoreHealth", plan.Arguments);
        Assert.DoesNotContain("/LimitAccess", plan.Arguments);
    }

    [Fact]
    public void SfcScannow_IsNeverTreatedAsInspection()
    {
        var plan = Plans.CreatePlan(SystemRepairOperation.SfcScanNow);
        Assert.Equal(new[] { "/scannow" }, plan.Arguments);
        Assert.Equal(SystemRepairImpact.ModifiesWindows, plan.Impact);
    }

    [Fact]
    public void ExecutablesAreOnlyMicrosoftSystemTools_NotUserInput()
    {
        var plans = Plans.GetAvailablePlans();
        Assert.All(plans, x => Assert.Contains(x.ExecutableName,
            new[] { "dism.exe", "sfc.exe" }));
        Assert.All(plans, x => Assert.DoesNotContain(x.Arguments,
            arg => arg.Contains("&") || arg.Contains("|") || arg.Contains("..")));
    }

    [Fact]
    public void InvalidEnumIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Plans.CreatePlan((SystemRepairOperation)int.MaxValue));
    }
}
