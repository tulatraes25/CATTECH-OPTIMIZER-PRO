namespace Cattech.Optimizer.Pro.Core.Models.Hardware;

/// <summary>
/// Información general del sistema operativo.
/// </summary>
public class SystemInfo
{
    /// <summary>
    /// Nombre del sistema operativo (ej: Windows 11 Pro).
    /// </summary>
    public string OsName { get; set; } = string.Empty;

    /// <summary>
    /// Versión del SO (ej: 23H2).
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Número de build (ej: 22631.3880).
    /// </summary>
    public string BuildNumber { get; set; } = string.Empty;

    /// <summary>
    /// Arquitectura del SO (x64, ARM64).
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de instalación o último upgrade.
    /// </summary>
    public DateTime InstallDate { get; set; }

    /// <summary>
    /// Nombre del equipo (hostname).
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;
}

/// <summary>
/// Información de la CPU.
/// </summary>
public class CpuInfo
{
    /// <summary>
    /// Nombre del procesador (ej: Intel Core i7-13700K).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fabricante (Intel, AMD).
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Número de núcleos físicos.
    /// </summary>
    public int Cores { get; set; }

    /// <summary>
    /// Número de hilos (threads).
    /// </summary>
    public int Threads { get; set; }

    /// <summary>
    /// Velocidad base en GHz.
    /// </summary>
    public double BaseSpeedGHz { get; set; }

    /// <summary>
    /// Uso actual porcentaje (0-100).
    /// </summary>
    public double UsagePercent { get; set; }

    /// <summary>
    /// Temperatura actual en °C (si disponible).
    /// </summary>
    public double? TemperatureCelsius { get; set; }
}

/// <summary>
/// Módulo de memoria física instalado (SMBIOS/WMI).
/// </summary>
public class MemoryModuleInfo
{
    /// <summary>
    /// Etiqueta del socket o ranura (ej: "DIMM 0").
    /// </summary>
    public string DeviceLocator { get; set; } = string.Empty;

    /// <summary>
    /// Banco físico donde se ubica (ej: "BANK 0").
    /// </summary>
    public string BankLabel { get; set; } = string.Empty;

    /// <summary>
    /// Fabricante según SMBIOS. Nunca se inventa ni normaliza.
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Part number del módulo.
    /// </summary>
    public string PartNumber { get; set; } = string.Empty;

    /// <summary>
    /// Número de serie del módulo.
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Capacidad en bytes. Null si no informada. Nunca se convierte null en 0.
    /// </summary>
    public ulong? CapacityBytes { get; set; }

    /// <summary>
    /// Velocidad configurada en MHz (ConfiguredClockSpeed). Null si 0 o ausente.
    /// </summary>
    public uint? ConfiguredClockSpeedMHz { get; set; }

    /// <summary>
    /// Código SMBIOS raw del tipo de memoria (SMBIOSMemoryType). Se conserva siempre.
    /// </summary>
    public uint? SMBIOSMemoryTypeCode { get; set; }

    /// <summary>
    /// Tipo de memoria legible ("DDR4", "LPDDR5", "Desconocida").
    /// </summary>
    public string MemoryType { get; set; } = string.Empty;

    /// <summary>
    /// Ancho de datos en bits (DataWidth). Null si no disponible.
    /// </summary>
    public ushort? DataWidthBits { get; set; }

    /// <summary>
    /// Ancho total en bits incluyendo bits de corrección (TotalWidth). Null si no disponible.
    /// </summary>
    public ushort? TotalWidthBits { get; set; }

    /// <summary>
    /// Rank del módulo (Attributes SMBIOS). Null si 0 o no disponible.
    /// </summary>
    public uint? Rank { get; set; }

    /// <summary>
    /// Capacidad en GB calculada desde CapacityBytes (1024^3).
    /// </summary>
    public double? CapacityGB => CapacityBytes.HasValue
        ? (double)CapacityBytes.Value / (1024 * 1024 * 1024)
        : null;
}

/// <summary>
/// Información de la memoria RAM.
/// </summary>
public class MemoryInfo
{
    /// <summary>
    /// Memoria total en GB.
    /// </summary>
    public double TotalGB { get; set; }

    /// <summary>
    /// Memoria disponible en GB.
    /// </summary>
    public double AvailableGB { get; set; }

    /// <summary>
    /// Memoria en uso en GB.
    /// </summary>
    public double UsedGB => TotalGB - AvailableGB;

    /// <summary>
    /// Porcentaje de uso.
    /// </summary>
    public double UsagePercent => TotalGB > 0 ? (UsedGB / TotalGB) * 100 : 0;

    /// <summary>
    /// Velocidad resumen en MHz: valor único si todos los módulos válidos coinciden, 0 en otro caso.
    /// </summary>
    public int SpeedMHz { get; set; }

    /// <summary>
    /// Tipo de memoria resumen: uniforme, "Mixta" o "Desconocida".
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Número de slots ocupados por módulos con capacidad válida.
    /// </summary>
    public int SlotsUsed { get; set; }

    /// <summary>
    /// Número total de slots (suma de arrays de memoria de sistema).
    /// </summary>
    public int SlotsTotal { get; set; }

    /// <summary>
    /// Módulos de memoria física detectados.
    /// </summary>
    public List<MemoryModuleInfo> Modules { get; set; } = new();

    /// <summary>
    /// Si existe inventario de módulos con detalles.
    /// </summary>
    public bool HasModuleDetails => Modules.Count > 0;
}

/// <summary>
/// Información de la GPU.
/// </summary>
public class GpuInfo
{
    /// <summary>
    /// Nombre de la GPU (ej: NVIDIA GeForce RTX 4070).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fabricante (NVIDIA, AMD, Intel).
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Memoria dedicada en GB.
    /// </summary>
    public double MemoryGB { get; set; }

    /// <summary>
    /// Tipo de memoria (GDDR6, GDDR6X, HBM2).
    /// </summary>
    public string MemoryType { get; set; } = string.Empty;

    /// <summary>
    /// Temperatura actual en °C (si disponible).
    /// </summary>
    public double? TemperatureCelsius { get; set; }

    /// <summary>
    /// Uso actual porcentaje (0-100, si disponible).
    /// </summary>
    public double? UsagePercent { get; set; }
}

/// <summary>
/// Información de un disco duro.
/// </summary>
public class DiskInfo
{
    /// <summary>
    /// Nombre del disco (ej: Samsung SSD 980 PRO).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Letra de unidad (ej: C:).
    /// </summary>
    public string DriveLetter { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de disco (HDD, SSD, NVMe).
    /// </summary>
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// Interfaz (SATA, NVMe, USB).
    /// </summary>
    public string Interface { get; set; } = string.Empty;

    /// <summary>
    /// Capacidad total en GB.
    /// </summary>
    public double TotalGB { get; set; }

    /// <summary>
    /// Espacio libre en GB.
    /// </summary>
    public double FreeGB { get; set; }

    /// <summary>
    /// Espacio usado en GB.
    /// </summary>
    public double UsedGB => TotalGB - FreeGB;

    /// <summary>
    /// Porcentaje de uso.
    /// </summary>
    public double UsagePercent => TotalGB > 0 ? (UsedGB / TotalGB) * 100 : 0;

    /// <summary>
    /// Estado de salud SMART (si disponible).
    /// </summary>
    public string? HealthStatus { get; set; }

    /// <summary>
    /// Temperatura del disco en °C (si disponible).
    /// </summary>
    public double? TemperatureCelsius { get; set; }
}

/// <summary>
/// Información de la placa madre.
/// </summary>
public class MotherboardInfo
{
    /// <summary>
    /// Fabricante (ej: ASUS, MSI, Gigabyte).
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Modelo de la placa madre.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Versión del BIOS.
    /// </summary>
    public string BiosVersion { get; set; } = string.Empty;

    /// <summary>
    /// Fecha del BIOS.
    /// </summary>
    public DateTime? BiosDate { get; set; }
}

/// <summary>
/// Información completa del hardware del equipo.
/// </summary>
public class HardwareReport
{
    /// <summary>
    /// Fecha y hora del reporte.
    /// </summary>
    public DateTime ReportDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Información del sistema operativo.
    /// </summary>
    public SystemInfo System { get; set; } = new();

    /// <summary>
    /// Información de la CPU.
    /// </summary>
    public CpuInfo Cpu { get; set; } = new();

    /// <summary>
    /// Información de la memoria RAM.
    /// </summary>
    public MemoryInfo Memory { get; set; } = new();

    /// <summary>
    /// Lista de GPUs encontradas.
    /// </summary>
    public List<GpuInfo> Gpus { get; set; } = new();

    /// <summary>
    /// Lista de discos encontrados.
    /// </summary>
    public List<DiskInfo> Disks { get; set; } = new();

    /// <summary>
    /// Información de la placa madre.
    /// </summary>
    public MotherboardInfo Motherboard { get; set; } = new();
}
