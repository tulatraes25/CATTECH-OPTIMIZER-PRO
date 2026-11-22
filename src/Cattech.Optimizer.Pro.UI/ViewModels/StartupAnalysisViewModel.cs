using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de análisis de programas de inicio.
/// Incluye desactivación segura con backup y reversión.
/// </summary>
public partial class StartupAnalysisViewModel : ObservableObject
{
    private readonly IStartupService _startupService;
    private List<StartupEntry> _allEntries = [];

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

    // --- Filtros ---

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todos";

    public string[] Filters { get; } =
    [
        "Todos",
        "No Microsoft",
        "Recomendados para revisar",
        "Posible desactivar",
        "Alertas"
    ];

    // --- Resumen ---

    [ObservableProperty]
    private string _summaryTotal = "0";

    [ObservableProperty]
    private string _summaryMicrosoft = "0";

    [ObservableProperty]
    private string _summaryThirdParty = "0";

    [ObservableProperty]
    private string _summaryPossibleDisable = "0";

    [ObservableProperty]
    private string _summaryAlerts = "0";

    // --- Entrada seleccionada ---

    [ObservableProperty]
    private StartupEntry? _selectedEntry;

    [ObservableProperty]
    private bool _hasSelectedEntry;

    // --- Selección para desactivación ---

    public ObservableCollection<SelectableStartupEntry> SelectableEntries { get; } = new();

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _hasSelectableEntries;

    [ObservableProperty]
    private bool _isDisabling;

    [ObservableProperty]
    private string _disableReason = string.Empty;

    // --- Backups ---

    public ObservableCollection<StartupBackupRecord> Backups { get; } = new();

    [ObservableProperty]
    private bool _showBackups;

    [ObservableProperty]
    private StartupBackupRecord? _selectedBackup;

    [ObservableProperty]
    private bool _hasSelectedBackup;

    [ObservableProperty]
    private bool _isRestoring;

    // --- Lista filtrada ---

    /// <summary>
    /// Entradas visibles en la grilla. Es la MISMA referencia que
    /// <see cref="SelectableEntries"/>, por lo que la selección, el toggle y la
    /// desactivación siguen funcionando sobre el entry real. Los filtros y el
    /// buscador ahora filtran realmente la grilla (antes llenaban FilteredEntries,
    /// que el DataGrid no bindeaba, y el filtro era un no-op visible).
    /// </summary>
    public ObservableCollection<SelectableStartupEntry> FilteredSelectableEntries { get; } = new();

    /// <summary>Entradas visibles (vista de solo lectura, se mantiene sincronizada).</summary>
    public ObservableCollection<StartupEntry> FilteredEntries { get; } = new();

    [ObservableProperty]
    private SelectableStartupEntry? _selectedSelectableEntry;

    [ObservableProperty]
    private string _filteredCountText = "0 de 0";

    public StartupAnalysisViewModel(IStartupService startupService)
    {
        _startupService = startupService;
    }

    /// <summary>
    /// Confirmación explícita provista por la vista (diálogo modal) antes de
    /// desactivar entradas de inicio.
    /// </summary>
    public Func<string, bool>? ConfirmDisableRequest { get; set; }

    /// <summary>
    /// Hook para tests: recibe (ejecutable, argumentos) y devuelve si se abrió.
    /// Evita lanzar Explorer/regedit reales durante los tests.
    /// </summary>
    public Func<string, string, bool>? OpenShellTargetRequest { get; set; }

    /// <summary>
    /// Hook de portapapeles para tests: evita depender de una sesión STA/UI.
    /// </summary>
    internal Func<string, bool>? CopyTextOverride { get; set; }

    // =====================
    // ANÁLISIS
    // =====================

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        ClearMessages();
        IsRunning = true;
        HasResults = false;
        StatusText = "Analizando...";

        try
        {
            var analysis = await _startupService.AnalyzeStartupAsync();
            _allEntries = analysis.Entries;

            SummaryTotal = analysis.TotalCount.ToString();
            SummaryMicrosoft = analysis.MicrosoftCount.ToString();
            SummaryThirdParty = analysis.ThirdPartyCount.ToString();
            SummaryPossibleDisable = analysis.PossibleDisableCount.ToString();
            SummaryAlerts = analysis.AlertCount.ToString();

            RebuildSelectableEntries();
            ApplyFilter();

            // Dejar una fila seleccionada: el panel de detalle técnico arranca
            // con datos reales en lugar de "Seleccioná una entrada".
            if (SelectedSelectableEntry is null && FilteredSelectableEntries.Count > 0)
                SelectedSelectableEntry = FilteredSelectableEntries[0];

            HasResults = true;
            StatusText = analysis.AlertCount > 0
                ? $"Análisis completado ({analysis.AlertCount} alertas)"
                : "Análisis completado";
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

    [RelayCommand]
    private async Task SaveAnalysisAsync()
    {
        if (_allEntries.Count == 0)
        {
            ShowError("No hay análisis para guardar. Ejecutá uno primero.");
            return;
        }

        ClearMessages();
        try
        {
            var analysis = new StartupAnalysis
            {
                Entries = _allEntries,
                TechnicianName = await GetTechnicianNameAsync()
            };
            var fileName = await _startupService.SaveAnalysisAsync(analysis);
            ShowSuccess($"Análisis guardado: {fileName}");
        }
        catch (Exception ex)
        {
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

    // =====================
    // FILTROS
    // =====================

    [RelayCommand]
    private void SetFilter(string? filter)
    {
        if (!string.IsNullOrEmpty(filter))
        {
            SelectedFilter = filter;
            ApplyFilter();
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredSelectableEntries.Clear();
        FilteredEntries.Clear();

        var filtered = SelectedFilter switch
        {
            "No Microsoft" => SelectableEntries.Where(s => !s.Entry.IsMicrosoft),
            "Recomendados para revisar" => SelectableEntries.Where(
                s => s.Entry.Recommendation == StartupRecommendation.Review),
            "Posible desactivar" => SelectableEntries.Where(
                s => s.Entry.Recommendation == StartupRecommendation.PossibleDisable),
            "Alertas" => SelectableEntries.Where(s =>
                s.Entry.Status == StartupEntryStatus.PathNotFound ||
                s.Entry.Risk == RiskLevel.High ||
                s.Entry.Notes.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
                s.Entry.Notes.Contains("AppData", StringComparison.OrdinalIgnoreCase)),
            _ => SelectableEntries.AsEnumerable()
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(s =>
                s.Entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Entry.Command.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Entry.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Entry.Location.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var selectable in filtered)
        {
            FilteredSelectableEntries.Add(selectable);
            FilteredEntries.Add(selectable.Entry);
        }

        FilteredCountText = SelectableEntries.Count == FilteredSelectableEntries.Count
            ? $"{FilteredSelectableEntries.Count} de {SelectableEntries.Count}"
            : $"{FilteredSelectableEntries.Count} de {SelectableEntries.Count} (filtrado)";

        // Si la entrada seleccionada quedó fuera del filtro, se limpia el detalle.
        if (SelectedSelectableEntry is not null
            && !FilteredSelectableEntries.Contains(SelectedSelectableEntry))
        {
            SelectedSelectableEntry = null;
        }

        UpdateSelectedCount();
    }

    partial void OnSelectedSelectableEntryChanged(SelectableStartupEntry? value)
    {
        // El detalle técnico siempre refleja la fila seleccionada en la grilla.
        SelectedEntry = value?.Entry;
    }

    partial void OnSelectedEntryChanged(StartupEntry? value)
    {
        HasSelectedEntry = value != null;

        // Propiedades derivadas del detalle técnico.
        OnPropertyChanged(nameof(SelectedEntryCommand));
        OnPropertyChanged(nameof(SelectedEntryLocation));
        OnPropertyChanged(nameof(HasSelectedEntryLocation));
        OnPropertyChanged(nameof(IsSelectedEntryDisableable));
        OnPropertyChanged(nameof(IsRegistryEntry));
        OnPropertyChanged(nameof(IsStartupFolderEntry));
        OnPropertyChanged(nameof(IsScheduledTaskEntry));
        OnPropertyChanged(nameof(RegistryKeyFullPath));
        OnPropertyChanged(nameof(LocationKindText));
    }

    // =====================
    // SELECCIÓN Y DESACTIVACIÓN
    // =====================

    private void RebuildSelectableEntries()
    {
        foreach (var item in SelectableEntries)
            item.PropertyChanged -= OnSelectableEntryChanged;
        SelectableEntries.Clear();
        foreach (var entry in _allEntries)
        {
            var canDisable = _startupService.CanDisableStartupEntry(entry);
            var selectable = new SelectableStartupEntry
            {
                Entry = entry,
                IsSelectable = canDisable,
                SelectionReason = !canDisable && entry.IsMicrosoft
                    ? "Bloqueado: Microsoft"
                    : !canDisable
                        ? $"Fuente no soportada: {entry.SourceType}"
                        : ""
            };
            selectable.PropertyChanged += OnSelectableEntryChanged;
            SelectableEntries.Add(selectable);
        }
        HasSelectableEntries = SelectableEntries.Any(s => s.IsSelectable);
    }

    private void OnSelectableEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SelectableStartupEntry.IsSelected))
            UpdateSelectedCount();
    }

    [RelayCommand]
    private void ToggleSelection(SelectableStartupEntry? item)
    {
        if (item == null || !item.IsSelectable) return;
        item.IsSelected = !item.IsSelected;
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void SelectAllPossibleDisable()
    {
        // Selecciona todo lo que realmente se puede desactivar. Antes solo se
        // seleccionaban las entradas clasificadas "PossibleDisable", por lo que
        // una app de terceros normal (Keep/Review) nunca podía seleccionarse y
        // el botón de desactivar decía "no hay entradas seleccionadas".
        foreach (var item in SelectableEntries)
        {
            if (item.IsSelectable)
                item.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in SelectableEntries)
            item.IsSelected = false;
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = SelectableEntries.Count(s => s.IsSelected);
    }

    [RelayCommand]
    private async Task DisableSelectedAsync()
    {
        var selected = SelectableEntries.Where(s => s.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ShowError("No hay entradas seleccionadas para desactivar. Seleccioná una o usá 'Seleccionar todo lo desactivable'.");
            return;
        }

        if (IsDisabling)
            return;

        if (ConfirmDisableRequest is not null)
        {
            var names = string.Join("\n", selected.Select(s => "• " + s.Entry.Name));
            var message =
                $"¿Desactivar {selected.Count} entrada(s) de inicio?\n\n{names}\n\n" +
                "Se crea un backup y podés restaurarlas desde 'Backups'.";
            if (!ConfirmDisableRequest(message))
                return;
        }

        ClearMessages();
        IsDisabling = true;
        StatusText = "Desactivando...";

        try
        {
            var entries = selected.Select(s => s.Entry).ToList();
            var summary = await _startupService.DisableSelectedAsync(entries, DisableReason);

            // Refrescar lista (no borra los mensajes: se publican después)
            await AnalyzeAsync();

            var failures = summary.Results
                .Where(r => r.Result == StartupActionResult.Failed)
                .Select(r => $"• {r.EntryName}: {r.Message}")
                .ToList();
            var warnings = summary.Results
                .Where(r => r.Result is StartupActionResult.SkippedMicrosoft
                    or StartupActionResult.SkippedUnsupportedSource
                    or StartupActionResult.AlreadyDisabled)
                .Select(r => $"• {r.EntryName}: {r.Message}")
                .ToList();

            if (summary.FailedCount == 0)
            {
                var ok = $"Desactivadas {summary.SuccessCount} entrada(s) de inicio.";
                if (warnings.Count > 0)
                    ok += $" Omitidas: {warnings.Count}.";
                ShowSuccess(ok);
            }
            else
            {
                var parts = new List<string>
                {
                    $"Fallaron {summary.FailedCount} de {summary.TotalAttempted}."
                };
                if (summary.SuccessCount > 0)
                    parts.Add($"Exitosas: {summary.SuccessCount}.");
                ShowError(string.Join(" ", parts) +
                    (failures.Count > 0 ? "\n" + string.Join("\n", failures) : string.Empty));
            }

            // Las omisiones deben verse aunque haya éxito parcial.
            if (warnings.Count > 0)
            {
                ErrorMessage = string.IsNullOrEmpty(ErrorMessage)
                    ? "Omitidas:\n" + string.Join("\n", warnings)
                    : ErrorMessage + "\nOmitidas:\n" + string.Join("\n", warnings);
                HasError = true;
            }

            DisableReason = string.Empty;
        }
        catch (Exception ex)
        {
            ShowError($"Error durante la desactivación: {ex.Message}");
        }
        finally
        {
            IsDisabling = false;
        }
    }

    // =====================
    // BACKUPS Y REVERSIÓN
    // =====================

    [RelayCommand]
    private async Task LoadBackupsAsync()
    {
        ShowBackups = !ShowBackups;

        if (ShowBackups)
            await ReloadBackupsAsync();
    }

    private async Task ReloadBackupsAsync()
    {
        Backups.Clear();
        var backups = await _startupService.ListBackupsAsync();
        foreach (var backup in backups)
            Backups.Add(backup);
    }

    partial void OnSelectedBackupChanged(StartupBackupRecord? value)
    {
        HasSelectedBackup = value != null && value.CanRestore;
    }

    /// <summary>
    /// Restaura el backup recibido por CommandParameter. Antes el comando no
    /// tenía parámetro y dependía de SelectedBackup, que la UI nunca asignaba:
    /// el botón "Restaurar" era un no-op silencioso.
    /// </summary>
    [RelayCommand]
    private async Task RestoreBackupAsync(StartupBackupRecord? backup)
    {
        var target = backup ?? SelectedBackup;
        if (target == null)
        {
            ShowError("Elegí un backup para restaurar.");
            return;
        }

        if (IsRestoring)
            return;

        ClearMessages();
        IsRestoring = true;

        try
        {
            var result = await _startupService.RestoreAsync(target);

            if (result == StartupActionResult.Success)
            {
                ShowSuccess($"Entrada '{target.EntryName}' restaurada correctamente.");
                await ReloadBackupsAsync();
                await AnalyzeAsync();
            }
            else
            {
                ShowError($"No se pudo restaurar '{target.EntryName}': {result}.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error durante la restauración: {ex.Message}");
        }
        finally
        {
            IsRestoring = false;
        }
    }

    // =====================
    // DETALLE TÉCNICO Y ACCIONES DEL ENTRY SELECCIONADO
    // =====================

    /// <summary>
    /// Texto completo del comando del entry seleccionado (para "Copiar comando").
    /// </summary>
    public string BuildSelectedCommandText() =>
        SelectedEntry is null ? string.Empty : SelectedEntry.Command;

    /// <summary>
    /// Texto multilínea con toda la información técnica del entry seleccionado.
    /// Función pura: no toca el portapapeles.
    /// </summary>
    public string BuildSelectedEntryText()
    {
        var entry = SelectedEntry;
        if (entry is null)
            return string.Empty;

        var lines = new List<string>
        {
            $"ID: {entry.Id}",
            $"Nombre: {entry.Name}",
            $"Comando: {entry.Command}",
            $"Ubicación: {entry.Location}",
            $"Origen: {SourceTypeText(entry.SourceType)}",
            $"Editor: {ValueOrDash(entry.Publisher)}",
            $"Estado: {StatusDisplayText(entry.Status)}",
            $"Riesgo: {RiskText(entry.Risk)}",
            $"Recomendación: {RecommendationText(entry.Recommendation)}",
            $"Microsoft: {(entry.IsMicrosoft ? "sí" : "no")}",
            $"Desactivable: {(IsSelectedEntryDisableable ? "sí" : "no")}"
        };

        if (!string.IsNullOrWhiteSpace(entry.Notes))
            lines.Add($"Notas: {entry.Notes}");

        if (entry.SourceType is StartupSourceType.RegistryRun or StartupSourceType.RegistryRunOnce)
            lines.Add($"Regedit: {RegistryKeyFullPath}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Comando completo del entry seleccionado.</summary>
    public string SelectedEntryCommand => SelectedEntry?.Command ?? string.Empty;

    /// <summary>Ubicación completa (clave de registro, archivo o tarea).</summary>
    public string SelectedEntryLocation => SelectedEntry?.Location ?? string.Empty;

    public bool HasSelectedEntryLocation => !string.IsNullOrWhiteSpace(SelectedEntryLocation);

    public bool IsSelectedEntryDisableable
    {
        get
        {
            if (SelectedSelectableEntry is not null)
                return SelectedSelectableEntry.IsSelectable;

            return SelectedEntry is not null
                && _startupService.CanDisableStartupEntry(SelectedEntry);
        }
    }

    public bool IsRegistryEntry => SelectedEntry?.SourceType is
        StartupSourceType.RegistryRun or StartupSourceType.RegistryRunOnce;

    public bool IsStartupFolderEntry => SelectedEntry?.SourceType is
        StartupSourceType.StartupFolder or StartupSourceType.StartupFolderCommon;

    public bool IsScheduledTaskEntry => SelectedEntry?.SourceType == StartupSourceType.ScheduledTask;

    /// <summary>
    /// Ruta completa de la clave de registro (HKCU → HKEY_CURRENT_USER,
    /// HKLM → HKEY_LOCAL_MACHINE) para abrir regedit en la clave exacta.
    /// Vacío si el entry no es de registro.
    /// </summary>
    public string RegistryKeyFullPath
    {
        get
        {
            var location = SelectedEntry?.Location;
            if (string.IsNullOrWhiteSpace(location))
                return string.Empty;

            const string currentUser = "HKCU\\";
            const string localMachine = "HKLM\\";
            const string users = "HKU\\";

            if (location.StartsWith(currentUser, StringComparison.OrdinalIgnoreCase))
                return "HKEY_CURRENT_USER\\" + location[currentUser.Length..];
            if (location.StartsWith(localMachine, StringComparison.OrdinalIgnoreCase))
                return "HKEY_LOCAL_MACHINE\\" + location[localMachine.Length..];
            if (location.StartsWith(users, StringComparison.OrdinalIgnoreCase))
                return "HKEY_USERS\\" + location[users.Length..];

            return string.Empty;
        }
    }

    public string LocationKindText => SelectedEntry?.SourceType switch
    {
        StartupSourceType.RegistryRun or StartupSourceType.RegistryRunOnce => "Clave de registro",
        StartupSourceType.StartupFolder or StartupSourceType.StartupFolderCommon => "Carpeta de inicio",
        StartupSourceType.ScheduledTask => "Tarea programada",
        _ => "-"
    };

    [RelayCommand]
    private Task CopyAsync()
    {
        if (SelectedEntry is null)
        {
            ShowError("Seleccioná una entrada de inicio para copiar su comando.");
            return Task.CompletedTask;
        }

        if (!TryCopyText(BuildSelectedCommandText()))
        {
            ShowError("No se pudo copiar el comando al portapapeles.");
            return Task.CompletedTask;
        }

        ShowSuccess("Comando copiado al portapapeles.");
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CopyEntryAsync()
    {
        if (SelectedEntry is null)
        {
            ShowError("Seleccioná una entrada de inicio para copiar su detalle.");
            return Task.CompletedTask;
        }

        if (!TryCopyText(BuildSelectedEntryText()))
        {
            ShowError("No se pudo copiar la entrada al portapapeles.");
            return Task.CompletedTask;
        }

        ShowSuccess("Entrada completa copiada al portapapeles.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Abre la ubicación del entry seleccionado:
    /// - registro → regedit en la clave HK* exacta (si el shell lo permite);
    /// - carpeta de inicio → Explorer seleccionando el archivo/carpeta;
    /// - tarea programada → Programador de tareas.
    /// Nunca muestra rutas privadas en el mensaje de error.
    /// </summary>
    [RelayCommand]
    private Task OpenLocationAsync()
    {
        if (SelectedEntry is null)
        {
            ShowError("Seleccioná una entrada de inicio para abrir su ubicación.");
            return Task.CompletedTask;
        }

        var opened = false;

        if (IsRegistryEntry)
        {
            var keyPath = RegistryKeyFullPath;
            opened = !string.IsNullOrWhiteSpace(keyPath)
                && TryOpenShellTarget("regedit.exe", $"\"{keyPath}\"");
        }
        else if (IsStartupFolderEntry)
        {
            var location = SelectedEntryLocation;
            try
            {
                if (File.Exists(location))
                {
                    opened = TryOpenShellTarget("explorer.exe", $"/select,\"{location}\"");
                }
                else if (Directory.Exists(location))
                {
                    opened = TryOpenShellTarget("explorer.exe", $"\"{location}\"");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException
                or PathTooLongException or UnauthorizedAccessException
                or System.Security.SecurityException)
            {
                opened = false;
            }
        }
        else if (IsScheduledTaskEntry)
        {
            opened = TryOpenShellTarget("taskschd.msc", string.Empty);
        }

        if (!opened)
        {
            ShowError("No se pudo abrir la ubicación. Verificá que el elemento exista y que tengas permisos.");
            return Task.CompletedTask;
        }

        ShowSuccess("Ubicación abierta.");
        return Task.CompletedTask;
    }

    private bool TryCopyText(string text)
    {
        try
        {
            if (CopyTextOverride is not null)
                return CopyTextOverride(text);

            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryOpenShellTarget(string fileName, string arguments)
    {
        try
        {
            if (OpenShellTargetRequest is not null)
                return OpenShellTargetRequest(fileName, arguments);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string SourceTypeText(StartupSourceType sourceType) => sourceType switch
    {
        StartupSourceType.RegistryRun => "Registro (Run)",
        StartupSourceType.RegistryRunOnce => "Registro (RunOnce)",
        StartupSourceType.StartupFolder => "Carpeta de inicio del usuario",
        StartupSourceType.StartupFolderCommon => "Carpeta de inicio común",
        StartupSourceType.ScheduledTask => "Tarea programada",
        _ => sourceType.ToString()
    };

    private static string RiskText(RiskLevel risk) => risk switch
    {
        RiskLevel.Low => "Bajo",
        RiskLevel.Medium => "Medio",
        RiskLevel.High => "Alto",
        _ => "Desconocido"
    };

    private static string RecommendationText(StartupRecommendation recommendation) => recommendation switch
    {
        StartupRecommendation.Keep => "Mantener",
        StartupRecommendation.Review => "Revisar",
        StartupRecommendation.PossibleDisable => "Posible desactivar",
        _ => recommendation.ToString()
    };

    private static string StatusDisplayText(StartupEntryStatus status) => status switch
    {
        StartupEntryStatus.Active => "Activo",
        StartupEntryStatus.Inactive => "Inactivo",
        StartupEntryStatus.PathNotFound => "Ruta no encontrada",
        _ => "Desconocido"
    };

    private static string ValueOrDash(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;

    // =====================
    // HELPERS
    // =====================

    private static async Task<string> GetTechnicianNameAsync()
    {
        return await Infrastructure.Helpers.SettingsHelper.GetTechnicianNameAsync(
            new Infrastructure.Data.JsonSettingsService());
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
/// Entrada de inicio seleccionable para desactivación.
/// </summary>
public partial class SelectableStartupEntry : ObservableObject
{
    public required StartupEntry Entry { get; init; }
    public required bool IsSelectable { get; init; }
    public required string SelectionReason { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    // --- Presentación técnica en la grilla (español, sin cambiar el modelo Core) ---

    public string Name => Entry.Name;

    public string Publisher => Entry.Publisher;

    public string Command => Entry.Command;

    public string Location => Entry.Location;

    public string SourceTypeText => Entry.SourceType switch
    {
        StartupSourceType.RegistryRun => "Registro (Run)",
        StartupSourceType.RegistryRunOnce => "Registro (RunOnce)",
        StartupSourceType.StartupFolder => "Carpeta usuario",
        StartupSourceType.StartupFolderCommon => "Carpeta común",
        StartupSourceType.ScheduledTask => "Tarea programada",
        _ => Entry.SourceType.ToString()
    };

    public string RiskText => Entry.Risk switch
    {
        RiskLevel.Low => "Bajo",
        RiskLevel.Medium => "Medio",
        RiskLevel.High => "Alto",
        _ => "Desconocido"
    };

    public string RecommendationText => Entry.Recommendation switch
    {
        StartupRecommendation.Keep => "Mantener",
        StartupRecommendation.Review => "Revisar",
        StartupRecommendation.PossibleDisable => "Posible desactivar",
        _ => Entry.Recommendation.ToString()
    };

    public string StatusText => Entry.Status switch
    {
        StartupEntryStatus.Active => "Activo",
        StartupEntryStatus.Inactive => "Inactivo",
        StartupEntryStatus.PathNotFound => "Ruta no encontrada",
        _ => "Desconocido"
    };
}
