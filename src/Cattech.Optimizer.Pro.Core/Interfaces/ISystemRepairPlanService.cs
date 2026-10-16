using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>Solo produce planes visibles; no puede ejecutar nada.</summary>
public interface ISystemRepairPlanService
{
    SystemRepairOperationPlan CreatePlan(SystemRepairOperation operation);
    IReadOnlyList<SystemRepairOperationPlan> GetAvailablePlans();
}
