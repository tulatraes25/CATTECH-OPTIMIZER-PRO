using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Startup;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;
using Cattech.Optimizer.Pro.Core.Models.Smart;

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

        // Recomendaciones basadas en análisis SMART (solo si la sección fue incluida)
        if (options.IncludeSmart && options.SmartAnalysis != null)
        {
            recommendations.AddRange(GetSmartRecommendations(options.SmartAnalysis));
        }

        // Recomendaciones basadas en self-tests SMART (solo si la sección fue incluida)
        if (options.IncludeSmartTests && options.SmartTestSessions.Count > 0)
        {
            recommendations.AddRange(GetSmartTestRecommendations(options.SmartTestSessions));
        }

        return recommendations;
    }

    /// <summary>
    /// Recomendaciones SMART basadas exclusivamente en el análisis ya calculado.
    /// Confía en HealthStatus y RequiresBackupRecommendation; no reinterpreta atributos raw.
    /// </summary>
    private static List<ReportRecommendation> GetSmartRecommendations(SmartAnalysisResult analysis)
    {
        var recs = new List<ReportRecommendation>();

        foreach (var disk in analysis.Reports)
        {
            var label = GetDiskLabel(disk);
            var criticalGenerated = false;

            // A. Critical: priorizar backup y evaluar reemplazo
            if (disk.HealthStatus == SmartHealthStatus.Critical)
            {
                recs.Add(new ReportRecommendation
                {
                    Category = $"SMART - {label}",
                    Message = "El disco presenta indicadores SMART críticos. Priorizar el backup de los datos importantes antes de continuar con pruebas o reparaciones y evaluar el reemplazo de la unidad.",
                    Severity = "Critical",
                    Icon = "❌"
                });
                criticalGenerated = true;
            }

            // B. Backup recomendado por el analizador (evita duplicar backup del mismo disco)
            if (disk.RequiresBackupRecommendation && !criticalGenerated)
            {
                recs.Add(new ReportRecommendation
                {
                    Category = $"SMART - {label}",
                    Message = "El análisis SMART recomienda priorizar el backup de los datos importantes de este disco antes de continuar con el diagnóstico.",
                    Severity = "Critical",
                    Icon = "❌"
                });
                criticalGenerated = true;
            }

            // C. Warning: revisar indicadores sin recomendar reemplazo obligatorio
            if (disk.HealthStatus == SmartHealthStatus.Warning && !criticalGenerated)
            {
                recs.Add(new ReportRecommendation
                {
                    Category = $"SMART - {label}",
                    Message = "El disco presenta advertencias SMART. Se recomienda revisar los indicadores, mantener backup actualizado y controlar su evolución.",
                    Severity = "Warning",
                    Icon = "⚠️"
                });
            }

            // D. NotAvailable: estado no concluyente, no confirma salud
            if (disk.HealthStatus == SmartHealthStatus.NotAvailable)
            {
                recs.Add(new ReportRecommendation
                {
                    Category = $"SMART - {label}",
                    Message = "No fue posible obtener un estado SMART concluyente para este disco. Esto no confirma que la unidad esté sana. Verificar compatibilidad, conexión o acceso al dispositivo antes de cerrar el diagnóstico.",
                    Severity = "Warning",
                    Icon = "⚠️"
                });
            }

            // E. Unknown: estado no determinado, no asumir salud
            if (disk.HealthStatus == SmartHealthStatus.Unknown)
            {
                recs.Add(new ReportRecommendation
                {
                    Category = $"SMART - {label}",
                    Message = "El estado SMART del disco no pudo determinarse. No asumir que la unidad está sana; completar el diagnóstico antes de concluir.",
                    Severity = "Warning",
                    Icon = "⚠️"
                });
            }

            // F. Good: no genera recomendación
        }

        return recs;
    }

    /// <summary>
    /// Recomendaciones de self-test basadas exclusivamente en Status persistido.
    /// Errors/Warnings operativos no se usan como heurística de salud.
    /// </summary>
    private static List<ReportRecommendation> GetSmartTestRecommendations(IEnumerable<SmartTestSession> tests)
    {
        var recs = new List<ReportRecommendation>();
        var seen = new HashSet<string>();

        foreach (var test in tests)
        {
            var label = GetTestLabel(test);

            switch (test.Status)
            {
                case SmartTestStatus.CompletedWithError:
                    if (seen.Add($"error:{test.Id}"))
                    {
                        var testName = test.TestType == SmartTestType.Extended
                            ? "El self-test extendido"
                            : "El self-test corto";
                        recs.Add(new ReportRecommendation
                        {
                            Category = $"Self-Test SMART - {label}",
                            Message = $"{testName} finalizó con errores. Priorizar el backup de los datos importantes y evaluar la unidad antes de continuar con operaciones que puedan generar carga adicional.",
                            Severity = "Critical",
                            Icon = "❌"
                        });
                    }
                    break;

                case SmartTestStatus.InProgress:
                case SmartTestStatus.Starting:
                    if (seen.Add($"running:{test.Id}"))
                    {
                        recs.Add(new ReportRecommendation
                        {
                            Category = $"Self-Test SMART - {label}",
                            Message = "Existe un self-test SMART todavía en ejecución o iniciándose. Consultar el resultado final antes de cerrar el diagnóstico.",
                            Severity = "Info",
                            Icon = "ℹ️"
                        });
                    }
                    break;

                case SmartTestStatus.Unsupported:
                    if (seen.Add($"unsupported:{test.Id}"))
                    {
                        recs.Add(new ReportRecommendation
                        {
                            Category = $"Self-Test SMART - {label}",
                            Message = "El dispositivo no soportó el self-test solicitado. Esto no permite determinar por sí solo el estado de salud del disco; utilizar el análisis SMART disponible y otros métodos de diagnóstico.",
                            Severity = "Info",
                            Icon = "ℹ️"
                        });
                    }
                    break;

                case SmartTestStatus.FailedToStart:
                    if (seen.Add($"failedstart:{test.Id}"))
                    {
                        recs.Add(new ReportRecommendation
                        {
                            Category = $"Self-Test SMART - {label}",
                            Message = "No fue posible iniciar el self-test SMART. Verificar soporte y acceso al dispositivo antes de repetir la prueba. Este resultado no implica por sí solo una falla del disco.",
                            Severity = "Warning",
                            Icon = "⚠️"
                        });
                    }
                    break;

                case SmartTestStatus.Aborted:
                case SmartTestStatus.Interrupted:
                case SmartTestStatus.Unknown:
                case SmartTestStatus.NotStarted:
                    if (seen.Add($"inconclusive:{test.Id}"))
                    {
                        recs.Add(new ReportRecommendation
                        {
                            Category = $"Self-Test SMART - {label}",
                            Message = GetInconclusiveMessage(test),
                            Severity = "Info",
                            Icon = "ℹ️"
                        });
                    }
                    break;

                case SmartTestStatus.CompletedWithoutError:
                    // No genera recomendación: no se afirma salud absoluta
                    break;
            }

            // Última consulta fallida: independiente del Status
            if (!test.LastCheckSucceeded &&
                (test.LastCheckedAt.HasValue || !string.IsNullOrEmpty(test.LastCheckError)) &&
                seen.Add($"checkfailed:{test.Id}"))
            {
                recs.Add(new ReportRecommendation
                {
                    Category = $"Self-Test SMART - {label}",
                    Message = "La última consulta del self-test no pudo completarse. El estado persistido podría no estar actualizado; verificarlo antes de emitir una conclusión definitiva.",
                    Severity = "Warning",
                    Icon = "⚠️"
                });
            }
        }

        return recs;
    }

    /// <summary>
    /// Etiqueta legible de un disco: ModelName → DeviceName → Device.
    /// </summary>
    private static string GetDiskLabel(SmartDiskReport disk)
    {
        if (!string.IsNullOrWhiteSpace(disk.ModelName)) return disk.ModelName;
        if (!string.IsNullOrWhiteSpace(disk.DeviceName)) return disk.DeviceName;
        return string.IsNullOrWhiteSpace(disk.Device) ? "Disco" : disk.Device;
    }

    /// <summary>
    /// Etiqueta legible de una sesión de self-test: ModelName → Device.
    /// </summary>
    private static string GetTestLabel(SmartTestSession test)
    {
        if (!string.IsNullOrWhiteSpace(test.ModelName)) return test.ModelName;
        return string.IsNullOrWhiteSpace(test.Device) ? "Disco" : test.Device;
    }

    private static string GetInconclusiveMessage(SmartTestSession test)
    {
        if (test.Status is SmartTestStatus.Aborted or SmartTestStatus.Interrupted)
        {
            return "El self-test no produjo un resultado concluyente. Repetir únicamente si las condiciones del equipo y del disco lo permiten.";
        }

        return "El self-test no produjo un resultado concluyente.";
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
