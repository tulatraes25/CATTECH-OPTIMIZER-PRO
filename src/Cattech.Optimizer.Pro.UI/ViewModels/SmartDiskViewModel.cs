using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de Discos SMART.
/// </summary>
public partial class SmartDiskViewModel : ObservableObject
{
    private readonly ISmartctlRunner _smartctlRunner;
    private readonly ISmartDiskService _smartDiskService;
    private List<SmartDiskDevice> _allDevices = [];
    private List<SmartDiskReport> _allReports = [];

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin verificar";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Estado de smartctl ---

    [ObservableProperty]
    private bool _smartctlChecked;

    [ObservableProperty]
    private bool _smartctlAvailable;

    [ObservableProperty]
    private string _smartctlPath = string.Empty;

    [ObservableProperty]
    private string _smartctlVersion = string.Empty;

    [ObservableProperty]
    private bool _smartctlSupportsJson;

    [ObservableProperty]
    private string _smartctlErrorMessage = string.Empty;

    // --- Discos ---

    [ObservableProperty]
    private bool _hasDevices;

    [ObservableProperty]
    private bool _hasResults;

    // --- Resumen ---

    [ObservableProperty]
    private string _summaryTotal = "0";

    [ObservableProperty]
    private string _summaryGood = "0";

    [ObservableProperty]
    private string _summaryWarning = "0";

    [ObservableProperty]
    private string _summaryCritical = "0";

    [ObservableProperty]
    private string _summaryNotAvailable = "0";

    [ObservableProperty]
    private int _summaryWarningCount;

    [ObservableProperty]
    private int _summaryCriticalCount;

    // --- Reporte seleccionado ---

    [ObservableProperty]
    private SmartDiskReport? _selectedReport;

    [ObservableProperty]
    private bool _hasSelectedReport;

    // --- Listas ---

    public ObservableCollection<SmartDiskDevice> Devices { get; } = new();
    public ObservableCollection<SmartDiskReport> Reports { get; } = new();
    public ObservableCollection<SmartAttribute> SelectedAttributes { get; } = new();

    public SmartDiskViewModel(ISmartctlRunner smartctlRunner, ISmartDiskService smartDiskService)
    {
        _smartctlRunner = smartctlRunner;
        _smartDiskService = smartDiskService;
    }

    /// <summary>
    /// Verifica si smartctl está disponible.
    /// </summary>
    [RelayCommand]
    private async Task CheckSmartctlAsync()
    {
        ClearMessages();
        IsRunning = true;
        StatusText = "Verificando smartctl...";

        try
        {
            var availability = await _smartctlRunner.CheckAvailabilityAsync();

            SmartctlChecked = true;
            SmartctlAvailable = availability.IsAvailable;
            SmartctlPath = availability.SmartctlPath;
            SmartctlVersion = availability.Version;
            SmartctlSupportsJson = availability.SupportsJson;
            SmartctlErrorMessage = availability.ErrorMessage;

            StatusText = availability.IsAvailable ? "smartctl disponible" : "smartctl no disponible";
        }
        catch (Exception ex)
        {
            SmartctlChecked = true;
            SmartctlAvailable = false;
            SmartctlErrorMessage = ex.Message;
            StatusText = "Error al verificar";
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Detecta discos disponibles.
    /// </summary>
    [RelayCommand]
    private async Task DetectDevicesAsync()
    {
        if (!SmartctlAvailable)
        {
            ShowError("smartctl no está disponible. Verifique la configuración.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Detectando discos...";

        try
        {
            _allDevices = (await _smartctlRunner.ListDevicesAsync()).ToList();

            Devices.Clear();
            foreach (var device in _allDevices)
                Devices.Add(device);

            HasDevices = Devices.Count > 0;
            StatusText = Devices.Count > 0
                ? $"{Devices.Count} disco(s) detectado(s)"
                : "No se encontraron discos";

            if (Devices.Count == 0)
            {
                ShowError("No se encontraron dispositivos de almacenamiento. Verifique que smartctl tenga permisos.");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al detectar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Ejecuta análisis SMART de todos los discos.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeSmartAsync()
    {
        if (!SmartctlAvailable)
        {
            ShowError("smartctl no está disponible.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Analizando SMART...";

        try
        {
            var result = await _smartDiskService.AnalyzeAllDisksAsync();

            _allReports = result.Reports.ToList();

            Reports.Clear();
            foreach (var report in _allReports)
                Reports.Add(report);

            // Calcular resumen
            var good = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Good);
            var warning = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Warning);
            var critical = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.Critical);
            var notAvailable = _allReports.Count(r => r.HealthStatus == SmartHealthStatus.NotAvailable);

            SummaryTotal = result.DevicesAnalyzed.ToString();
            SummaryGood = good.ToString();
            SummaryWarning = warning.ToString();
            SummaryCritical = critical.ToString();
            SummaryNotAvailable = notAvailable.ToString();
            SummaryWarningCount = warning;
            SummaryCriticalCount = critical;

            HasResults = true;
            HasDevices = Reports.Count > 0;

            if (critical > 0)
            {
                StatusText = $"Análisis completado: {critical} disco(s) crítico(s)";
                ShowError($"⚠️ {critical} disco(s) en estado CRÍTICO. Backup inmediato recomendado.");
            }
            else if (warning > 0)
            {
                StatusText = $"Análisis completado: {warning} advertencia(s)";
                ShowSuccess($"Análisis completado. {warning} disco(s) con advertencias.");
            }
            else
            {
                StatusText = "Análisis completado";
                ShowSuccess("Todos los discos en buen estado.");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al analizar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Guarda el resultado del análisis.
    /// </summary>
    [RelayCommand]
    private async Task SaveAnalysisAsync()
    {
        if (_allReports.Count == 0)
        {
            ShowError("No hay análisis para guardar.");
            return;
        }

        ClearMessages();

        try
        {
            var result = new SmartAnalysisResult
            {
                SmartctlAvailable = SmartctlAvailable,
                SmartctlVersion = SmartctlVersion,
                DevicesAnalyzed = _allReports.Count,
                Reports = _allReports,
                StartedAt = DateTime.Now,
                FinishedAt = DateTime.Now
            };

            var fileName = await _smartDiskService.SaveResultAsync(result);
            ShowSuccess($"Análisis guardado: {fileName}");
        }
        catch (Exception ex)
        {
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

    /// <summary>
    /// Selecciona un reporte para ver detalle.
    /// </summary>
    [RelayCommand]
    private void SelectReport(SmartDiskReport? report)
    {
        SelectedReport = report;
        HasSelectedReport = report != null;

        SelectedAttributes.Clear();
        if (report?.ImportantAttributes != null)
        {
            foreach (var attr in report.ImportantAttributes)
                SelectedAttributes.Add(attr);
        }
    }

    partial void OnSelectedReportChanged(SmartDiskReport? value)
    {
        HasSelectedReport = value != null;
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
