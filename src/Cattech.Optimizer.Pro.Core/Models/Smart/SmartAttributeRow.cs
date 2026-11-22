using System.Globalization;

namespace Cattech.Optimizer.Pro.Core.Models.Smart;

/// <summary>
/// Fila de presentación de un atributo SMART crudo (ATA o NVMe) para la UI técnica.
///
/// Solo deriva texto de datos YA parseados por <c>SmartctlParser</c>: no inventa
/// valores, no completa atributos ausentes y no evalúa salud. Los campos ATA
/// (id/nombre/valor/peor/umbral/raw) provienen de <see cref="SmartDiskReport.ImportantAttributes"/>;
/// los contadores NVMe críticos provienen de los campos estructurados del reporte.
/// </summary>
public sealed class SmartAttributeRow
{
    private const string NotAvailable = "-";

    /// <summary>ID del atributo ATA o "NVMe" para los campos estructurados NVMe.</summary>
    public string IdDisplay { get; init; } = NotAvailable;

    /// <summary>Nombre técnico del atributo.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Valor normalizado (ATA). "-" si no aplica.</summary>
    public string ValueDisplay { get; init; } = NotAvailable;

    /// <summary>Peor valor registrado (ATA). "-" si no aplica.</summary>
    public string WorstDisplay { get; init; } = NotAvailable;

    /// <summary>Umbral de fallo sobre el valor normalizado (ATA). "-" si no aplica.</summary>
    public string ThresholdDisplay { get; init; } = NotAvailable;

    /// <summary>Valor crudo tal como lo expuso smartctl ("-"/texto legible).</summary>
    public string RawDisplay { get; init; } = NotAvailable;

    /// <summary>Flags del atributo ATA (vacío si el parser no los expuso).</summary>
    public string Flags { get; init; } = string.Empty;

    /// <summary>Descripción breve del atributo (vacía si no hay).</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Si el atributo ATA es pre-failure según la estructura de flags.</summary>
    public bool IsPrefailure { get; init; }

    /// <summary>Severidad calculada por la política existente (solo presentación).</summary>
    public SmartSeverity Severity { get; init; } = SmartSeverity.Unknown;

    /// <summary>Origen del dato: "ATA" o "NVMe".</summary>
    public string Source { get; init; } = "ATA";

    /// <summary>Severidad en español para la grilla técnica.</summary>
    public string SeverityDisplay => Severity switch
    {
        SmartSeverity.Info => "Info",
        SmartSeverity.Warning => "Advertencia",
        SmartSeverity.Critical => "Crítico",
        _ => "Desconocida"
    };

    /// <summary>Texto de tooltip con descripción y flags (sin datos inventados).</summary>
    public string ToolTipText => string.IsNullOrWhiteSpace(Flags)
        ? Description
        : string.IsNullOrWhiteSpace(Description)
            ? $"Flags: {Flags}"
            : $"{Description} | Flags: {Flags}";

    /// <summary>
    /// Proyecta un <see cref="SmartAttribute"/> ATA a la fila de la grilla técnica.
    /// </summary>
    public static SmartAttributeRow FromAttribute(SmartAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        return new SmartAttributeRow
        {
            IdDisplay = attribute.Id.ToString(CultureInfo.InvariantCulture),
            Name = attribute.Name,
            ValueDisplay = attribute.Value.ToString(CultureInfo.InvariantCulture),
            WorstDisplay = attribute.Worst.ToString(CultureInfo.InvariantCulture),
            ThresholdDisplay = attribute.Threshold.ToString(CultureInfo.InvariantCulture),
            RawDisplay = attribute.RawValue.ToString(CultureInfo.InvariantCulture),
            Flags = attribute.Flags,
            Description = attribute.Description,
            IsPrefailure = attribute.IsPrefailure,
            Severity = attribute.Severity,
            Source = "ATA"
        };
    }

    /// <summary>
    /// Proyecta los atributos disponibles de un disco a filas de la grilla técnica:
    /// atributos ATA parseados primero y, si existen, los contadores NVMe
    /// estructurados (críticos) al final. Un reporte sin datos devuelve una lista
    /// vacía (la UI informa "sin atributos"; no se completa con valores falsos).
    /// </summary>
    public static IReadOnlyList<SmartAttributeRow> FromReport(SmartDiskReport? report)
    {
        var rows = new List<SmartAttributeRow>();
        if (report is null)
            return rows;

        foreach (var attribute in report.ImportantAttributes)
            rows.Add(FromAttribute(attribute));

        rows.AddRange(FromNvmeReport(report));
        return rows;
    }

    /// <summary>
    /// Proyecta los contadores NVMe estructurados del reporte. Solo se agregan
    /// los campos realmente informados (null = smartctl no lo expuso).
    /// </summary>
    public static IReadOnlyList<SmartAttributeRow> FromNvmeReport(SmartDiskReport? report)
    {
        var rows = new List<SmartAttributeRow>();
        if (report is null)
            return rows;

        if (report.NvmePercentageUsed.HasValue)
        {
            rows.Add(NvmeRow(
                "percentage_used",
                FormatPercent(report.NvmePercentageUsed.Value),
                "Porcentaje de vida útil usado (nvme percentage_used)"));
        }

        if (report.NvmeAvailableSpare.HasValue)
        {
            rows.Add(NvmeRow(
                "available_spare",
                FormatPercent(report.NvmeAvailableSpare.Value),
                "Espacio de repuesto disponible (nvme available_spare)"));
        }

        if (report.NvmeAvailableSpareThreshold.HasValue)
        {
            rows.Add(NvmeRow(
                "available_spare_threshold",
                FormatPercent(report.NvmeAvailableSpareThreshold.Value),
                "Umbral de espacio de repuesto (nvme available_spare_threshold)"));
        }

        if (report.NvmeCriticalWarning.HasValue)
        {
            rows.Add(NvmeRow(
                "critical_warning",
                FormatCriticalWarning(report.NvmeCriticalWarning.Value),
                "Bitmask de advertencias críticas NVMe (0x00 = sin advertencias)",
                report.NvmeCriticalWarning.Value != 0 ? SmartSeverity.Critical : SmartSeverity.Info));
        }

        if (report.NvmeMediaErrors.HasValue)
        {
            rows.Add(NvmeRow(
                "media_errors",
                report.NvmeMediaErrors.Value.ToString(CultureInfo.InvariantCulture),
                "Errores de medios NVMe (nvme media_errors)"));
        }

        if (report.NvmeUnsafeShutdowns.HasValue)
        {
            rows.Add(NvmeRow(
                "unsafe_shutdowns",
                report.NvmeUnsafeShutdowns.Value.ToString(CultureInfo.InvariantCulture),
                "Apagados inseguros NVMe (nvme unsafe_shutdowns)"));
        }

        return rows;
    }

    private static SmartAttributeRow NvmeRow(
        string name,
        string rawDisplay,
        string description,
        SmartSeverity severity = SmartSeverity.Info) => new()
        {
            IdDisplay = "NVMe",
            Name = name,
            ValueDisplay = NotAvailable,
            WorstDisplay = NotAvailable,
            ThresholdDisplay = NotAvailable,
            RawDisplay = rawDisplay,
            Description = description,
            Severity = severity,
            Source = "NVMe"
        };

    private static string FormatPercent(int value) =>
        $"{value.ToString(CultureInfo.InvariantCulture)} %";

    private static string FormatCriticalWarning(int value) =>
        "0x" + value.ToString("X2", CultureInfo.InvariantCulture);
}
