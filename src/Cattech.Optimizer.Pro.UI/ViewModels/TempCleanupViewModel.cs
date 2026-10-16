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
    private string? _approvedScope;

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

    // The user must affirm the current selection before any destructive action.
    [ObservableProperty]
    private bool _hasCleanupConsent;

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

    public bool CanReviewCleanupConsent =>
        HasScanned && !IsRunning && SelectedTargetsCount > 0;

    // Never accept consent for a different scope after a selection/path change.
    public bool CanStartCleanup => CanReviewCleanupConsent && HasCleanupConsent
        && _approvedScope == CurrentSelectionScope();

    partial void OnHasScannedChanged(bool value) => RefreshConsentAvailability();
    partial void OnIsRunningChanged(bool value) => RefreshConsentAvailability();
    partial void OnSelectedTargetsCountChanged(int value) => RefreshConsentAvailability();

    partial void OnHasCleanupConsentChanged(bool value)
    {
        _approvedScope = value ? CurrentSelectionScope() : null;
        OnPropertyChanged(nameof(CanStartCleanup));
    }

    private void RefreshConsentAvailability()
    {
        OnPropertyChanged(nameof(CanReviewCleanupConsent));
        OnPropertyChanged(nameof(CanStartCleanup));
    }

    private string CurrentSelectionScope() => string.Join("\n", _targets
        .Where(target => target.IsSelected)
        .OrderBy(target => target.Id, StringComparer.Ordinal)
        .Select(target => target.Id + "\0" + target.Path));

    /// <summary>
    /// Escanea las ubicaciones de temporales.
    /// </summary>
    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsRunning)
            return;

        HasCleanupConsent = false;
        IsRunning = true;
        ClearMessages();
        IsScanning = true;
        HasScanned = false;
        HasResult = false;
        // A refresh is not permitted to keep stale approved target rows.
        // This also clears selections if the new scan throws or is canceled.
        _targets = [];
        Targets.Clear();
        UpdateSelectedCount();
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
            // RECYCLE_BIN is intentionally unavailable; don't treat its
            // mandatory opt-out as a failure of otherwise healthy scans.
            var candidates = _targets.Where(t => t.Id != "RECYCLE_BIN").ToArray();
            StatusText = candidates.All(t => !t.IsAccessible)
                ? "Escaneo sin ubicaciones disponibles"
                : candidates.Any(t => !t.IsAccessible)
                    ? "Escaneo completado con ubicaciones no disponibles"
                    : "Listo para limpiar";
        }
        catch (Exception)
        {
            StatusText = "Error al escanear";
            // Paths, user names and provider exception text are not safe
            // to display in a technician/client-facing status message.
            ShowError("No se pudo completar el escaneo. Revisá los permisos y las ubicaciones.");
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
        if (IsRunning)
            return;

        if (!HasScanned)
        {
            ShowError("Primero escaneá las ubicaciones antes de limpiar.");
            return;
        }

        var selected = _targets.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowError("No hay ubicaciones seleccionadas para limpiar.");
            return;
        }

        // Fail closed when invoked directly, even if XAML incorrectly enables
        // the button or the approved selection changed after confirmation.
        if (!CanStartCleanup)
        {
            HasCleanupConsent = false;
            ShowError("Confirmá la limpieza de las ubicaciones seleccionadas antes de borrar.");
            return;
        }

        IsRunning = true;
        ClearMessages();
        HasResult = false;
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

            var fullyCompleted = result.IsSuccess && !result.HasWarnings;
            if (fullyCompleted)
            {
                ResultSummary = $"Limpieza completada: {result.DeletedMB} MB liberados, {result.DeletedFiles} archivos eliminados";
                StatusText = "Limpieza completada";
            }
            else if (result.IsSuccess)
            {
                ResultSummary = $"Limpieza completada con advertencias: {result.DeletedMB} MB liberados, {result.SkippedFiles} omitidos, {result.FailedFiles} con error";
                StatusText = "Limpieza completada con advertencias";
            }
            else
            {
                ResultSummary = $"Limpieza incompleta: {result.DeletedMB} MB liberados, {result.SkippedFiles} omitidos, {result.FailedFiles} con error";
                StatusText = "Limpieza incompleta";
            }

            ResultDetails = $"Duración: {result.Duration.TotalSeconds:F1} segundos";
            HasResult = true;

            // Distinguish destructive operation outcomes from audit persistence.
            // A failed SaveResultAsync must never turn a completed deletion
            // into the misleading message "Error durante la limpieza".
            try
            {
                await _cleanupService.SaveResultAsync(result);
            }
            catch (Exception)
            {
                ResultSummary += " El registro técnico NO pudo guardarse.";
                StatusText = "Limpieza ejecutada; registro no guardado";
                ShowError("La limpieza se ejecutó, pero no se pudo guardar su historial. Conservá los datos del resultado.");
                return;
            }

            if (fullyCompleted)
                ShowSuccess("La limpieza se completó y el registro se guardó.");
            else if (!result.IsSuccess)
                ShowError("La limpieza no se completó: revisá los errores y archivos omitidos.");
            else
            {
                // Skipped files are warnings, not a full green success.
                IsSuccess = false;
                HasError = false;
            }
        }
        catch (Exception)
        {
            StatusText = "Error durante la limpieza";
            ShowError("No se pudo completar la limpieza. Revisá el estado del equipo.");
        }
        finally
        {
            HasCleanupConsent = false; // A second deletion needs fresh approval.
            IsCleaning = false;
            IsRunning = false;
        }
    }

    /// <summary>
    /// Reacts to a CheckBox selection change. The TwoWay binding has already
    /// assigned IsSelected; inverting it again would silently undo the click.
    /// </summary>
    [RelayCommand]
    private void ToggleTarget(TempCleanupTarget? target)
    {
        if (target is null || IsRunning)
            return;

        if (!target.IsScanned || !target.IsAccessible || target.Id == "RECYCLE_BIN")
            target.IsSelected = false;

        UpdateSelectedCount();
    }

    /// <summary>
    /// Selecciona todos los targets de bajo riesgo.
    /// </summary>
    [RelayCommand]
    private void SelectAllLowRisk()
    {
        if (IsRunning || !HasScanned)
            return;

        foreach (var target in _targets)
        {
            // Even if a system target is categorized Low, it must require
            // a deliberate manual selection, never a bulk low-risk action.
            if (target.IsScanned && target.IsAccessible && !target.IsSystemLocation
                && target.RiskLevel == CleanupRiskLevel.Low
                && target.Id != "RECYCLE_BIN")
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
        if (IsRunning)
            return;

        foreach (var target in _targets)
            target.IsSelected = false;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        HasCleanupConsent = false;
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
