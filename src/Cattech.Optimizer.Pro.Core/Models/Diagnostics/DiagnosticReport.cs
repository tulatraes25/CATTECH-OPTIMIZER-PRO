namespace Cattech.Optimizer.Pro.Core.Models.Diagnostics;

/// <summary>
/// Nivel de severidad de una alerta de diagnóstico.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Una alerta generada durante el diagnóstico.
/// </summary>
public class DiagnosticAlert
{
    /// <summary>
    /// Severidad de la alerta.
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Categoría de la alerta (ej: RAM, Disco, Seguridad).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje descriptivo de la alerta.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Recomendación técnica (opcional).
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Resultado del análisis de programas de inicio.
/// </summary>
public class StartupInfo
{
    /// <summary>
    /// Total de programas al inicio.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Programas de terceros (no Microsoft) al inicio.
    /// </summary>
    public int ThirdPartyCount { get; set; }

    /// <summary>
    /// Lista de nombres de programas detectados.
    /// </summary>
    public List<string> ProgramNames { get; set; } = new();
}

/// <summary>
/// Resultado del análisis de archivos temporales.
/// </summary>
public class TempFilesInfo
{
    /// <summary>
    /// Tamaño total estimado en bytes.
    /// </summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Tamaño total en GB (redondeado).
    /// </summary>
    public double TotalSizeGB => Math.Round((double)TotalSizeBytes / (1024 * 1024 * 1024), 2);

    /// <summary>
    /// Cantidad de archivos encontrados.
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Cantidad de carpetas encontradas.
    /// </summary>
    public int FolderCount { get; set; }

    /// <summary>
    /// Detalle por ubicación.
    /// </summary>
    public List<TempLocationDetail> Locations { get; set; } = new();
}

/// <summary>
/// Detalle de temporales en una ubicación específica.
/// </summary>
public class TempLocationDetail
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public double SizeGB => Math.Round((double)SizeBytes / (1024 * 1024 * 1024), 2);
    public int FileCount { get; set; }
}

/// <summary>
/// Estado de seguridad detectado.
/// </summary>
public class SecurityInfo
{
    /// <summary>
    /// Nombre del antivirus detectado.
    /// </summary>
    public string AntivirusName { get; set; } = "No detectado";

    /// <summary>
    /// Si el antivirus parece activo.
    /// </summary>
    public bool AntivirusActive { get; set; }

    /// <summary>
    /// Estado del firewall de Windows.
    /// </summary>
    public bool FirewallActive { get; set; }

    /// <summary>
    /// Estado de Windows Update (última verificación, servicio, etc.).
    /// </summary>
    public string WindowsUpdateStatus { get; set; } = "No determinado";
}

/// <summary>
/// Estado de la memoria virtual detectado.
/// </summary>
public class VirtualMemoryInfo
{
    /// <summary>
    /// Tamaño del archivo de paginación en GB.
    /// </summary>
    public double PagingFileSizeGB { get; set; }

    /// <summary>
    /// Si está configurado automáticamente.
    /// </summary>
    public bool IsAutoManaged { get; set; }

    /// <summary>
    /// Ubicación del archivo de paginación.
    /// </summary>
    public string Location { get; set; } = string.Empty;
}

/// <summary>
/// Reporte completo de diagnóstico rápido.
/// Se persiste en data/diagnostics/diagnostic-YYYYMMDD-HHMMSS.json
/// </summary>
public class DiagnosticReport
{
    /// <summary>
    /// ID único del diagnóstico.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha y hora del diagnóstico.
    /// </summary>
    public DateTime DiagnosisDate { get; set; } = DateTime.Now;

    // --- Sistema ---

    /// <summary>
    /// Nombre del sistema operativo.
    /// </summary>
    public string OsName { get; set; } = string.Empty;

    /// <summary>
    /// Edición de Windows.
    /// </summary>
    public string WindowsEdition { get; set; } = string.Empty;

    /// <summary>
    /// Arquitectura del SO.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del equipo.
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// Usuario actual.
    /// </summary>
    public string CurrentUser { get; set; } = string.Empty;

    // --- Hardware ---

    /// <summary>
    /// Procesador detectado.
    /// </summary>
    public string Processor { get; set; } = string.Empty;

    /// <summary>
    /// Núcleos de CPU.
    /// </summary>
    public int CpuCores { get; set; }

    /// <summary>
    /// RAM total en GB.
    /// </summary>
    public double RamTotalGB { get; set; }

    /// <summary>
    /// RAM en uso en GB.
    /// </summary>
    public double RamUsedGB { get; set; }

    /// <summary>
    /// RAM disponible en GB.
    /// </summary>
    public double RamAvailableGB { get; set; }

    /// <summary>
    /// Porcentaje de uso de RAM.
    /// </summary>
    public double RamUsagePercent { get; set; }

    // --- Disco ---

    /// <summary>
    /// Nombre del disco principal.
    /// </summary>
    public string PrimaryDiskName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de disco (HDD, SSD, NVMe).
    /// </summary>
    public string DiskType { get; set; } = string.Empty;

    /// <summary>
    /// Capacidad del disco en GB.
    /// </summary>
    public double DiskCapacityGB { get; set; }

    /// <summary>
    /// Espacio libre en GB.
    /// </summary>
    public double DiskFreeGB { get; set; }

    /// <summary>
    /// Porcentaje de espacio libre.
    /// </summary>
    public double DiskFreePercent { get; set; }

    // --- Programas de inicio ---

    /// <summary>
    /// Información de programas de inicio.
    /// </summary>
    public StartupInfo Startup { get; set; } = new();

    // --- Temporales ---

    /// <summary>
    /// Información de archivos temporales.
    /// </summary>
    public TempFilesInfo TempFiles { get; set; } = new();

    // --- Seguridad ---

    /// <summary>
    /// Estado de seguridad detectado.
    /// </summary>
    public SecurityInfo Security { get; set; } = new();

    // --- Memoria virtual ---

    /// <summary>
    /// Estado de la memoria virtual.
    /// </summary>
    public VirtualMemoryInfo VirtualMemory { get; set; } = new();

    // --- Alertas ---

    /// <summary>
    /// Lista de alertas generadas durante el diagnóstico.
    /// </summary>
    public List<DiagnosticAlert> Alerts { get; set; } = new();

    /// <summary>
    /// Nombre del técnico que ejecutó el diagnóstico.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;
}
