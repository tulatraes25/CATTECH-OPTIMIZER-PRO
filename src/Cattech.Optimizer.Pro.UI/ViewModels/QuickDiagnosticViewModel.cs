using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de diagnóstico rápido.
/// </summary>
public partial class QuickDiagnosticViewModel : ObservableObject
{
    private readonly IDiagnosticService _diagnosticService;

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

    // --- Alertas ---

    public ObservableCollection<DiagnosticAlert> Alerts { get; } = new();

    // --- Diagnóstico actual ---

    private DiagnosticReport? _currentReport;

    public QuickDiagnosticViewModel(IDiagnosticService diagnosticService)
    {
        _diagnosticService = diagnosticService;
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
            StatusText = Alerts.Any(a => a.Severity == AlertSeverity.Critical)
                ? "Diagnóstico completado con advertencias"
                : Alerts.Count > 0
                    ? "Diagnóstico completado con advertencias"
                    : "Diagnóstico completado";
        }
        catch (Exception ex)
        {
            StatusText = "Error al diagnosticar";
            ShowError($"Error durante el diagnóstico: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Guarda el diagnóstico actual en disco.
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
        catch (Exception ex)
        {
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

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
    }

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
