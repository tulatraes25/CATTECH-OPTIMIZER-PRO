using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// Opciones para la generación de un informe técnico.
/// </summary>
public class ReportGenerationOptions
{
    /// <summary>
    /// Configuración de empresa/técnico.
    /// </summary>
    public AppSettings? Settings { get; set; }

    /// <summary>
    /// Datos del cliente/equipo.
    /// </summary>
    public ServiceReport? ServiceReport { get; set; }

    /// <summary>
    /// Resultado del diagnóstico rápido.
    /// </summary>
    public DiagnosticReport? DiagnosticReport { get; set; }

    /// <summary>
    /// Último análisis de programas de inicio.
    /// </summary>
    public StartupAnalysis? StartupAnalysis { get; set; }

    /// <summary>
    /// Último resultado de limpieza de temporales.
    /// </summary>
    public TempCleanupResult? CleanupResult { get; set; }

    /// <summary>
    /// Último resultado de optimización visual.
    /// </summary>
    public VisualOptimizationResult? VisualOptimizationResult { get; set; }

    /// <summary>
    /// Último resultado de punto de restauración.
    /// </summary>
    public RestorePointResult? RestorePointResult { get; set; }

    /// <summary>
    /// Observaciones finales del técnico.
    /// </summary>
    public string FinalObservations { get; set; } = string.Empty;

    /// <summary>
    /// Incluir sección de empresa/técnico.
    /// </summary>
    public bool IncludeCompany { get; set; } = true;

    /// <summary>
    /// Incluir sección de cliente/equipo.
    /// </summary>
    public bool IncludeClient { get; set; } = true;

    /// <summary>
    /// Incluir sección de diagnóstico.
    /// </summary>
    public bool IncludeDiagnostic { get; set; } = true;

    /// <summary>
    /// Incluir sección de programas de inicio.
    /// </summary>
    public bool IncludeStartup { get; set; } = true;

    /// <summary>
    /// Incluir sección de limpieza.
    /// </summary>
    public bool IncludeCleanup { get; set; } = true;

    /// <summary>
    /// Incluir sección de optimización visual.
    /// </summary>
    public bool IncludeVisualOptimization { get; set; } = true;

    /// <summary>
    /// Incluir sección de punto de restauración.
    /// </summary>
    public bool IncludeRestorePoint { get; set; } = true;

    /// <summary>
    /// Incluir recomendaciones automáticas.
    /// </summary>
    public bool IncludeRecommendations { get; set; } = true;

    /// <summary>
    /// Nombre del archivo de salida (sin extensión).
    /// </summary>
    public string OutputFileName { get; set; } = string.Empty;
}

/// <summary>
/// Información de un informe generado.
/// </summary>
public class GeneratedReportInfo
{
    /// <summary>
    /// ID único del informe.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Fecha/hora de generación.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Nombre del cliente.
    /// </summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del equipo.
    /// </summary>
    public string EquipmentName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta del archivo HTML generado.
    /// </summary>
    public string HtmlPath { get; set; } = string.Empty;

    /// <summary>
    /// Ruta del archivo PDF generado.
    /// </summary>
    public string PdfPath { get; set; } = string.Empty;

    /// <summary>
    /// Secciones incluidas.
    /// </summary>
    public List<string> IncludedSections { get; set; } = new();

    /// <summary>
    /// Notas adicionales.
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}
