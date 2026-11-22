using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private bool _hasResultErrors;

    [ObservableProperty]
    private bool _requiresRestart;

    [ObservableProperty]
    private bool _requiresSignOut;

    [ObservableProperty]
    private int _resultAppliedCount;

    [ObservableProperty]
    private int _resultSkippedCount;

    [ObservableProperty]
    private int _resultFailedCount;

    /// <summary>
    /// Errores reales devueltos por el servicio al aplicar los ajustes.
    /// </summary>
    public ObservableCollection<VisualOptimizationError> ResultErrors { get; } = new();

    public VisualOptimizationViewModel(IVisualOptimizationService optimizationService)
    {
        _optimizationService = optimizationService;
    }

    /// <summary>
    /// Analiza el estado actual de los ajustes visuales.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    private async Task AnalyzeAsync()
    {
        if (IsRunning) return;

        ClearMessages();
        IsRunning = true;
        HasResults = false;
        StatusText = "Analizando...";

        try
        {
            await AnalyzeCoreAsync();
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

    private bool CanAnalyze() => !IsRunning;

    /// <summary>
    /// Aplica los ajustes seleccionados.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (IsRunning) return;

        var selected = _settings
            .Where(s => s.IsSelected && !s.IsAlreadyOptimized && s.IsSupported)
            .ToList();

        if (selected.Count == 0)
        {
            ShowError("No hay ajustes seleccionados para aplicar.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        HasResult = false;
        HasResultErrors = false;
        ResultErrors.Clear();
        StatusText = "Aplicando...";

        try
        {
            var result = await _optimizationService.ApplyAsync(selected);

            // Re-analizar ANTES de publicar mensajes: el análisis reinicia el
            // estado de la vista y no debe borrar el resultado recién aplicado.
            string? reanalyzeError = null;
            try
            {
                await AnalyzeCoreAsync();
            }
            catch (Exception analyzeEx)
            {
                reanalyzeError = analyzeEx.Message;
                HasResults = false;
            }

            ResultAppliedCount = result.AppliedCount;
            ResultSkippedCount = result.SkippedCount;
            ResultFailedCount = result.FailedCount;
            RequiresRestart = result.RequiresRestart;
            RequiresSignOut = result.RequiresSignOut;

            ResultErrors.Clear();
            foreach (var error in result.Errors)
                ResultErrors.Add(error);
            HasResultErrors = ResultErrors.Count > 0;

            if (result.IsSuccess)
            {
                ResultSummary = $"Optimización completada: {result.AppliedCount} ajustes aplicados";
                ResultDetails = reanalyzeError is not null
                    ? $"Los cambios se aplicaron, pero no se pudo re-analizar el estado actual: {reanalyzeError}"
                    : result.RequiresRestart
                        ? "⚠️ Se requiere reinicio para que algunos cambios surtan efecto."
                        : result.RequiresSignOut
                            ? "⚠️ Se requiere cerrar sesión para que algunos cambios surtan efecto."
                            : "Los cambios ya deberían estar activos.";
                StatusText = "Optimización completada";
                ShowSuccess($"Optimización completada: {result.AppliedCount} ajustes aplicados.");
            }
            else
            {
                ResultSummary = $"Optimización con advertencias: {result.AppliedCount} aplicados, {result.FailedCount} fallidos";
                ResultDetails = reanalyzeError is not null
                    ? $"Advertencias durante la aplicación y falló el re-análisis: {reanalyzeError}"
                    : $"Fallaron {result.FailedCount} de {selected.Count} ajustes seleccionados.";
                var detalle = string.Join(
                    Environment.NewLine,
                    ResultErrors.Take(5).Select(e => $"• {e.SettingName}: {e.Message}"));
                ShowError(string.IsNullOrWhiteSpace(detalle)
                    ? "La optimización terminó con advertencias."
                    : $"La optimización terminó con advertencias:{Environment.NewLine}{detalle}");
                StatusText = "Optimización completada con advertencias";
            }

            HasResult = true;
        }
        catch (Exception ex)
        {
            StatusText = "Error al aplicar";
            ShowError($"Error al aplicar los ajustes: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanApply()
        => !IsRunning
           && HasResults
           && _settings.Any(s => s.IsSelected && !s.IsAlreadyOptimized && s.IsSupported);

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
    /// Muestra/oculta la lista de backups y la recarga al abrirla.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadBackups))]
    private async Task LoadBackupsAsync()
    {
        if (IsRunning) return;

        if (ShowBackups)
        {
            ShowBackups = false;
            return;
        }

        ShowBackups = true;
        await RefreshBackupsAsync();
    }

    private bool CanLoadBackups() => !IsRunning;

    /// <summary>
    /// Restaura un backup. El backup llega como parámetro del comando
    /// (el panel de backups no tiene selección previa obligatoria).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRestoreBackup))]
    private async Task RestoreBackupAsync(VisualOptimizationBackup? backup)
    {
        if (IsRunning) return;

        var target = backup ?? SelectedBackup;
        if (target is null)
        {
            ShowError("Seleccioná un backup para restaurar.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = $"Restaurando '{target.SettingName}'...";

        try
        {
            var restoreResult = await _optimizationService.RestoreWithResultAsync(target);

            // Re-analizar y recargar backups ANTES de publicar mensajes.
            try
            {
                await AnalyzeCoreAsync();
            }
            catch (Exception analyzeEx)
            {
                HasResults = false;
                ResultDetails = $"No se pudo re-analizar el estado actual: {analyzeEx.Message}";
            }

            await RefreshBackupsAsync();

            if (restoreResult.Success)
            {
                StatusText = "Ajuste restaurado";
                ShowSuccess(string.IsNullOrWhiteSpace(restoreResult.Message)
                    ? $"Ajuste '{target.SettingName}' restaurado correctamente."
                    : restoreResult.Message);
            }
            else
            {
                StatusText = "Error al restaurar";
                ShowError(string.IsNullOrWhiteSpace(restoreResult.Message)
                    ? "No se pudo restaurar el ajuste desde el backup."
                    : restoreResult.Message);
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al restaurar";
            ShowError($"Error durante la restauración: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRestoreBackup(VisualOptimizationBackup? backup)
        => !IsRunning && backup is { CanRestore: true };

    partial void OnSelectedBackupChanged(VisualOptimizationBackup? value)
    {
        HasSelectedBackup = value != null && value.CanRestore;
        RestoreBackupCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCountChanged(int value)
    {
        ApplyCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasResultsChanged(bool value)
    {
        ApplyCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsRunningChanged(bool value)
    {
        AnalyzeCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        LoadBackupsCommand.NotifyCanExecuteChanged();
        RestoreBackupCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Reemplaza la lista de ajustes manteniendo las suscripciones de selección.
    /// </summary>
    private void ReplaceSettings(IEnumerable<VisualOptimizationSetting> settings)
    {
        foreach (var previous in _settings)
            previous.PropertyChanged -= OnSettingPropertyChanged;

        _settings = settings.ToList();
        Settings.Clear();
        foreach (var setting in _settings)
        {
            setting.PropertyChanged += OnSettingPropertyChanged;
            Settings.Add(setting);
        }

        UpdateSelectedCount();
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(VisualOptimizationSetting.IsSelected):
                UpdateSelectedCount();
                break;

            case nameof(VisualOptimizationSetting.IsAlreadyOptimized):
                AlreadyOptimizedCount = _settings.Count(s => s.IsAlreadyOptimized);
                break;
        }
    }

    /// <summary>
    /// Analiza sin publicar mensajes: quien llama decide cuándo mostrarlos.
    /// </summary>
    private async Task AnalyzeCoreAsync()
    {
        var analyzed = await _optimizationService.AnalyzeAsync();
        ReplaceSettings(analyzed);
        AlreadyOptimizedCount = _settings.Count(s => s.IsAlreadyOptimized);
        HasResults = true;
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = _settings.Count(s => s.IsSelected);
    }

    /// <summary>
    /// Recarga la lista de backups sin ocultar el panel ni tocar los mensajes.
    /// </summary>
    private async Task RefreshBackupsAsync()
    {
        try
        {
            var backups = await _optimizationService.ListBackupsAsync();
            Backups.Clear();
            foreach (var backup in backups)
                Backups.Add(backup);
            SelectedBackup = null;
        }
        catch (Exception ex)
        {
            // No se muestra "No hay backups" en silencio ante un archivo dañado.
            StatusText = "Error al leer backups";
            ShowError($"No se pudieron leer los backups: {ex.Message}");
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
