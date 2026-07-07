using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// Motor de recomendaciones automáticas para informes.
/// </summary>
public static class ReportRecommendationEngine
{
    public static List<ReportRecommendation> GenerateRecommendations(ReportGenerationOptions options)
    {
        var recommendations = new List<ReportRecommendation>();

        // Recomendaciones basadas en diagnóstico
        if (options.DiagnosticReport != null)
        {
            recommendations.AddRange(GetDiagnosticRecommendations(options.DiagnosticReport));
        }

        // Recomendaciones basadas en programas de inicio
        if (options.StartupAnalysis != null)
        {
            recommendations.AddRange(GetStartupRecommendations(options.StartupAnalysis));
        }

        // Recomendaciones basadas en limpieza
        if (options.CleanupResult != null)
        {
            recommendations.AddRange(GetCleanupRecommendations(options.CleanupResult));
        }

        // Recomendaciones basadas en punto de restauración
        if (options.RestorePointResult != null && !options.RestorePointResult.Success)
        {
            recommendations.Add(new ReportRecommendation
            {
                Category = "Restauración",
                Message = "No se pudo crear punto de restauración. Verificar que la protección del sistema esté habilitada y ejecutar como administrador.",
                Severity = "Warning",
                Icon = "⚠️"
            });
        }

        return recommendations;
    }

    private static List<ReportRecommendation> GetDiagnosticRecommendations(DiagnosticReport diag)
    {
        var recs = new List<ReportRecommendation>();

        // RAM
        if (diag.RamTotalGB > 0 && diag.RamTotalGB <= 4)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Memoria RAM",
                Message = $"El equipo tiene {diag.RamTotalGB} GB de RAM, lo cual es bajo para uso general. Se recomienda ampliar a al menos 8 GB para mejorar rendimiento.",
                Severity = "Warning",
                Icon = "⚠️"
            });
        }
        else if (diag.RamTotalGB > 4 && diag.RamTotalGB <= 8)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Memoria RAM",
                Message = $"El equipo tiene {diag.RamTotalGB} GB de RAM, suficiente para uso básico. Para multitarea frecuente se recomienda 16 GB.",
                Severity = "Info",
                Icon = "ℹ️"
            });
        }

        // Disco
        if (diag.DiskType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
            diag.DiskType == "No detectado")
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Disco",
                Message = $"El equipo usa un disco {diag.DiskType}. Se recomienda migrar a SSD para mejorar significativamente tiempos de arranque y apertura de aplicaciones.",
                Severity = "Warning",
                Icon = "⚠️"
            });
        }

        if (diag.DiskFreePercent > 0 && diag.DiskFreePercent < 15)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Disco",
                Message = $"El espacio libre es bajo ({diag.DiskFreePercent}%). Se recomienda liberar espacio o ampliar el almacenamiento.",
                Severity = "Warning",
                Icon = "⚠️"
            });
        }

        // Inicio
        if (diag.Startup.TotalCount > 10)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Inicio",
                Message = $"Hay {diag.Startup.TotalCount} programas al inicio. Reducir el número de programas de inicio puede mejorar el tiempo de arranque.",
                Severity = "Info",
                Icon = "ℹ️"
            });
        }

        // Temporales
        if (diag.TempFiles.TotalSizeGB > 2)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Mantenimiento",
                Message = $"Se detectaron {diag.TempFiles.TotalSizeGB} GB de archivos temporales. Se recomienda limpieza periódica.",
                Severity = "Info",
                Icon = "ℹ️"
            });
        }

        // Windows
        if (!string.IsNullOrEmpty(diag.OsName) &&
            !diag.OsName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase) &&
            !diag.OsName.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Sistema",
                Message = $"El sistema operativo ({diag.OsName}) no es Windows 10/11. Algunas funciones podrían no estar disponibles.",
                Severity = "Info",
                Icon = "ℹ️"
            });
        }

        return recs;
    }

    private static List<ReportRecommendation> GetStartupRecommendations(StartupAnalysis analysis)
    {
        var recs = new List<ReportRecommendation>();

        if (analysis.ThirdPartyCount > 5)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Inicio",
                Message = $"Hay {analysis.ThirdPartyCount} programas de terceros al inicio. Se recomienda revisar y desactivar los innecesarios.",
                Severity = "Info",
                Icon = "ℹ️"
            });
        }

        return recs;
    }

    private static List<ReportRecommendation> GetCleanupRecommendations(TempCleanupResult result)
    {
        var recs = new List<ReportRecommendation>();

        if (result.DeletedMB > 500)
        {
            recs.Add(new ReportRecommendation
            {
                Category = "Mantenimiento",
                Message = $"Se liberaron {result.DeletedMB} MB. Se recomienda realizar limpiezas periódicas para mantener el sistema optimizado.",
                Severity = "Info",
                Icon = "ℹ️"
            });
        }

        return recs;
    }
}
