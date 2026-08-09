using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Infrastructure.Smart;

/// <summary>
/// Política CATTECH de salud SMART.
/// Separa la interpretación (aquí) de la extracción de datos (SmartctlParser).
/// Distingue señales del estándar/proveedor de políticas conservadoras CATTECH.
/// </summary>
internal static class SmartHealthPolicy
{
    // =====================
    // Políticas CATTECH (conservadoras; NO son umbrales universales del estándar SMART)
    // =====================

    public const int ReallocatedCriticalCount = 10;
    public const int PendingCriticalCount = 5;
    public const int OfflineUncorrectableCriticalCount = 5;

    public const int DiskTemperatureWarningCelsius = 55;
    public const int DiskTemperatureCriticalCelsius = 65;

    public const int NvmePercentageUsedWarning = 80;

    /// <summary>
    /// Evalúa la severidad de un atributo SMART individual.
    /// Prioridad: reglas del estándar (when_failed / VALUE vs THRESH / prefailure)
    /// y luego política CATTECH por ID. Nunca compara RawValue contra THRESH.
    /// </summary>
    public static SmartSeverity EvaluateAttributeSeverity(SmartAttribute attribute)
    {
        // --- Reglas del estándar / smartctl (fuente principal) ---

        var whenFailed = attribute.WhenFailed?.Trim();
        if (!string.IsNullOrEmpty(whenFailed))
        {
            if (string.Equals(whenFailed, "now", StringComparison.OrdinalIgnoreCase))
            {
                return attribute.IsPrefailure ? SmartSeverity.Critical : SmartSeverity.Warning;
            }

            if (string.Equals(whenFailed, "past", StringComparison.OrdinalIgnoreCase))
            {
                return SmartSeverity.Warning;
            }
        }

        // Fallback normalizado: VALUE <= THRESH (THRESH aplica al valor normalizado)
        if (attribute.Threshold > 0)
        {
            if (attribute.Value <= attribute.Threshold)
            {
                return attribute.IsPrefailure ? SmartSeverity.Critical : SmartSeverity.Warning;
            }

            // Fallo histórico (worst bajo, valor recuperado): Warning como máximo, nunca Critical actual
            if (attribute.Worst <= attribute.Threshold)
            {
                return SmartSeverity.Warning;
            }
        }

        // --- Política CATTECH por ID (crítico primero) ---

        return attribute.Id switch
        {
            5 when attribute.RawValue > ReallocatedCriticalCount => SmartSeverity.Critical,
            5 when attribute.RawValue > 0 => SmartSeverity.Warning,

            197 when attribute.RawValue > PendingCriticalCount => SmartSeverity.Critical,
            197 when attribute.RawValue > 0 => SmartSeverity.Warning,

            198 when attribute.RawValue > OfflineUncorrectableCriticalCount => SmartSeverity.Critical,
            198 when attribute.RawValue > 0 => SmartSeverity.Warning,

            // CRC ID199: errores de interfaz. Nunca Critical solo por contador raw.
            199 when attribute.RawValue > 0 => SmartSeverity.Warning,

            // 187 Reported Uncorrectable / 188 Command Timeout: warning por conteo
            187 when attribute.RawValue > 0 => SmartSeverity.Warning,
            188 when attribute.RawValue > 0 => SmartSeverity.Warning,

            // IDs 1/3/9/12 informativos; SSD vendor-specific (173/175/176/177/231/233)
            // sin thresholds raw universales: Info salvo reglas del estándar arriba.
            _ => SmartSeverity.Info
        };
    }

    /// <summary>
    /// Evalúa la salud general del disco con la precedencia completa.
    /// Precedencia: CRÍTICO → WARNING → GOOD (solo con evidencia positiva) → UNKNOWN.
    /// No usa report.Errors como señal de salud: distingue hallazgos de errores técnicos.
    /// </summary>
    public static void EvaluateOverallHealth(SmartDiskReport report, SmartctlExitFlags? exitFlags)
    {
        var flags = exitFlags ?? SmartctlExitFlags.None;
        var criticalReasons = new List<string>();
        var warningReasons = new List<string>();

        // =====================
        // CRÍTICO — señales del estándar/proveedor
        // =====================

        if (report.OverallHealthPassed == false)
        {
            AddOnce(criticalReasons, "SMART self-assessment reportó fallo.");
            report.RequiresBackupRecommendation = true;
        }

        if (flags.HasFlag(SmartctlExitFlags.SmartStatusFailed))
        {
            AddOnce(criticalReasons, "smartctl informó fallo del self-assessment (exit bit 3).");
            report.RequiresBackupRecommendation = true;
        }

        if (flags.HasFlag(SmartctlExitFlags.PrefailAttributeThreshold))
        {
            AddOnce(criticalReasons, "smartctl informó atributo pre-fail bajo umbral (exit bit 4).");
            report.RequiresBackupRecommendation = true;
        }

        if (report.NvmeCriticalWarning is > 0)
        {
            AddOnce(criticalReasons, $"NVMe critical_warning: {report.NvmeCriticalWarning}");
            report.RequiresBackupRecommendation = true;
        }

        if (report.NvmeMediaErrors is > 0)
        {
            AddOnce(criticalReasons, $"NVMe media errors: {report.NvmeMediaErrors}");
            report.RequiresBackupRecommendation = true;
        }

        // Atributos críticos (reglas oficiales + política CATTECH)
        foreach (var attr in report.ImportantAttributes)
        {
            if (attr.Severity == SmartSeverity.Critical)
            {
                AddOnce(criticalReasons, $"Atributo crítico: {attr.Name} (ID {attr.Id})");
                report.RequiresBackupRecommendation = true;
            }
        }

        // Temperatura crítica (política CATTECH)
        if (report.TemperatureCelsius > DiskTemperatureCriticalCelsius)
        {
            AddOnce(criticalReasons, $"Temperatura crítica: {report.TemperatureCelsius} °C");
            report.RequiresBackupRecommendation = true;
        }

        if (criticalReasons.Count > 0)
        {
            report.HealthStatus = SmartHealthStatus.Critical;
            report.HealthSummary = string.Join(" ", criticalReasons);
            AddRangeOnce(report.Errors, criticalReasons);

            // Inconsistencia conservadora: passed=true con critical_warning activo
            if (report.OverallHealthPassed == true && report.NvmeCriticalWarning is > 0)
            {
                AddOnce(report.Warnings,
                    "Discrepancia: smart_status passed=true pero NVMe critical_warning está activo.");
            }

            return;
        }

        // =====================
        // WARNING — señales del estándar/proveedor + políticas CATTECH
        // =====================

        if (flags.HasFlag(SmartctlExitFlags.PastOrUsageAttributeFailure))
        {
            AddOnce(warningReasons, "smartctl informó fallo de atributo pasado su vida útil (exit bit 5).");
        }

        if (flags.HasFlag(SmartctlExitFlags.ErrorLogContainsErrors))
        {
            AddOnce(warningReasons, "smartctl informó errores en el error log (exit bit 6).");
        }

        if (flags.HasFlag(SmartctlExitFlags.SelfTestLogContainsErrors))
        {
            AddOnce(warningReasons, "smartctl informó errores en el self-test log (exit bit 7).");
        }

        if (report.NvmePercentageUsed.HasValue && report.NvmePercentageUsed.Value >= NvmePercentageUsedWarning)
        {
            AddOnce(warningReasons, $"NVMe vida útil usada: {report.NvmePercentageUsed.Value}%");
        }

        if (report.NvmeAvailableSpare.HasValue &&
            report.NvmeAvailableSpareThreshold is > 0 &&
            report.NvmeAvailableSpare.Value <= report.NvmeAvailableSpareThreshold.Value)
        {
            AddOnce(warningReasons,
                $"NVMe espacio de repuesto bajo: {report.NvmeAvailableSpare.Value}% (umbral: {report.NvmeAvailableSpareThreshold.Value}%)");
        }

        foreach (var attr in report.ImportantAttributes)
        {
            if (attr.Severity == SmartSeverity.Warning)
            {
                AddOnce(warningReasons, $"Atributo a revisar: {attr.Name} (ID {attr.Id})");
            }
        }

        if (report.TemperatureCelsius > DiskTemperatureWarningCelsius)
        {
            AddOnce(warningReasons, $"Temperatura alta: {report.TemperatureCelsius} °C");
        }

        if (warningReasons.Count > 0)
        {
            report.HealthStatus = SmartHealthStatus.Warning;
            report.HealthSummary = string.Join(" ", warningReasons);
            AddRangeOnce(report.Warnings, warningReasons);
            return;
        }

        // =====================
        // GOOD requiere evidencia positiva
        // =====================

        if (report.OverallHealthPassed == true)
        {
            report.HealthStatus = SmartHealthStatus.Good;
            report.HealthSummary = "Salud general: Buena. Sin atributos críticos ni advertencias.";
            return;
        }

        // Sin evidencia positiva: no concluyente
        report.HealthStatus = SmartHealthStatus.Unknown;
        report.HealthSummary = "Estado SMART no concluyente: no se informó self-assessment general.";
    }

    private static void AddOnce(List<string> list, string message)
    {
        if (!list.Contains(message, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(message);
        }
    }

    private static void AddRangeOnce(List<string> target, IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            AddOnce(target, message);
        }
    }
}
