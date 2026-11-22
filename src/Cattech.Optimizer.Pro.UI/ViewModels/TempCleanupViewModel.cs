using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private readonly string _resultsDirectory;
    private readonly Action<string> _openDirectory;
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

    // --- Historial técnico persistido ---

    public ObservableCollection<CleanupHistoryRow> HistoryRows { get; } = new();

    [ObservableProperty]
    private bool _hasHistory;

    [ObservableProperty]
    private bool _isLoadingHistory;

    [ObservableProperty]
    private string _historyStatusText = "Sin registros cargados";

    public TempCleanupViewModel(
        ITempCleanupService cleanupService,
        string? baseDirectory = null,
        Action<string>? openDirectory = null)
    {
        _cleanupService = cleanupService;

        var root = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : baseDirectory;
        _resultsDirectory = Path.Combine(root, "data", "cleanup-results");
        _openDirectory = openDirectory ?? OpenDirectoryWithShell;
    }

    public bool CanReviewCleanupConsent =>
        HasScanned && !IsRunning && SelectedTargetsCount > 0;

    // Never accept consent for a different scope after a selection/path change.
    public bool CanStartCleanup => CanReviewCleanupConsent && HasCleanupConsent
        && _approvedScope == CurrentSelectionScope();

    /// <summary>
    /// Confirmación explícita provista por la vista (diálogo modal). Cuando no
    /// hay host de UI (tests), se exige la casilla HasCleanupConsent.
    /// </summary>
    public Func<string, bool>? ConfirmCleanupRequest { get; set; }

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
            var scanned = await _cleanupService.ScanAsync(progress);
            // Refuse a malformed/incomplete provider snapshot before making
            // any row actionable. An overflow in selected-size accounting must
            // likewise revoke the whole scan, not leave HasScanned = true.
            if (scanned is null || scanned.Any(target =>
                target is null || target.EstimatedSizeBytes < 0
                || target.EstimatedFileCount < 0))
                throw new InvalidOperationException("Invalid cleanup scan snapshot.");

            _targets = scanned;
            Targets.Clear();
            foreach (var target in _targets)
                Targets.Add(target);

            UpdateSelectedCount();
            HasScanned = true;
            var candidates = _targets.ToArray();
            StatusText = candidates.All(t => !t.IsAccessible)
                ? "Escaneo sin ubicaciones disponibles"
                : candidates.Any(t => !t.IsAccessible)
                    ? "Escaneo completado con ubicaciones no disponibles"
                    : "Listo para limpiar";
        }
        catch (Exception)
        {
            // Fail closed after ANY partial scan/UI update, including a
            // malformed provider response or overflowing summary. The user
            // must rescan and reapprove; no stale target survives.
            HasScanned = false;
            HasCleanupConsent = false;
            _targets = [];
            Targets.Clear();
            UpdateSelectedCount();
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
    /// Flujo de un solo clic usado por la UI: valida, pide confirmación con los
    /// números reales de la selección y recién entonces borra. La confirmación
    /// del diálogo es el consentimiento explícito para ESA selección exacta.
    /// </summary>
    [RelayCommand]
    private async Task CleanupWithConfirmationAsync()
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

        var files = selected.Sum(t => (long)t.EstimatedFileCount);
        var bytes = selected.Sum(t => t.EstimatedSizeBytes);
        var message =
            $"¿Eliminar {files} archivos ({FormatSize(bytes)}) de las ubicaciones seleccionadas?\n\n" +
            string.Join("\n", selected.Select(t => "• " + t.DisplayName)) +
            "\n\nEsta acción no crea respaldo y no se puede deshacer.";

        if (ConfirmCleanupRequest is not null && !ConfirmCleanupRequest(message))
            return;

        // The dialog is the explicit approval for the exact current selection.
        HasCleanupConsent = true;
        if (_approvedScope != CurrentSelectionScope())
        {
            HasCleanupConsent = false;
            ShowError("La selección cambió antes de confirmar. Volvé a intentar la limpieza.");
            return;
        }

        await CleanupAsync();
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        if (bytes >= 1024L * 1024)
            return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} bytes";
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
                await LoadHistoryCoreAsync();
                ShowError("La limpieza se ejecutó, pero no se pudo guardar su historial. Conservá los datos del resultado.");
                return;
            }

            // Después de limpiar, el historial técnico se recarga con el nuevo registro.
            await LoadHistoryCoreAsync();

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

        if (!target.IsScanned || !target.IsAccessible)
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

    // =====================
    // HISTORIAL TÉCNICO
    // =====================

    /// <summary>
    /// Carga el historial de limpiezas persistidas (data/cleanup-results).
    /// Solo lectura: no ejecuta ni repite limpiezas.
    /// </summary>
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (IsRunning)
            return;

        await LoadHistoryCoreAsync();
    }

    /// <summary>
    /// Carga el historial sin la guarda de IsRunning: se usa también al final
    /// de una limpieza (la operación sigue marcada como en curso hasta el finally).
    /// </summary>
    private async Task LoadHistoryCoreAsync()
    {
        IsLoadingHistory = true;

        try
        {
            var results = await _cleanupService.ListResultsAsync(20);

            HistoryRows.Clear();
            foreach (var summary in results)
            {
                if (summary is null)
                    continue;
                HistoryRows.Add(new CleanupHistoryRow(summary));
            }

            HasHistory = HistoryRows.Count > 0;
            HistoryStatusText = HasHistory
                ? $"{HistoryRows.Count} registro(s) de limpieza"
                : "Sin registros de limpieza guardados.";
        }
        catch (Exception)
        {
            HistoryRows.Clear();
            HasHistory = false;
            HistoryStatusText = "No se pudo leer el historial de limpieza.";
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    /// <summary>
    /// Abre la carpeta data/cleanup-results (la crea si todavía no existe).
    /// </summary>
    [RelayCommand]
    private Task OpenResultsFolderAsync()
    {
        if (IsRunning)
        {
            ShowError("Esperá a que termine la operación en curso.");
            return Task.CompletedTask;
        }

        try
        {
            var directory = EnsureResultsDirectory();
            _openDirectory(directory);
            ShowSuccess("Carpeta de resultados de limpieza abierta.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException
            or System.ComponentModel.Win32Exception)
        {
            // Aviso visible sin exponer rutas privadas en el banner.
            ShowError("No se pudo abrir la carpeta de resultados. Verificá los permisos e intentá nuevamente.");
        }

        return Task.CompletedTask;
    }

    internal string ResultsDirectoryPath => _resultsDirectory;

    internal string EnsureResultsDirectory()
    {
        Directory.CreateDirectory(_resultsDirectory);
        return Path.GetFullPath(_resultsDirectory);
    }

    private static void OpenDirectoryWithShell(string directory)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
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

/// <summary>
/// Fila de presentación del historial técnico de limpiezas.
/// Solo deriva texto desde el resumen persistido: no ejecuta limpiezas.
/// </summary>
public sealed class CleanupHistoryRow
{
    public CleanupHistoryRow(TempCleanupResultSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        StartedAt = summary.StartedAt;
        DeletedMB = summary.DeletedMB;
        DeletedFiles = summary.DeletedFiles;
        HasWarnings = summary.HasWarnings;
        FileName = summary.FileName;
    }

    public DateTime StartedAt { get; }

    public double DeletedMB { get; }

    public int DeletedFiles { get; }

    public bool HasWarnings { get; }

    public string FileName { get; }

    public string StartedAtDisplay =>
        StartedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public string DeletedMBDisplay =>
        DeletedMB.ToString("0.##", CultureInfo.InvariantCulture) + " MB";

    public string DeletedFilesDisplay =>
        DeletedFiles.ToString(CultureInfo.InvariantCulture);

    public string WarningsDisplay => HasWarnings ? "Sí" : "No";

    public string FileNameDisplay => string.IsNullOrWhiteSpace(FileName) ? "-" : FileName;
}
