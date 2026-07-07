using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de optimización visual segura.
/// </summary>
public partial class VisualOptimizationViewModel : ObservableObject
{
    private readonly IVisualOptimizationService _optimizationService;
    private List<VisualOptimizationSetting> _settings = [];

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin analizar";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Settings ---

    public ObservableCollection<VisualOptimizationSetting> Settings { get; } = new();

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private int _alreadyOptimizedCount;

    // --- Backups ---

    public ObservableCollection<VisualOptimizationBackup> Backups { get; } = new();

    [ObservableProperty]
    private bool _showBackups;

    [ObservableProperty]
    private VisualOptimizationBackup? _selectedBackup;

    [ObservableProperty]
    private bool _hasSelectedBackup;

    // --- Resultado ---

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _resultDetails = string.Empty;

    [ObservableProperty]
    private bool _requiresRestart;

    [ObservableProperty]
    private bool _requiresSignOut;

    public VisualOptimizationViewModel(IVisualOptimizationService optimizationService)
    {
        _optimizationService = optimizationService;
    }

    /// <summary>
    /// Analiza el estado actual de los ajustes visuales.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        ClearMessages();
        IsRunning = true;
        HasResults = false;
        StatusText = "Analizando...";

        try
        {
            _settings = await _optimizationService.AnalyzeAsync();

            Settings.Clear();
            foreach (var setting in _settings)
                Settings.Add(setting);

            AlreadyOptimizedCount = _settings.Count(s => s.IsAlreadyOptimized);
            UpdateSelectedCount();
            HasResults = true;
            StatusText = "Listo para aplicar";
        }
        catch (Exception ex)
        {
            StatusText = "Error al analizar";
            ShowError($"Error durante el análisis: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Aplica los ajustes seleccionados.
    /// </summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        var selected = _settings.Where(s => s.IsSelected && !s.IsAlreadyOptimized).ToList();
        if (selected.Count == 0)
        {
            ShowError("No hay ajustes seleccionados para aplicar.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Aplicando...";

        try
        {
            var result = await _optimizationService.ApplyAsync(selected);

            ResultAppliedCount = result.AppliedCount;
            ResultSkippedCount = result.SkippedCount;
            ResultFailedCount = result.FailedCount;
            RequiresRestart = result.RequiresRestart;
            RequiresSignOut = result.RequiresSignOut;

            if (result.IsSuccess)
            {
                ResultSummary = $"Optimización completada: {result.AppliedCount} ajustes aplicados";
                StatusText = "Optimización completada";
            }
            else
            {
                ResultSummary = $"Optimización con advertencias: {result.AppliedCount} aplicados, {result.FailedCount} fallidos";
                StatusText = "Optimización completada con advertencias";
            }

            if (result.RequiresRestart)
                ResultDetails = "⚠️ Se requiere reinicio para que algunos cambios surtan efecto.";
            else if (result.RequiresSignOut)
                ResultDetails = "⚠️ Se requiere cerrar sesión para que algunos cambios surtan efecto.";
            else
                ResultDetails = "Los cambios ya deberían estar activos.";

            HasResult = true;
            IsSuccess = result.IsSuccess;
            HasError = !result.IsSuccess;

            // Re-analizar para ver valores actualizados
            await AnalyzeAsync();
        }
        catch (Exception ex)
        {
            StatusText = "Error al aplicar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Selecciona/deselecciona un ajuste.
    /// </summary>
    [RelayCommand]
    private void ToggleSetting(VisualOptimizationSetting? setting)
    {
        if (setting == null || setting.IsAlreadyOptimized) return;
        setting.IsSelected = !setting.IsSelected;
        UpdateSelectedCount();
    }

    /// <summary>
    /// Selecciona todos los ajustes de bajo riesgo no optimizados.
    /// </summary>
    [RelayCommand]
    private void SelectAllSafe()
    {
        foreach (var setting in _settings)
        {
            if (!setting.IsAlreadyOptimized && setting.RiskLevel == VisualRiskLevel.Low)
                setting.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Deselecciona todos los ajustes.
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var setting in _settings)
            setting.IsSelected = false;
        UpdateSelectedCount();
    }

    /// <summary>
    /// Muestra/oculta la lista de backups.
    /// </summary>
    [RelayCommand]
    private async Task LoadBackupsAsync()
    {
        ShowBackups = !ShowBackups;

        if (ShowBackups)
        {
            Backups.Clear();
            var backups = await _optimizationService.ListBackupsAsync();
            foreach (var backup in backups)
                Backups.Add(backup);
        }
    }

    partial void OnSelectedBackupChanged(VisualOptimizationBackup? value)
    {
        HasSelectedBackup = value != null && value.CanRestore;
    }

    /// <summary>
    /// Restaura un backup seleccionado.
    /// </summary>
    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (SelectedBackup == null) return;

        ClearMessages();
        IsRunning = true;

        try
        {
            var success = await _optimizationService.RestoreAsync(SelectedBackup);

            if (success)
            {
                ShowSuccess($"Ajuste '{SelectedBackup.SettingName}' restaurado correctamente.");
                await LoadBackupsAsync();
                await AnalyzeAsync();
            }
            else
            {
                ShowError("Error al restaurar el ajuste.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error durante la restauración: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = _settings.Count(s => s.IsSelected);
    }

    // --- Campos para propiedades de resultado ---

    [ObservableProperty]
    private int _resultAppliedCount;

    [ObservableProperty]
    private int _resultSkippedCount;

    [ObservableProperty]
    private int _resultFailedCount;

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
