using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de limpieza segura de temporales.
/// </summary>
public partial class TempCleanupViewModel : ObservableObject
{
    private readonly ITempCleanupService _cleanupService;
    private List<TempCleanupTarget> _targets = [];

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin escanear";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isCleaning;

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private bool _hasScanned;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Targets ---

    public ObservableCollection<TempCleanupTarget> Targets { get; } = new();

    [ObservableProperty]
    private int _selectedTargetsCount;

    [ObservableProperty]
    private string _totalSelectedSize = "0 MB";

    // --- Resultado ---

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _resultDetails = string.Empty;

    [ObservableProperty]
    private int _resultDeletedFiles;

    [ObservableProperty]
    private string _resultDeletedSize = "0 MB";

    [ObservableProperty]
    private int _resultSkippedFiles;

    [ObservableProperty]
    private int _resultFailedFiles;

    public ObservableCollection<CleanupError> ResultErrors { get; } = new();

    public TempCleanupViewModel(ITempCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    /// <summary>
    /// Escanea las ubicaciones de temporales.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        ClearMessages();
        IsScanning = true;
        HasScanned = false;
        HasResult = false;
        StatusText = "Escaneando...";

        var progress = new Progress<int>(percent => ProgressPercent = percent);

        try
        {
            _targets = await _cleanupService.ScanAsync(progress);

            Targets.Clear();
            foreach (var target in _targets)
                Targets.Add(target);

            HasScanned = true;
            UpdateSelectedCount();
            StatusText = "Listo para limpiar";
        }
        catch (Exception ex)
        {
            StatusText = "Error al escanear";
            ShowError($"Error durante el escaneo: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
            IsRunning = false;
        }
    }

    /// <summary>
    /// Limpia las ubicaciones seleccionadas.
    /// </summary>
    [RelayCommand]
    private async Task CleanupAsync()
    {
        var selected = _targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowError("No hay ubicaciones seleccionadas para limpiar.");
            return;
        }

        ClearMessages();
        IsCleaning = true;
        StatusText = "Limpiando...";

        var progress = new Progress<int>(percent => ProgressPercent = percent);

        try
        {
            var result = await _cleanupService.CleanupAsync(selected, progress);

            // Mostrar resultado
            ResultDeletedFiles = result.DeletedFiles;
            ResultDeletedSize = $"{result.DeletedMB} MB";
            ResultSkippedFiles = result.SkippedFiles;
            ResultFailedFiles = result.FailedFiles;

            ResultErrors.Clear();
            foreach (var error in result.Errors)
                ResultErrors.Add(error);

            if (result.IsSuccess)
            {
                ResultSummary = $"Limpieza completada: {result.DeletedMB} MB liberados, {result.DeletedFiles} archivos eliminados";
                StatusText = "Limpieza completada";
            }
            else
            {
                ResultSummary = $"Limpieza completada con advertencias: {result.DeletedMB} MB liberados, {result.SkippedFiles} omitidos, {result.FailedFiles} con error";
                StatusText = "Limpieza completada con advertencias";
            }

            ResultDetails = $"Duración: {result.Duration.TotalSeconds:F1} segundos";

            HasResult = true;
            IsSuccess = result.IsSuccess;
            HasError = !result.IsSuccess;

            // Guardar resultado
            await _cleanupService.SaveResultAsync(result);
        }
        catch (Exception ex)
        {
            StatusText = "Error durante la limpieza";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsCleaning = false;
            IsRunning = false;
        }
    }

    /// <summary>
    /// Selecciona/deselecciona un target.
    /// </summary>
    [RelayCommand]
    private void ToggleTarget(TempCleanupTarget? target)
    {
        if (target == null) return;
        target.IsSelected = !target.IsSelected;
        UpdateSelectedCount();
    }

    /// <summary>
    /// Selecciona todos los targets de bajo riesgo.
    /// </summary>
    [RelayCommand]
    private void SelectAllLowRisk()
    {
        foreach (var target in _targets)
        {
            if (target.IsAccessible && target.RiskLevel == CleanupRiskLevel.Low)
                target.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Deselecciona todos los targets.
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var target in _targets)
            target.IsSelected = false;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedTargetsCount = _targets.Count(t => t.IsSelected);
        var totalBytes = _targets.Where(t => t.IsSelected).Sum(t => t.EstimatedSizeBytes);
        TotalSelectedSize = totalBytes > 1024 * 1024 * 1024
            ? $"{totalBytes / (1024.0 * 1024 * 1024):F2} GB"
            : $"{totalBytes / (1024.0 * 1024):F1} MB";
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
