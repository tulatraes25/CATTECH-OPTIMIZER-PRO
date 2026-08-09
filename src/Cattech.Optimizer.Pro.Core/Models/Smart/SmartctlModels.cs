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
/// Bitmask de exit status de smartctl (spec smartmontools).
/// smartctl no retorna códigos enum simples: combina bits de 8 bits.
/// </summary>
[Flags]
public enum SmartctlExitFlags
{
    /// <summary>Sin bits activos.</summary>
    None = 0,

    /// <summary>Bit 0: error de línea de comando o interno (1).</summary>
    CommandLineOrInternalError = 1,

    /// <summary>Bit 1: fallo al abrir el dispositivo o fallo de identidad (2).</summary>
    DeviceOpenOrIdentityFailed = 2,

    /// <summary>Bit 2: error de comando SMART o de checksum (4).</summary>
    SmartCommandOrChecksumError = 4,

    /// <summary>Bit 3: fallo del self-assessment de salud SMART (8).</summary>
    SmartStatusFailed = 8,

    /// <summary>Bit 4: atributo por debajo del umbral (pre-fail) (16).</summary>
    PrefailAttributeThreshold = 16,

    /// <summary>Bit 5: fallo de atributo pasada la vida útil (32).</summary>
    PastOrUsageAttributeFailure = 32,

    /// <summary>Bit 6: el error log contiene errores (64).</summary>
    ErrorLogContainsErrors = 64,

    /// <summary>Bit 7: el self-test log contiene errores (128).</summary>
    SelfTestLogContainsErrors = 128
}

/// <summary>
/// Resultado de la ejecución de un comando smartctl.
/// </summary>
public class SmartctlCommandResult
{
    /// <summary>
    /// Código de salida del proceso. Fuente primaria del resultado.
    /// -1 indica que el proceso no llegó a ejecutarse (smartctl no encontrado,
    /// excepción de Process) — NO es un bitmask smartctl válido.
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
    /// Exit status interpretado como bitmask smartctl.
    /// ExitCode &lt; 0 (proceso no ejecutado) NO se convierte a bits: queda None.
    /// </summary>
    public SmartctlExitFlags ExitFlags =>
        ExitCode >= 0 ? (SmartctlExitFlags)ExitCode : SmartctlExitFlags.None;

    /// <summary>
    /// Fallo operativo de invocación: timeout, proceso no ejecutado (ExitCode &lt; 0),
    /// o bits 0-1 (línea de comando / apertura del dispositivo).
    /// </summary>
    public bool HasInvocationFailure =>
        TimedOut || ExitCode < 0 || ExitFlags.HasFlag(SmartctlExitFlags.CommandLineOrInternalError) ||
        ExitFlags.HasFlag(SmartctlExitFlags.DeviceOpenOrIdentityFailed);

    /// <summary>
    /// Bit 2: error de comando SMART o checksum.
    /// </summary>
    public bool HasSmartCommandFailure =>
        ExitFlags.HasFlag(SmartctlExitFlags.SmartCommandOrChecksumError);

    /// <summary>
    /// Bits 3-7: hallazgos de salud/log. NO son fallo de proceso: el JSON
    /// puede ser utilizable (ej: exit 128 con self-test log parseable).
    /// </summary>
    public bool HasHealthOrLogFindings =>
        ExitFlags.HasFlag(SmartctlExitFlags.SmartStatusFailed) ||
        ExitFlags.HasFlag(SmartctlExitFlags.PrefailAttributeThreshold) ||
        ExitFlags.HasFlag(SmartctlExitFlags.PastOrUsageAttributeFailure) ||
        ExitFlags.HasFlag(SmartctlExitFlags.ErrorLogContainsErrors) ||
        ExitFlags.HasFlag(SmartctlExitFlags.SelfTestLogContainsErrors);

    /// <summary>
    /// El comando no tuvo bits operativos 0-2 ni timeout.
    /// NO significa "disco sano" ni "sin hallazgos SMART":
    /// los bits 3-7 (hallazgos) pueden estar activos.
    /// Los servicios aplican políticas específicas por comando.
    /// </summary>
    public bool IsSuccess => !HasInvocationFailure && !HasSmartCommandFailure;
}
