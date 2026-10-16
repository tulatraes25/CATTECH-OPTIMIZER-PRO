using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Inspects SFC/DISM prerequisites without starting or changing any process.
/// </summary>
public interface ISystemRepairPreflightService
{
    SystemRepairPreflightResult Inspect(SystemRepairOperation operation);
}
