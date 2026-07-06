namespace Cattech.Optimizer.Pro.Core.Models.Startup;

/// <summary>
/// Tipo de origen de una entrada de inicio.
/// </summary>
public enum StartupSourceType
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
    StartupFolderCommon,
    ScheduledTask
}

/// <summary>
/// Nivel de riesgo estimado de una entrada de inicio.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Unknown
}

/// <summary>
/// Recomendación para una entrada de inicio.
/// </summary>
public enum StartupRecommendation
{
    Keep,
    Review,
    PossibleDisable
}

/// <summary>
/// Estado de una entrada de inicio.
/// </summary>
public enum StartupEntryStatus
{
    Active,
    Inactive,
    PathNotFound,
    Unknown
}

/// <summary>
/// Entrada individual de programa de inicio.
/// Preparada para futura desactivación con backup y reversión.
/// </summary>
public class StartupEntry
{
    /// <summary>
    /// ID único de la entrada.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Nombre de la entrada.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Comando o ruta completa.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Ubicación exacta en registro o carpeta.
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de origen.
    /// </summary>
    public StartupSourceType SourceType { get; set; }

    /// <summary>
    /// Editor o compañía detectado (si es posible).
    /// </summary>
    public string Publisher { get; set; } = string.Empty;

    /// <summary>
    /// Si probablemente es de Microsoft.
    /// </summary>
    public bool IsMicrosoft { get; set; }

    /// <summary>
    /// Estado de la entrada.
    /// </summary>
    public StartupEntryStatus Status { get; set; }

    /// <summary>
    /// Nivel de riesgo estimado.
    /// </summary>
    public RiskLevel Risk { get; set; }

    /// <summary>
    /// Recomendación.
    /// </summary>
    public StartupRecommendation Recommendation { get; set; }

    /// <summary>
    /// Notas adicionales (alertas específicas).
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    // --- Campos preparados para futura desactivación ---

    /// <summary>
    /// Valor original del registro (para reversión).
    /// </summary>
    public string? OriginalRegistryValue { get; set; }

    /// <summary>
    /// Ruta original del archivo en carpeta de inicio (para reversión).
    /// </summary>
    public string? OriginalFilePath { get; set; }

    /// <summary>
    /// Fecha de primera detección.
    /// </summary>
    public DateTime FirstDetected { get; set; } = DateTime.Now;
}

/// <summary>
/// Análisis completo de programas de inicio.
/// Se persiste en data/startup-analysis/startup-analysis-YYYYMMDD-HHMMSS.json
/// </summary>
public class StartupAnalysis
{
    /// <summary>
    /// ID único del análisis.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha y hora del análisis.
    /// </summary>
    public DateTime AnalysisDate { get; set; } = DateTime.Now;

    /// <summary>
    /// Lista de todas las entradas encontradas.
    /// </summary>
    public List<StartupEntry> Entries { get; set; } = new();

    /// <summary>
    /// Total de entradas encontradas.
    /// </summary>
    public int TotalCount => Entries.Count;

    /// <summary>
    /// Entradas de Microsoft o probables del sistema.
    /// </summary>
    public int MicrosoftCount => Entries.Count(e => e.IsMicrosoft);

    /// <summary>
    /// Entradas de terceros.
    /// </summary>
    public int ThirdPartyCount => Entries.Count(e => !e.IsMicrosoft);

    /// <summary>
    /// Entradas recomendadas para posible desactivación.
    /// </summary>
    public int PossibleDisableCount => Entries.Count(e => e.Recommendation == StartupRecommendation.PossibleDisable);

    /// <summary>
    /// Entradas con alertas.
    /// </summary>
    public int AlertCount => Entries.Count(e =>
        e.Status == StartupEntryStatus.PathNotFound ||
        e.Risk == RiskLevel.High ||
        e.Notes.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
        e.Notes.Contains("AppData", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Nombre del técnico que ejecutó el análisis.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;
}
