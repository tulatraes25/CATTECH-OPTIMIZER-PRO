using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de análisis de programas de inicio.
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

    // --- Lista filtrada ---

    public ObservableCollection<StartupEntry> FilteredEntries { get; } = new();

    public StartupAnalysisViewModel(IStartupService startupService)
    {
        _startupService = startupService;
    }

    /// <summary>
    /// Ejecuta el análisis de programas de inicio.
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
            var analysis = await _startupService.AnalyzeStartupAsync();
            _allEntries = analysis.Entries;

            SummaryTotal = analysis.TotalCount.ToString();
            SummaryMicrosoft = analysis.MicrosoftCount.ToString();
            SummaryThirdParty = analysis.ThirdPartyCount.ToString();
            SummaryPossibleDisable = analysis.PossibleDisableCount.ToString();
            SummaryAlerts = analysis.AlertCount.ToString();

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

    /// <summary>
    /// Guarda el análisis actual en disco.
    /// </summary>
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

    /// <summary>
    /// Cambia el filtro activo.
    /// </summary>
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

        // Aplicar búsqueda
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(e =>
                e.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Command.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var entry in filtered)
        {
            FilteredEntries.Add(entry);
        }
    }

    partial void OnSelectedEntryChanged(StartupEntry? value)
    {
        HasSelectedEntry = value != null;
    }

    private static async Task<string> GetTechnicianNameAsync()
    {
        try
        {
            var settingsService = new Infrastructure.Data.JsonSettingsService();
            var settings = await settingsService.LoadSettingsAsync();
            return settings.Company.TechnicianName;
        }
        catch
        {
            return string.Empty;
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
