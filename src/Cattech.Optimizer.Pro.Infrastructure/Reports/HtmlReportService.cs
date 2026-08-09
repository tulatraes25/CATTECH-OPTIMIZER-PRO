using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Core.Models.Reports;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// Implementación de IReportGenerationService.
/// Genera informes técnicos HTML profesionales.
/// </summary>
public class HtmlReportService : IReportGenerationService
{
    private readonly string _baseDirectory;
    private readonly string _reportsDirectory;
    private readonly string _reportsInfoPath;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HtmlReportService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _reportsDirectory = Path.Combine(_baseDirectory, "reports", "html");
        _reportsInfoPath = Path.Combine(_reportsDirectory, "reports-info.json");
    }

    /// <inheritdoc/>
    public async Task<string> GenerateHtmlReportAsync(ReportGenerationOptions options)
    {
        EnsureDirectoryExists(_reportsDirectory);

        var html = GenerateHtmlContent(options);

        // Generar nombre de archivo
        var clientName = options.ServiceReport?.Client?.Name ?? "SinCliente";
        var safeName = SanitizeFileName(clientName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"Informe_Tecnico_CATTECH_{safeName}_{timestamp}.html";
        var filePath = Path.Combine(_reportsDirectory, fileName);

        await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);

        return filePath;
    }

    /// <inheritdoc/>
    public async Task SaveReportInfoAsync(GeneratedReportInfo info)
    {
        var reports = await LoadReportsInfoInternalAsync();
        reports.Add(info);
        await SaveReportsInfoInternalAsync(reports);
    }

    /// <inheritdoc/>
    public async Task<List<GeneratedReportInfo>> ListGeneratedReportsAsync(int maxResults = 20)
    {
        var reports = await LoadReportsInfoInternalAsync();
        return reports.OrderByDescending(r => r.CreatedAt).Take(maxResults).ToList();
    }

    /// <inheritdoc/>
    public Task OpenReportAsync(string htmlPath)
    {
        if (File.Exists(htmlPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = htmlPath,
                UseShellExecute = true
            });
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OpenReportsFolderAsync()
    {
        if (Directory.Exists(_reportsDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _reportsDirectory,
                UseShellExecute = true
            });
        }
        return Task.CompletedTask;
    }

    // =====================
    // GENERACIÓN HTML
    // =====================

    private string GenerateHtmlContent(ReportGenerationOptions options)
    {
        var sb = new StringBuilder();

        // CSS embebido
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("<title>Informe Técnico - CATTECH</title>");
        sb.AppendLine(GetCss());
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Portada / Encabezado
        sb.AppendLine(BuildHeader(options));

        // Datos del cliente
        if (options.IncludeClient && options.ServiceReport != null)
        {
            sb.AppendLine(BuildClientSection(options));
        }

        // Datos del equipo
        if (options.IncludeClient && options.ServiceReport != null)
        {
            sb.AppendLine(BuildEquipmentSection(options));
        }

        // Diagnóstico
        if (options.IncludeDiagnostic && options.DiagnosticReport != null)
        {
            sb.AppendLine(BuildDiagnosticSection(options));
        }

        // Acciones realizadas
        sb.AppendLine(BuildActionsSection(options));

        // Resultados
        sb.AppendLine(BuildResultsSection(options));

        // Estado SMART de discos
        if (options.IncludeSmart && options.SmartAnalysis != null)
        {
            sb.AppendLine(BuildSmartSection(options));
        }

        // Recomendaciones
        if (options.IncludeRecommendations)
        {
            sb.AppendLine(BuildRecommendationsSection(options));
        }

        // Observaciones finales
        if (!string.IsNullOrWhiteSpace(options.FinalObservations))
        {
            sb.AppendLine(BuildObservationsSection(options));
        }

        // Firma
        sb.AppendLine(BuildSignatureSection(options));

        // Footer
        sb.AppendLine(BuildFooter(options));

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private string BuildHeader(ReportGenerationOptions options)
    {
        var company = options.Settings?.Company;
        var color = company?.PrimaryColor ?? "#0078D4";
        var logoHtml = "";

        if (company != null && !string.IsNullOrEmpty(company.LogoPath) && File.Exists(company.LogoPath))
        {
            try
            {
                var imageBytes = File.ReadAllBytes(company.LogoPath);
                var base64 = Convert.ToBase64String(imageBytes);
                var ext = Path.GetExtension(company.LogoPath).ToLowerInvariant();
                var mime = ext == ".png" ? "image/png" : "image/jpeg";
                logoHtml = $"<img src=\"data:{mime};base64,{base64}\" alt=\"Logo\" class=\"logo\">";
            }
            catch { }
        }

        return $@"
<div class='header' style='border-bottom: 4px solid {color};'>
    <div class='header-content'>
        <div class='company-info'>
            {logoHtml}
            <div>
                <h1 style='color: {color};'>{EscapeHtml(company?.Name ?? "CATTECH OPTIMIZER PRO")}</h1>
                <p><strong>Técnico:</strong> {EscapeHtml(company?.TechnicianName ?? "No especificado")}</p>
                <p><strong>Teléfono:</strong> {EscapeHtml(company?.Phone ?? "-")} | <strong>WhatsApp:</strong> {EscapeHtml(company?.WhatsApp ?? "-")}</p>
                <p><strong>Email:</strong> {EscapeHtml(company?.Email ?? "-")} | <strong>Ciudad:</strong> {EscapeHtml(company?.City ?? "-")}</p>
            </div>
        </div>
        <div class='report-title'>
            <h2 style='color: {color};'>INFORME TÉCNICO</h2>
            <p class='subtitle'>Diagnóstico, Optimización y Mantenimiento de Windows</p>
            <p class='date'>Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}</p>
        </div>
    </div>
</div>";
    }

    private string BuildClientSection(ReportGenerationOptions options)
    {
        var client = options.ServiceReport!.Client;
        return $@"
<div class='section'>
    <h2 class='section-title'>Datos del Cliente</h2>
    <table>
        <tr><td class='label'>Nombre:</td><td>{EscapeHtml(client.Name)}</td></tr>
        <tr><td class='label'>Teléfono:</td><td>{EscapeHtml(client.Phone)}</td></tr>
        <tr><td class='label'>Email:</td><td>{EscapeHtml(client.Email)}</td></tr>
        <tr><td class='label'>Empresa:</td><td>{EscapeHtml(client.Company)}</td></tr>
        <tr><td class='label'>Dirección:</td><td>{EscapeHtml(client.Address)}</td></tr>
        <tr><td class='label'>Observaciones:</td><td>{EscapeHtml(client.Notes)}</td></tr>
    </table>
</div>";
    }

    private string BuildEquipmentSection(ReportGenerationOptions options)
    {
        var equip = options.ServiceReport!.Equipment;
        return $@"
<div class='section'>
    <h2 class='section-title'>Datos del Equipo</h2>
    <table>
        <tr><td class='label'>Marca:</td><td>{EscapeHtml(equip.Brand)}</td><td class='label'>Modelo:</td><td>{EscapeHtml(equip.Model)}</td></tr>
        <tr><td class='label'>Serie:</td><td>{EscapeHtml(equip.SerialNumber)}</td><td class='label'>Tipo:</td><td>{EscapeHtml(equip.EquipmentType)}</td></tr>
        <tr><td class='label'>Equipo:</td><td>{EscapeHtml(equip.ComputerName)}</td><td class='label'>Usuario:</td><td>{EscapeHtml(equip.CurrentUser)}</td></tr>
        <tr><td class='label'>SO:</td><td>{EscapeHtml(equip.OperatingSystem)}</td><td class='label'>Edición:</td><td>{EscapeHtml(equip.WindowsEdition)}</td></tr>
        <tr><td class='label'>Arquitectura:</td><td>{EscapeHtml(equip.Architecture)}</td><td class='label'>Procesador:</td><td>{EscapeHtml(equip.Processor)}</td></tr>
        <tr><td class='label'>RAM:</td><td>{equip.RamTotalGB} GB</td><td class='label'>Disco:</td><td>{EscapeHtml(equip.PrimaryDisk)} ({EscapeHtml(equip.DiskType)})</td></tr>
        <tr><td class='label'>Capacidad:</td><td>{equip.DiskCapacityGB:F1} GB</td><td class='label'>Libre:</td><td>{equip.DiskFreeGB:F1} GB</td></tr>
        <tr><td class='label'>Motivo:</td><td colspan='3'>{EscapeHtml(equip.ServiceReason)}</td></tr>
    </table>
</div>";
    }

    private string BuildDiagnosticSection(ReportGenerationOptions options)
    {
        var diag = options.DiagnosticReport!;
        var alertsHtml = "";

        if (diag.Alerts.Count > 0)
        {
            alertsHtml = "<div class='alerts'>";
            foreach (var alert in diag.Alerts)
            {
                var alertClass = alert.Severity switch
                {
                    AlertSeverity.Critical => "alert-critical",
                    AlertSeverity.Warning => "alert-warning",
                    _ => "alert-info"
                };
                alertsHtml += $@"<div class='alert {alertClass}'>
                    <strong>{EscapeHtml(alert.Category)}:</strong> {EscapeHtml(alert.Message)}
                    {(string.IsNullOrEmpty(alert.Recommendation) ? "" : $"<br><em>{EscapeHtml(alert.Recommendation)}</em>")}
                </div>";
            }
            alertsHtml += "</div>";
        }

        return $@"
<div class='section'>
    <h2 class='section-title'>Diagnóstico Inicial</h2>
    <table>
        <tr><td class='label'>SO:</td><td>{EscapeHtml(diag.OsName)}</td><td class='label'>Edición:</td><td>{EscapeHtml(diag.WindowsEdition)}</td></tr>
        <tr><td class='label'>CPU:</td><td>{EscapeHtml(diag.Processor)} ({diag.CpuCores} núcleos)</td><td class='label'>RAM:</td><td>{diag.RamTotalGB} GB ({diag.RamUsagePercent}% uso)</td></tr>
        <tr><td class='label'>Disco:</td><td>{EscapeHtml(diag.PrimaryDiskName)} ({EscapeHtml(diag.DiskType)})</td><td class='label'>Espacio libre:</td><td>{diag.DiskFreeGB:F1} GB ({diag.DiskFreePercent}%)</td></tr>
        <tr><td class='label'>Inicio:</td><td>{diag.Startup.TotalCount} programas ({diag.Startup.ThirdPartyCount} de terceros)</td><td class='label'>Temporales:</td><td>{diag.TempFiles.TotalSizeGB} GB</td></tr>
        <tr><td class='label'>Antivirus:</td><td>{EscapeHtml(diag.Security.AntivirusName)}</td><td class='label'>Firewall:</td><td>{(diag.Security.FirewallActive ? "Activo" : "Inactivo")}</td></tr>
        <tr><td class='label'>Windows Update:</td><td colspan='3'>{EscapeHtml(diag.Security.WindowsUpdateStatus)}</td></tr>
    </table>
    {alertsHtml}
</div>";
    }

    private string BuildActionsSection(ReportGenerationOptions options)
    {
        var actions = new List<string>();

        // Punto de restauración
        if (options.IncludeRestorePoint && options.RestorePointResult != null)
        {
            var rp = options.RestorePointResult;
            var status = rp.Success ? "✅ Creado correctamente" : $"❌ No se pudo crear: {EscapeHtml(rp.ErrorMessage)}";
            actions.Add($"<tr><td class='label'>Punto de restauración:</td><td>{status}</td></tr>");
        }

        // Limpieza
        if (options.IncludeCleanup && options.CleanupResult != null)
        {
            var cl = options.CleanupResult;
            actions.Add($"<tr><td class='label'>Limpieza de temporales:</td><td>{cl.DeletedMB} MB liberados, {cl.DeletedFiles} archivos eliminados</td></tr>");
        }

        // Optimización visual
        if (options.IncludeVisualOptimization && options.VisualOptimizationResult != null)
        {
            var vo = options.VisualOptimizationResult;
            actions.Add($"<tr><td class='label'>Optimización visual:</td><td>{vo.AppliedCount} ajustes aplicados</td></tr>");
        }

        // Programas de inicio
        if (options.IncludeStartup && options.StartupAnalysis != null)
        {
            actions.Add($"<tr><td class='label'>Análisis de inicio:</td><td>{options.StartupAnalysis.TotalCount} programas analizados, {options.StartupAnalysis.ThirdPartyCount} de terceros</td></tr>");
        }

        if (actions.Count == 0)
        {
            actions.Add("<tr><td colspan='2'>No se registraron acciones en esta visita.</td></tr>");
        }

        return $@"
<div class='section'>
    <h2 class='section-title'>Acciones Realizadas</h2>
    <table>
        {string.Join("\n        ", actions)}
    </table>
</div>";
    }

    private string BuildResultsSection(ReportGenerationOptions options)
    {
        var results = new List<string>();

        // Resultado de limpieza
        if (options.CleanupResult != null && options.CleanupResult.DeletedFiles > 0)
        {
            results.Add($"<div class='result-item'><strong>Espacio liberado:</strong> {options.CleanupResult.DeletedMB} MB ({options.CleanupResult.DeletedFiles} archivos eliminados)</div>");
        }

        // Resultado de optimización
        if (options.VisualOptimizationResult != null && options.VisualOptimizationResult.AppliedCount > 0)
        {
            results.Add($"<div class='result-item'><strong>Ajustes visuales aplicados:</strong> {options.VisualOptimizationResult.AppliedCount}</div>");
            if (options.VisualOptimizationResult.RequiresRestart)
                results.Add("<div class='result-item warning'>⚠️ Se requiere reinicio para que algunos cambios surtan efecto.</div>");
        }

        // Punto de restauración
        if (options.RestorePointResult != null)
        {
            if (options.RestorePointResult.Success)
                results.Add($"<div class='result-item success'>✅ Punto de restauración creado: {EscapeHtml(options.RestorePointResult.RestorePointName)}</div>");
            else
                results.Add($"<div class='result-item warning'>⚠️ No se pudo crear punto de restauración: {EscapeHtml(options.RestorePointResult.ErrorMessage)}</div>");
        }

        if (results.Count == 0)
        {
            results.Add("<div class='result-item'>No hay resultados específicos para mostrar.</div>");
        }

        return $@"
<div class='section'>
    <h2 class='section-title'>Resultados</h2>
    {string.Join("\n    ", results)}
</div>";
    }

    /// <summary>
    /// Construye la sección HTML de Estado SMART de Discos.
    /// Usa únicamente resultados SMART persistidos (no ejecuta smartctl).
    /// </summary>
    private string BuildSmartSection(ReportGenerationOptions options)
    {
        var analysis = options.SmartAnalysis!;

        // Resumen general
        var good = analysis.Reports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
        var warning = analysis.Reports.Count(r => r.HealthStatus == SmartHealthStatus.Warning);
        var critical = analysis.Reports.Count(r => r.HealthStatus == SmartHealthStatus.Critical);
        var notAvailable = analysis.Reports.Count(r => r.HealthStatus == SmartHealthStatus.NotAvailable);
        var unknown = analysis.Reports.Count(r => r.HealthStatus == SmartHealthStatus.Unknown);

        var summaryHtml = $@"
<div class='smart-summary'>
    <table>
        <tr><td class='label'>Fecha del análisis:</td><td>{analysis.StartedAt:dd/MM/yyyy HH:mm}</td></tr>
        <tr><td class='label'>Versión de smartctl:</td><td>{EscapeHtml(analysis.SmartctlVersion)}</td></tr>
        <tr><td class='label'>Discos analizados:</td><td>{analysis.DevicesAnalyzed}</td></tr>
        <tr><td class='label'>Buenos:</td><td>{good}</td></tr>
        <tr><td class='label'>Precaución:</td><td>{warning}</td></tr>
        <tr><td class='label'>Críticos:</td><td>{critical}</td></tr>
        <tr><td class='label'>No disponibles:</td><td>{notAvailable}</td></tr>
        <tr><td class='label'>Desconocidos:</td><td>{unknown}</td></tr>
    </table>
</div>";

        // Detalle por disco
        var disksHtml = new StringBuilder();
        foreach (var report in analysis.Reports)
        {
            disksHtml.AppendLine(BuildSmartDiskBlock(report));
        }

        return $@"
<div class='section' style='page-break-inside: auto;'>
    <h2 class='section-title'>Estado SMART de Discos</h2>
    {summaryHtml}
    {disksHtml}
</div>";
    }

    /// <summary>
    /// Construye el bloque HTML de detalle de un disco SMART.
    /// </summary>
    private string BuildSmartDiskBlock(SmartDiskReport report)
    {
        var statusClass = report.HealthStatus switch
        {
            SmartHealthStatus.Good => "smart-good",
            SmartHealthStatus.Warning => "smart-warning",
            SmartHealthStatus.Critical => "smart-critical",
            SmartHealthStatus.NotAvailable => "smart-unavailable",
            _ => "smart-unknown"
        };

        var statusText = report.HealthStatus switch
        {
            SmartHealthStatus.Good => "Bueno",
            SmartHealthStatus.Warning => "Precaución",
            SmartHealthStatus.Critical => "Crítico",
            SmartHealthStatus.NotAvailable => "No disponible",
            SmartHealthStatus.Unknown => "Desconocido",
            _ => "Desconocido"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"<div class='smart-disk'>");
        sb.AppendLine($"<h3>{EscapeHtml(report.ModelName)} <span class='{statusClass}'>{statusText}</span></h3>");

        // Información básica
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><td class='label'>Dispositivo:</td><td>{EscapeHtml(report.Device)}</td></tr>");
        if (!string.IsNullOrEmpty(report.SerialNumber))
            sb.AppendLine($"<tr><td class='label'>Número de serie:</td><td>{EscapeHtml(report.SerialNumber)}</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Tipo:</td><td>{EscapeHtml(report.DeviceType)}</td></tr>");
        if (!string.IsNullOrEmpty(report.Protocol))
            sb.AppendLine($"<tr><td class='label'>Protocolo:</td><td>{EscapeHtml(report.Protocol)}</td></tr>");
        if (!string.IsNullOrEmpty(report.FirmwareVersion))
            sb.AppendLine($"<tr><td class='label'>Firmware:</td><td>{EscapeHtml(report.FirmwareVersion)}</td></tr>");
        if (report.CapacityBytes > 0)
            sb.AppendLine($"<tr><td class='label'>Capacidad:</td><td>{report.CapacityGB} GB</td></tr>");
        sb.AppendLine($"<tr><td class='label'>Estado:</td><td>{statusText}</td></tr>");

        // HealthSummary
        if (!string.IsNullOrEmpty(report.HealthSummary))
            sb.AppendLine($"<tr><td class='label'>Resumen:</td><td>{EscapeHtml(report.HealthSummary)}</td></tr>");

        // NotAvailable: aclaración explícita
        if (report.HealthStatus == SmartHealthStatus.NotAvailable)
            sb.AppendLine("<tr><td colspan='2'><em>SMART no disponible. Esto no permite confirmar que el disco esté sano.</em></td></tr>");

        // Métricas ATA/SATA
        var isAta = report.DeviceType.Contains("HDD", StringComparison.OrdinalIgnoreCase) ||
                    report.DeviceType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                    report.Protocol.Contains("SATA", StringComparison.OrdinalIgnoreCase);

        if (isAta || report.TemperatureCelsius > 0 || report.PowerOnHours > 0)
        {
            if (report.TemperatureCelsius > 0)
                sb.AppendLine($"<tr><td class='label'>Temperatura:</td><td>{report.TemperatureCelsius} °C</td></tr>");
            if (report.PowerOnHours > 0)
                sb.AppendLine($"<tr><td class='label'>Horas de uso:</td><td>{report.PowerOnHours}</td></tr>");
            if (report.PowerCycleCount > 0)
                sb.AppendLine($"<tr><td class='label'>Ciclos de encendido:</td><td>{report.PowerCycleCount}</td></tr>");
            sb.AppendLine($"<tr><td class='label'>Sectores reasignados:</td><td>{report.ReallocatedSectorCount}</td></tr>");
            sb.AppendLine($"<tr><td class='label'>Sectores pendientes:</td><td>{report.PendingSectorCount}</td></tr>");
            sb.AppendLine($"<tr><td class='label'>Offline no corregibles:</td><td>{report.OfflineUncorrectableCount}</td></tr>");
            sb.AppendLine($"<tr><td class='label'>Errores CRC UDMA:</td><td>{report.UDMACrcErrorCount}</td></tr>");
        }

        // Métricas NVMe
        if (report.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
        {
            if (report.TemperatureCelsius > 0)
                sb.AppendLine($"<tr><td class='label'>Temperatura:</td><td>{report.TemperatureCelsius} °C</td></tr>");
            if (report.PowerOnHours > 0)
                sb.AppendLine($"<tr><td class='label'>Horas de uso:</td><td>{report.PowerOnHours}</td></tr>");
            if (report.NvmePercentageUsed.HasValue)
                sb.AppendLine($"<tr><td class='label'>Porcentaje usado:</td><td>{report.NvmePercentageUsed}%</td></tr>");
            if (report.NvmeAvailableSpare.HasValue)
                sb.AppendLine($"<tr><td class='label'>Spare disponible:</td><td>{report.NvmeAvailableSpare}%</td></tr>");
            if (report.NvmeMediaErrors.HasValue)
                sb.AppendLine($"<tr><td class='label'>Media errors:</td><td>{report.NvmeMediaErrors}</td></tr>");
            if (report.NvmeUnsafeShutdowns.HasValue)
                sb.AppendLine($"<tr><td class='label'>Unsafe shutdowns:</td><td>{report.NvmeUnsafeShutdowns}</td></tr>");
        }

        sb.AppendLine("</table>");

        // Backup recomendado
        if (report.RequiresBackupRecommendation)
        {
            sb.AppendLine("<div class='smart-backup-warning'><strong>Backup recomendado: Sí</strong> - Se recomienda realizar backup de los datos importantes antes de continuar con pruebas o reparaciones.</div>");
        }
        else
        {
            sb.AppendLine("<div class='smart-backup-ok'><strong>Backup recomendado: No</strong></div>");
        }

        // Warnings
        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("<div class='smart-warnings'><strong>Advertencias:</strong><ul>");
            foreach (var warning in report.Warnings)
                sb.AppendLine($"<li>{EscapeHtml(warning)}</li>");
            sb.AppendLine("</ul></div>");
        }

        // Errors
        if (report.Errors.Count > 0)
        {
            sb.AppendLine("<div class='smart-errors'><strong>Errores SMART:</strong><ul>");
            foreach (var error in report.Errors)
                sb.AppendLine($"<li>{EscapeHtml(error)}</li>");
            sb.AppendLine("</ul></div>");
        }

        sb.AppendLine("</div>");

        return sb.ToString();
    }

    private string BuildRecommendationsSection(ReportGenerationOptions options)
    {
        var recommendations = ReportRecommendationEngine.GenerateRecommendations(options);

        if (recommendations.Count == 0)
        {
            return @"";
        }

        var html = "<div class='section'><h2 class='section-title'>Recomendaciones</h2><div class='recommendations'>";
        foreach (var rec in recommendations)
        {
            var recClass = rec.Severity switch
            {
                "Critical" => "rec-critical",
                "Warning" => "rec-warning",
                _ => "rec-info"
            };
            html += $@"<div class='recommendation {recClass}'>
                <span class='rec-icon'>{rec.Icon}</span>
                <div><strong>{EscapeHtml(rec.Category)}:</strong> {EscapeHtml(rec.Message)}</div>
            </div>";
        }
        html += "</div></div>";
        return html;
    }

    private string BuildObservationsSection(ReportGenerationOptions options)
    {
        return $@"
<div class='section'>
    <h2 class='section-title'>Observaciones Finales</h2>
    <div class='observations'>{EscapeHtml(options.FinalObservations)}</div>
</div>";
    }

    private string BuildSignatureSection(ReportGenerationOptions options)
    {
        var company = options.Settings?.Company;
        return $@"
<div class='section signature'>
    <div class='sig-block'>
        <div class='sig-line'></div>
        <p><strong>{EscapeHtml(company?.TechnicianName ?? "Técnico")}</strong></p>
        <p>Técnico Responsable</p>
    </div>
    <div class='sig-block'>
        <div class='sig-line'></div>
        <p><strong>{EscapeHtml(company?.Name ?? "Empresa")}</strong></p>
        <p>{EscapeHtml(company?.FooterLegend ?? "")}</p>
    </div>
</div>";
    }

    private string BuildFooter(ReportGenerationOptions options)
    {
        return $@"
<div class='footer'>
    <p>Informe generado por CATTECH OPTIMIZER PRO v0.1.1 | {DateTime.Now:dd/MM/yyyy HH:mm}</p>
</div>";
    }

    // =====================
    // CSS
    // =====================

    private string GetCss()
    {
        return @"
<style>
    @page { size: A4; margin: 15mm; }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; font-size: 11pt; color: #333; line-height: 1.4; background: #fff; padding: 20px; }
    .header { margin-bottom: 20px; padding-bottom: 15px; }
    .header-content { display: flex; justify-content: space-between; align-items: flex-start; }
    .company-info { flex: 1; }
    .company-info h1 { font-size: 18pt; margin-bottom: 5px; }
    .company-info p { font-size: 9pt; color: #555; margin: 2px 0; }
    .logo { max-height: 60px; margin-right: 15px; }
    .report-title { text-align: right; }
    .report-title h2 { font-size: 14pt; margin-bottom: 5px; }
    .subtitle { font-size: 10pt; color: #666; }
    .date { font-size: 9pt; color: #888; margin-top: 5px; }
    .section { margin-bottom: 18px; page-break-inside: avoid; }
    .section-title { font-size: 12pt; color: #0078D4; border-bottom: 2px solid #0078D4; padding-bottom: 4px; margin-bottom: 10px; }
    table { width: 100%; border-collapse: collapse; margin-bottom: 10px; }
    td { padding: 5px 8px; border: 1px solid #ddd; font-size: 10pt; vertical-align: top; }
    .label { background: #f5f5f5; font-weight: 600; width: 120px; color: #444; }
    .alerts { margin-top: 10px; }
    .alert { padding: 8px 12px; border-radius: 4px; margin-bottom: 6px; font-size: 10pt; }
    .alert-critical { background: #FDECEA; border-left: 4px solid #D93025; color: #C62828; }
    .alert-warning { background: #FFF8E1; border-left: 4px solid #F9A825; color: #E65100; }
    .alert-info { background: #E3F2FD; border-left: 4px solid #42A5F5; color: #1565C0; }
    .result-item { padding: 6px 0; border-bottom: 1px solid #eee; font-size: 10pt; }
    .result-item.success { color: #2E7D32; }
    .result-item.warning { color: #E65100; }
    .recommendations { margin-top: 8px; }
    .recommendation { padding: 8px 12px; border-radius: 4px; margin-bottom: 6px; font-size: 10pt; display: flex; align-items: flex-start; }
    .rec-icon { margin-right: 8px; font-size: 14pt; }
    .rec-critical { background: #FDECEA; border-left: 4px solid #D93025; }
    .rec-warning { background: #FFF8E1; border-left: 4px solid #F9A825; }
    .rec-info { background: #E3F2FD; border-left: 4px solid #42A5F5; }
    .observations { background: #f9f9f9; padding: 12px; border: 1px solid #ddd; border-radius: 4px; font-size: 10pt; white-space: pre-wrap; }
    .signature { display: flex; justify-content: space-around; margin-top: 40px; padding-top: 20px; }
    .sig-block { text-align: center; width: 200px; }
    .sig-line { border-bottom: 1px solid #333; margin-bottom: 8px; height: 40px; }
    .sig-block p { font-size: 9pt; color: #555; }
    .footer { text-align: center; margin-top: 30px; padding-top: 10px; border-top: 1px solid #ddd; font-size: 8pt; color: #999; }
    .smart-summary { margin-bottom: 15px; }
    .smart-disk { margin-bottom: 15px; padding: 10px; border: 1px solid #ddd; border-radius: 4px; page-break-inside: avoid; }
    .smart-disk h3 { font-size: 11pt; margin-bottom: 8px; }
    .smart-good { background: #E8F5E9; color: #2E7D32; padding: 2px 8px; border-radius: 3px; font-size: 9pt; font-weight: 600; }
    .smart-warning { background: #FFF8E1; color: #E65100; padding: 2px 8px; border-radius: 3px; font-size: 9pt; font-weight: 600; }
    .smart-critical { background: #FDECEA; color: #C62828; padding: 2px 8px; border-radius: 3px; font-size: 9pt; font-weight: 600; }
    .smart-unavailable { background: #F5F5F5; color: #757575; padding: 2px 8px; border-radius: 3px; font-size: 9pt; font-weight: 600; }
    .smart-unknown { background: #F5F5F5; color: #616161; padding: 2px 8px; border-radius: 3px; font-size: 9pt; font-weight: 600; }
    .smart-backup-warning { background: #FFF8E1; border-left: 4px solid #F9A825; padding: 8px 12px; margin: 8px 0; font-size: 10pt; }
    .smart-backup-ok { background: #E8F5E9; border-left: 4px solid #4CAF50; padding: 8px 12px; margin: 8px 0; font-size: 10pt; }
    .smart-warnings { background: #FFF8E1; padding: 8px 12px; margin: 8px 0; border-radius: 4px; font-size: 10pt; }
    .smart-errors { background: #FDECEA; padding: 8px 12px; margin: 8px 0; border-radius: 4px; font-size: 10pt; }
    .smart-warnings ul, .smart-errors ul { margin: 4px 0 0 18px; }
</style>";
    }

    // =====================
    // HELPERS
    // =====================

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return System.Net.WebUtility.HtmlEncode(text);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }

    private async Task<List<GeneratedReportInfo>> LoadReportsInfoInternalAsync()
    {
        if (!File.Exists(_reportsInfoPath))
            return new List<GeneratedReportInfo>();

        try
        {
            var json = await File.ReadAllTextAsync(_reportsInfoPath);
            return JsonSerializer.Deserialize<List<GeneratedReportInfo>>(json, SerializerOptions)
                   ?? new List<GeneratedReportInfo>();
        }
        catch
        {
            return new List<GeneratedReportInfo>();
        }
    }

    private async Task SaveReportsInfoInternalAsync(List<GeneratedReportInfo> reports)
    {
        EnsureDirectoryExists(_reportsDirectory);
        var json = JsonSerializer.Serialize(reports, SerializerOptions);
        await File.WriteAllTextAsync(_reportsInfoPath, json);
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
