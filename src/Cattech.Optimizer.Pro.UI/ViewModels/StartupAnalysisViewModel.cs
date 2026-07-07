using System.Collections.ObjectModel;
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

    public ObservableCollection<StartupEntry> FilteredEntries { get; } = new();

    public StartupAnalysisViewModel(IStartupService startupService)
    {
        _startupService = startupService;
    }

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
        FilteredEntries.Clear();
        var filtered = SelectedFilter switch
        {
            "No Microsoft" => _allEntries.Where(e => !e.IsMicrosoft),
            "Recomendados para revisar" => _allEntries.Where(e => e.Recommendation == StartupRecommendation.Review),
            "Posible desactivar" => _allEntries.Where(e => e.Recommendation == StartupRecommendation.PossibleDisable),
            "Alertas" => _allEntries.Where(e =>
                e.Status == StartupEntryStatus.PathNotFound ||
                e.Risk == RiskLevel.High ||
                e.Notes.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
                e.Notes.Contains("AppData", StringComparison.OrdinalIgnoreCase)),
            _ => _allEntries.AsEnumerable()
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(e =>
                e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Command.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered)
            FilteredEntries.Add(entry);

        UpdateSelectedCount();
    }

    partial void OnSelectedEntryChanged(StartupEntry? value)
    {
        HasSelectedEntry = value != null;
    }

    // =====================
    // SELECCIÓN Y DESACTIVACIÓN
    // =====================

    private void RebuildSelectableEntries()
    {
        SelectableEntries.Clear();
        foreach (var entry in _allEntries)
        {
            var canDisable = _startupService.CanDisableStartupEntry(entry);
            SelectableEntries.Add(new SelectableStartupEntry
            {
                Entry = entry,
                IsSelectable = canDisable,
                SelectionReason = !canDisable && entry.IsMicrosoft
                    ? "Bloqueado: Microsoft"
                    : !canDisable
                        ? $"Fuente no soportada: {entry.SourceType}"
                        : ""
            });
        }
        HasSelectableEntries = SelectableEntries.Any(s => s.IsSelectable);
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
        foreach (var item in SelectableEntries)
        {
            if (item.IsSelectable && item.Entry.Recommendation == StartupRecommendation.PossibleDisable)
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
            ShowError("No hay entradas seleccionadas para desactivar.");
            return;
        }

        ClearMessages();
        IsDisabling = true;

        try
        {
            var entries = selected.Select(s => s.Entry).ToList();
            var summary = await _startupService.DisableSelectedAsync(entries, DisableReason);

            // Refrescar lista
            await AnalyzeAsync();

            // Mostrar resultado
            if (summary.FailedCount == 0 && summary.SkippedMicrosoftCount == 0 && summary.SkippedUnsupportedCount == 0)
            {
                ShowSuccess($"Desactivadas {summary.SuccessCount} entradas correctamente.");
            }
            else
            {
                var parts = new List<string>();
                if (summary.SuccessCount > 0) parts.Add($"{summary.SuccessCount} exitosas");
                if (summary.FailedCount > 0) parts.Add($"{summary.FailedCount} fallidas");
                if (summary.SkippedMicrosoftCount > 0) parts.Add($"{summary.SkippedMicrosoftCount} omitidas (Microsoft)");
                if (summary.SkippedUnsupportedCount > 0) parts.Add($"{summary.SkippedUnsupportedCount} omitidas (fuente no soportada)");

                StatusMessage = string.Join(", ", parts);
                IsSuccess = summary.SuccessCount > 0;
                HasError = summary.FailedCount > 0;
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
        {
            Backups.Clear();
            var backups = await _startupService.ListBackupsAsync();
            foreach (var backup in backups)
                Backups.Add(backup);
        }
    }

    partial void OnSelectedBackupChanged(StartupBackupRecord? value)
    {
        HasSelectedBackup = value != null && value.CanRestore;
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (SelectedBackup == null) return;

        ClearMessages();
        IsRestoring = true;

        try
        {
            var result = await _startupService.RestoreAsync(SelectedBackup);

            if (result == StartupActionResult.Success)
            {
                ShowSuccess($"Entrada '{SelectedBackup.EntryName}' restaurada correctamente.");
                await LoadBackupsAsync();
                await AnalyzeAsync();
            }
            else
            {
                ShowError($"Error al restaurar: {result}");
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
}
