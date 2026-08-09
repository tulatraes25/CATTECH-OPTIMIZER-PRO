using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Servicio de sensores de hardware dinámicos (temperatura).
/// Capa separada de IHardwareService (datos estáticos vía WMI).
/// </summary>
public interface IHardwareSensorService
{
    /// <summary>
    /// Obtiene un snapshot read-only de sensores de temperatura.
    /// No modifica hardware, no ejecuta controles de ventiladores ni escribe configuraciones.
    /// </summary>
    Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
        CancellationToken cancellationToken = default);
}
