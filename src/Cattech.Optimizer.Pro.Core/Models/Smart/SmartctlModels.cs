namespace Cattech.Optimizer.Pro.Core.Models.Smart;

/// <summary>
/// Estado de disponibilidad de smartctl en el sistema.
/// </summary>
public class SmartctlAvailability
{
    /// <summary>
    /// Si smartctl está disponible y accesible.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Ruta completa al ejecutable smartctl.
    /// </summary>
    public string SmartctlPath { get; set; } = string.Empty;

    /// <summary>
    /// Versión de smartctl detectada.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Si smartctl soporta salida JSON (-j).
    /// </summary>
    public bool SupportsJson { get; set; }

    /// <summary>
    /// Mensaje de error si no está disponible.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Fecha/hora de la verificación.
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Dispositivo de almacenamiento detectado por smartctl.
/// </summary>
public class SmartDiskDevice
{
    /// <summary>
    /// Nombre del dispositivo (ej: /dev/sda, /dev/nvme0n1).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Nombre para mostrar (ej: /dev/sda [SAT], /dev/nvme0n1).
    /// </summary>
    public string InfoName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de dispositivo (ej: scsi, nvme).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Protocolo (ej: SATA, SAS, NVMe).
    /// </summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del modelo (ej: Samsung SSD 980 PRO).
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Número de serie.
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>
    /// Si el dispositivo está disponible para diagnóstico.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Notas adicionales sobre el dispositivo.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>
    /// Tipo aproximado de disco (HDD, SSD, NVMe, USB).
    /// Se detecta por protocolo/modelo.
    /// </summary>
    public string ApproximateDiskType { get; set; } = string.Empty;
}

/// <summary>
/// Resultado de la ejecución de un comando smartctl.
/// </summary>
public class SmartctlCommandResult
{
    /// <summary>
    /// Código de salida del proceso.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Salida estándar del proceso.
    /// </summary>
    public string StandardOutput { get; set; } = string.Empty;

    /// <summary>
    /// Salida de error del proceso.
    /// </summary>
    public string StandardError { get; set; } = string.Empty;

    /// <summary>
    /// Si el proceso excedió el timeout.
    /// </summary>
    public bool TimedOut { get; set; }

    /// <summary>
    /// Duración de la ejecución en milisegundos.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Si la ejecución fue exitosa (exit code 0 o 1 para smartctl).
    /// smartctl retorna 0 si no hay errores, 1 si hay errores SMART.
    /// </summary>
    public bool IsSuccess => ExitCode == 0 || ExitCode == 1;
}
