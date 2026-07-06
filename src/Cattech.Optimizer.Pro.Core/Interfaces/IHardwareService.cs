using Cattech.Optimizer.Pro.Core.Models.Hardware;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para detectar y obtener información del hardware.
/// </summary>
public interface IHardwareService
{
    /// <summary>
    /// Obtiene un reporte completo del hardware del sistema.
    /// </summary>
    Task<HardwareReport> GetHardwareReportAsync();

    /// <summary>
    /// Obtiene información del sistema operativo.
    /// </summary>
    Task<SystemInfo> GetSystemInfoAsync();

    /// <summary>
    /// Obtiene información de la CPU.
    /// </summary>
    Task<CpuInfo> GetCpuInfoAsync();

    /// <summary>
    /// Obtiene información de la memoria RAM.
    /// </summary>
    Task<MemoryInfo> GetMemoryInfoAsync();

    /// <summary>
    /// Obtiene información de las GPUs.
    /// </summary>
    Task<List<GpuInfo>> GetGpuInfoAsync();

    /// <summary>
    /// Obtiene información de los discos.
    /// </summary>
    Task<List<DiskInfo>> GetDiskInfoAsync();

    /// <summary>
    /// Obtiene información de la placa madre.
    /// </summary>
    Task<MotherboardInfo> GetMotherboardInfoAsync();
}
