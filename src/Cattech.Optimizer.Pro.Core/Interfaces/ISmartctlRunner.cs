using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para ejecutar comandos smartctl y gestionar su disponibilidad.
/// smartctl se invoca como binario externo (no se integra código GPL).
/// </summary>
public interface ISmartctlRunner
{
    /// <summary>
    /// Verifica si smartctl está disponible en el sistema.
    /// Detecta ubicación, versión y soporte JSON.
    /// </summary>
    Task<SmartctlAvailability> CheckAvailabilityAsync();

    /// <summary>
    /// Ejecuta un comando smartctl con timeout.
    /// </summary>
    Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout);

    /// <summary>
    /// Lista dispositivos de almacenamiento detectados por smartctl.
    /// </summary>
    Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync();
}
