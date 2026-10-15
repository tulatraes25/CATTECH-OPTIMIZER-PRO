using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>Inspección solo lectura; no cambia servicios ni cachés.</summary>
public interface IWindowsUpdateReadinessService
{
    WindowsUpdateReadinessResult Inspect();
}
