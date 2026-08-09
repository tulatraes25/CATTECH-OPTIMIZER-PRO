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
    /// Abre una sesión, refresca una vez, captura y cierra.
    /// No modifica hardware, no ejecuta controles de ventiladores ni escribe configuraciones.
    /// </summary>
    Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produce un flujo de snapshots de temperatura reutilizando UNA sola sesión abierta.
    /// La primera muestra es inmediata; después de cada muestra espera el intervalo.
    /// El ciclo de vida se controla por el async enumerable: al cancelar, interrumpir o
    /// abandonar la enumeración, la sesión se libera.
    /// </summary>
    /// <param name="interval">Intervalo positivo entre muestras. Debe ser mayor que TimeSpan.Zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="interval"/> es menor o igual a TimeSpan.Zero.</exception>
    IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
        TimeSpan interval,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un snapshot live read-only: temperaturas + métricas de rendimiento
    /// (Load/Clock de CPU/GPU) de UN solo Refresh de la sesión.
    /// Abre una sesión, refresca una vez, captura y cierra.
    /// </summary>
    Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Produce un flujo de snapshots live reutilizando UNA sola sesión abierta.
    /// Cada muestra combina temperatura + Load + Clock de un mismo Refresh.
    /// Primera muestra inmediata; después de cada muestra espera el intervalo.
    /// Al cancelar, interrumpir o abandonar la enumeración, la sesión se libera.
    /// </summary>
    /// <param name="interval">Intervalo positivo entre muestras. Debe ser mayor que TimeSpan.Zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="interval"/> es menor o igual a TimeSpan.Zero.</exception>
    IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
        TimeSpan interval,
        CancellationToken cancellationToken = default);
}
