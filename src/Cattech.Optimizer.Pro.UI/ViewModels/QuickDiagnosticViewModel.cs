using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// Severidad de un problema de fuente del diagnóstico.
/// </summary>
public enum DiagnosticIssueSeverity
{
    /// <summary>La fuente respondió incompleta pero el diagnóstico continuó.</summary>
    Warning,

    /// <summary>La fuente falló o no devolvió datos; el diagnóstico es parcial.</summary>
    Error
}

/// <summary>
/// Problema técnico asociado a una fuente del diagnóstico (errores y
/// advertencias de <see cref="DiagnosticReport"/>).
/// </summary>
public sealed class DiagnosticSourceIssue
{
    public DiagnosticIssueSeverity Severity { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    public string SeverityText => Severity == DiagnosticIssueSeverity.Error
        ? "ERROR"
        : "ADVERTENCIA";
}

/// <summary>
/// ViewModel para la pantalla de diagnóstico rápido.
/// Presenta los datos crudos del reporte, los errores/advertencias reales por
/// fuente y permite copiar/exportar el resultado sin dialogs en la lógica de
/// construcción de texto (funciones puras para test).
/// </summary>
public partial class QuickDiagnosticViewModel : ObservableObject
{
    private readonly IDiagnosticService _diagnosticService;

    /// <summary>
    /// Opciones JSON idénticas a las que usa la app para persistir diagnósticos
    /// (DiagnosticService): indentado y camelCase. La exportación manual debe
    /// producir el mismo formato que data/diagnostics.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin ejecutar";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _hasAlerts;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Problemas por fuente y detalle técnico ---

    [ObservableProperty]
    private bool _hasSourceIssues;

    /// <summary>
    /// True cuando el reporte trae errores de fuente: la UI muestra
    /// "Diagnóstico parcial" en lugar de un éxito.
    /// </summary>
    [ObservableProperty]
    private bool _isPartial;

    /// <summary>
    /// JSON del último diagnóstico (mismo formato que data/diagnostics) o texto
    /// estructurado, generado en el VM para evitar bindings gigantes en XAML.
    /// </summary>
    [ObservableProperty]
    private string _technicalDetailText = string.Empty;

    [ObservableProperty]
    private bool _hasTechnicalDetail;

    [ObservableProperty]
    private string _diagnosticsFolderPath = string.Empty;

    // --- Datos del diagnóstico ---

    [ObservableProperty]
    private string _osName = string.Empty;

    [ObservableProperty]
    private string _windowsEdition = string.Empty;

    [ObservableProperty]
    private string _architecture = string.Empty;

    [ObservableProperty]
    private string _computerName = string.Empty;

    [ObservableProperty]
    private string _currentUser = string.Empty;

    [ObservableProperty]
    private string _processor = string.Empty;

    [ObservableProperty]
    private string _ramInfo = string.Empty;

    [ObservableProperty]
    private string _ramUsage = string.Empty;

    [ObservableProperty]
    private string _diskName = string.Empty;

    [ObservableProperty]
    private string _diskType = string.Empty;

    [ObservableProperty]
    private string _diskCapacity = string.Empty;

    [ObservableProperty]
    private string _diskFree = string.Empty;

    [ObservableProperty]
    private string _diskFreePercent = string.Empty;

    [ObservableProperty]
    private string _startupInfo = string.Empty;

    [ObservableProperty]
    private string _startupThirdParty = string.Empty;

    [ObservableProperty]
    private string _tempFilesInfo = string.Empty;

    [ObservableProperty]
    private string _antivirusInfo = string.Empty;

    [ObservableProperty]
    private string _firewallInfo = string.Empty;

    [ObservableProperty]
    private string _windowsUpdateInfo = string.Empty;

    [ObservableProperty]
    private string _virtualMemoryInfo = string.Empty;

    // --- Alertas y problemas por fuente ---

    public ObservableCollection<DiagnosticAlert> Alerts { get; } = new();

    public ObservableCollection<DiagnosticSourceIssue> SourceIssues { get; } = new();

    // --- Diagnóstico actual ---

    private DiagnosticReport? _currentReport;

    public QuickDiagnosticViewModel(IDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
        DiagnosticsFolderPath = ResolveDiagnosticsDirectory(AppDomain.CurrentDomain.BaseDirectory);
    }

    /// <summary>
    /// Ejecuta el diagnóstico rápido.
    /// </summary>
    [RelayCommand]
    private async Task RunDiagnosticAsync()
    {
        ClearMessages();
        IsRunning = true;
        HasResults = false;
        StatusText = "Analizando...";

        var progress = new Progress<int>(percent =>
        {
            ProgressPercent = percent;
        });

        try
        {
            _currentReport = await _diagnosticService.RunQuickDiagnosticAsync(progress);

            LoadResultsToUI(_currentReport);

            HasResults = true;
            HasAlerts = Alerts.Count > 0;
            StatusText = IsPartial
                ? "Diagnóstico parcial"
                : Alerts.Count > 0
                    ? "Diagnóstico completado con advertencias"
                    : "Diagnóstico completado";
        }
        catch (Exception ex)
        {
            StatusText = "Error al diagnosticar";
            // El mensaje de una excepción de WMI/archivos puede incluir rutas
            // privadas: se usa un mensaje técnico acotado y se descarta el detalle.
            ShowError($"Error durante el diagnóstico: {SanitizeExceptionMessage(ex)}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Guarda el diagnóstico actual en data/diagnostics (comportamiento existente).
    /// </summary>
    [RelayCommand]
    private async Task SaveDiagnosticAsync()
    {
        if (_currentReport == null)
        {
            ShowError("No hay diagnóstico para guardar. Ejecutá uno primero.");
            return;
        }

        ClearMessages();

        try
        {
            var fileName = await _diagnosticService.SaveDiagnosticAsync(_currentReport);
            ShowSuccess($"Diagnóstico guardado: {fileName}");
        }
        catch (Exception)
        {
            ShowError("Error al guardar el diagnóstico en data/diagnostics.");
        }
    }

    /// <summary>
    /// Copia al portapapeles un resumen plain-text/TSV del reporte actual.
    /// </summary>
    [RelayCommand]
    private void CopySummary()
    {
        if (_currentReport == null)
        {
            ShowError("No hay diagnóstico para copiar. Ejecutá uno primero.");
            return;
        }

        ClearMessages();

        try
        {
            Clipboard.SetText(BuildSummaryText(_currentReport));
            ShowSuccess("Resumen del diagnóstico copiado al portapapeles.");
        }
        catch (Exception)
        {
            ShowError("No se pudo copiar el resumen al portapapeles.");
        }
    }

    /// <summary>
    /// Exporta el reporte actual a un archivo JSON elegido por el técnico, con
    /// las mismas opciones de serialización que usa la app.
    /// </summary>
    [RelayCommand]
    private void ExportJson()
    {
        if (_currentReport == null)
        {
            ShowError("No hay diagnóstico para exportar. Ejecutá uno primero.");
            return;
        }

        ClearMessages();

        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "Exportar diagnóstico JSON",
                Filter = "JSON (*.json)|*.json|Todos los archivos (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"diagnostic-{_currentReport.DiagnosisDate:yyyyMMdd-HHmmss}-{_currentReport.Id}.json"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            File.WriteAllText(dialog.FileName, BuildJson(_currentReport), new UTF8Encoding(false));
            // Solo el nombre de archivo: no se exponen rutas privadas en banners.
            ShowSuccess($"Diagnóstico exportado: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception)
        {
            ShowError("No se pudo exportar el JSON del diagnóstico.");
        }
    }

    /// <summary>
    /// Abre (creando si falta) la carpeta data/diagnostics del ejecutable.
    /// </summary>
    [RelayCommand]
    private void OpenDiagnosticsFolder()
    {
        ClearMessages();

        try
        {
            var directory = ResolveDiagnosticsDirectory(AppDomain.CurrentDomain.BaseDirectory);
            Directory.CreateDirectory(directory);
            DiagnosticsFolderPath = directory;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });

            ShowSuccess("Carpeta de diagnósticos abierta.");
        }
        catch (Exception)
        {
            ShowError("No se pudo abrir la carpeta de diagnósticos.");
        }
    }

    // --- Funciones puras (testables sin Clipboard ni diálogos) ---

    /// <summary>
    /// Directorio de diagnósticos: data/diagnostics relativo a BaseDirectory.
    /// </summary>
    public static string ResolveDiagnosticsDirectory(string baseDirectory)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;

        return Path.Combine(root, "data", "diagnostics");
    }

    /// <summary>
    /// Construye la lista visible de errores y advertencias por fuente del
    /// reporte. No inventa problemas: solo refleja report.Errors / report.Warnings.
    /// </summary>
    public static List<DiagnosticSourceIssue> BuildSourceIssues(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var issues = new List<DiagnosticSourceIssue>();

        foreach (var error in report.Errors ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(error))
                continue;

            issues.Add(new DiagnosticSourceIssue
            {
                Severity = DiagnosticIssueSeverity.Error,
                Source = ExtractSource(error),
                Message = error
            });
        }

        foreach (var warning in report.Warnings ?? new List<string>())
        {
            if (string.IsNullOrWhiteSpace(warning))
                continue;

            issues.Add(new DiagnosticSourceIssue
            {
                Severity = DiagnosticIssueSeverity.Warning,
                Source = ExtractSource(warning),
                Message = warning
            });
        }

        return issues;
    }

    /// <summary>
    /// Serializa el reporte con exactamente las mismas opciones JSON que la app.
    /// </summary>
    public static string BuildJson(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, SerializerOptions);
    }

    /// <summary>
    /// Resumen plain-text/TSV del reporte: sistema, hardware, discos, arranque,
    /// temporales, seguridad, memoria virtual, alertas, errores y advertencias.
    /// </summary>
    public static string BuildSummaryText(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var sb = new StringBuilder();
        var errors = report.Errors ?? new List<string>();
        var warnings = report.Warnings ?? new List<string>();

        sb.AppendLine("CATTECH Optimizer Pro — Diagnóstico rápido");
        sb.AppendLine($"ID: {report.Id}");
        sb.AppendLine($"Fecha: {report.DiagnosisDate:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Estado: {(errors.Count > 0 ? "Diagnóstico parcial" : "Completo")}");
        sb.AppendLine($"Equipo: {Dash(report.ComputerName)}");
        sb.AppendLine($"SO: {Dash(report.OsName)} ({Dash(report.WindowsEdition)})");
        sb.AppendLine($"Arquitectura: {Dash(report.Architecture)}");
        sb.AppendLine($"Usuario: {Dash(report.CurrentUser)}");
        if (!string.IsNullOrWhiteSpace(report.TechnicianName))
            sb.AppendLine($"Técnico: {report.TechnicianName}");

        sb.AppendLine();
        sb.AppendLine("HARDWARE");
        sb.AppendLine($"CPU: {Dash(report.Processor)}");
        sb.AppendLine($"Núcleos: {report.CpuCores}");
        sb.AppendLine(
            $"RAM: {Num(report.RamTotalGB)} GB | en uso: {Num(report.RamUsedGB)} GB " +
            $"({Num(report.RamUsagePercent)} %) | disponible: {Num(report.RamAvailableGB)} GB");

        sb.AppendLine();
        sb.AppendLine("DISCO");
        sb.AppendLine($"Disco: {Dash(report.PrimaryDiskName)} | tipo: {Dash(report.DiskType)}");
        if (report.DiskFreeSpaceKnown)
        {
            sb.AppendLine(
                $"Capacidad: {Num(report.DiskCapacityGB)} GB | libre: {Num(report.DiskFreeGB)} GB " +
                $"({Num(report.DiskFreePercent)} %) | volumen: {Dash(report.SystemVolumeLetter)}");
        }
        else
        {
            sb.AppendLine("Capacidad/espacio libre: no verificados (volumen de sistema sin asociar).");
        }

        sb.AppendLine();
        sb.AppendLine("INICIO / TEMPORALES");
        sb.AppendLine(
            $"Programas al inicio: {report.Startup.TotalCount} " +
            $"({report.Startup.ThirdPartyCount} de terceros)");
        sb.AppendLine(
            $"Temporales: {Num(report.TempFiles.TotalSizeGB)} GB " +
            $"({report.TempFiles.FileCount} archivos, {report.TempFiles.FolderCount} carpetas)");

        sb.AppendLine();
        sb.AppendLine("SEGURIDAD / MEMORIA VIRTUAL");
        sb.AppendLine(
            $"Antivirus: {Dash(report.Security.AntivirusName)} | " +
            $"Firewall: {(report.Security.FirewallActive ? "Activo" : "No detectado/inactivo")} | " +
            $"Windows Update: {Dash(report.Security.WindowsUpdateStatus)}");
        sb.AppendLine(
            $"Memoria virtual: {(report.VirtualMemory.IsAutoManaged ? "Auto-administrada" : "Manual")} | " +
            $"{Num(report.VirtualMemory.PagingFileSizeGB)} GB | " +
            $"ubicación: {Dash(report.VirtualMemory.Location)}");

        sb.AppendLine();
        sb.AppendLine($"ERRORES DE FUENTE ({errors.Count})");
        AppendBullets(sb, errors);
        sb.AppendLine();
        sb.AppendLine($"ADVERTENCIAS DE FUENTE ({warnings.Count})");
        AppendBullets(sb, warnings);

        sb.AppendLine();
        sb.AppendLine($"ALERTAS ({report.Alerts?.Count ?? 0})");
        sb.AppendLine("SEVERIDAD\tCATEGORIA\tMENSAJE\tRECOMENDACION");
        foreach (var alert in report.Alerts ?? new List<DiagnosticAlert>())
        {
            sb.AppendLine(
                $"{alert.Severity}\t{SanitizeCell(alert.Category)}\t" +
                $"{SanitizeCell(alert.Message)}\t{SanitizeCell(alert.Recommendation)}");
        }

        return sb.ToString();
    }

    // --- Helpers internos ---

    private void LoadResultsToUI(DiagnosticReport report)
    {
        // Sistema
        OsName = report.OsName;
        WindowsEdition = report.WindowsEdition;
        Architecture = report.Architecture;
        ComputerName = report.ComputerName;
        CurrentUser = report.CurrentUser;

        // Hardware
        Processor = report.Processor;
        RamInfo = $"{report.RamTotalGB} GB total";
        RamUsage = $"{report.RamUsedGB:F1} GB en uso ({report.RamUsagePercent}%)";

        // Disco
        DiskName = report.PrimaryDiskName;
        DiskType = report.DiskType;
        DiskCapacity = $"{report.DiskCapacityGB:F1} GB";
        DiskFree = $"{report.DiskFreeGB:F1} GB libres";
        DiskFreePercent = $"{report.DiskFreePercent}% libre";

        // Inicio
        StartupInfo = $"{report.Startup.TotalCount} programas al inicio";
        StartupThirdParty = $"{report.Startup.ThirdPartyCount} de terceros";

        // Temporales
        TempFilesInfo = $"{report.TempFiles.TotalSizeGB} GB ({report.TempFiles.FileCount} archivos)";

        // Seguridad
        AntivirusInfo = report.Security.AntivirusName;
        FirewallInfo = report.Security.FirewallActive ? "Activo" : "No detectado/inactivo";
        WindowsUpdateInfo = report.Security.WindowsUpdateStatus;

        // Memoria virtual
        VirtualMemoryInfo = report.VirtualMemory.IsAutoManaged
            ? $"Auto-administrado ({report.VirtualMemory.PagingFileSizeGB} GB)"
            : $"{report.VirtualMemory.PagingFileSizeGB} GB en {report.VirtualMemory.Location}";

        // Alertas
        Alerts.Clear();
        foreach (var alert in report.Alerts)
        {
            Alerts.Add(alert);
        }

        // Errores y advertencias reales por fuente (DiagnosticReport.Errors/Warnings).
        var issues = BuildSourceIssues(report);
        SourceIssues.Clear();
        foreach (var issue in issues)
        {
            SourceIssues.Add(issue);
        }

        HasSourceIssues = issues.Count > 0;
        IsPartial = (report.Errors?.Count ?? 0) > 0;
        TechnicalDetailText = BuildJson(report);
        HasTechnicalDetail = !string.IsNullOrWhiteSpace(TechnicalDetailText);
    }

    private static string ExtractSource(string issue)
    {
        // Los mensajes del servicio usan el prefijo "Fuente: ...".
        var separator = issue.IndexOf(':', StringComparison.Ordinal);
        return separator > 0
            ? issue[..separator].Trim()
            : "General";
    }

    private static void AppendBullets(StringBuilder sb, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            sb.AppendLine("- (sin registros)");
            return;
        }

        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item))
                sb.AppendLine($"- {item}");
        }
    }

    private static string SanitizeCell(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var sanitized = value.Replace('\t', ' ').Replace("\r", " ").Replace('\n', ' ').Trim();
        return sanitized;
    }

    private static string Dash(string value)
        => string.IsNullOrWhiteSpace(value) ? "N/D" : value.Trim();

    private static string Num(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reduce una excepción a un mensaje técnico acotado: el mensaje original
    /// puede incluir rutas privadas o nombres de usuario y no debe mostrarse.
    /// </summary>
    private static string SanitizeExceptionMessage(Exception exception)
        => exception switch
        {
            UnauthorizedAccessException => "acceso denegado.",
            IOException => "error de E/S.",
            JsonException => "respuesta inválida.",
            _ => "fallo no detallado."
        };

    private void ShowSuccess(string message)
    {
        StatusMessage = message;
        IsSuccess = true;
        HasError = false;
        ErrorMessage = string.Empty;
    }

    private void ShowError(string message)
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
        HasError = true;
        ErrorMessage = message;
    }

    private void ClearMessages()
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
        HasError = false;
        ErrorMessage = string.Empty;
    }
}
