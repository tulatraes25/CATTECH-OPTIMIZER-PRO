using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de puntos de restauración.
/// </summary>
public partial class RestorePointViewModel : ObservableObject
{
    private readonly IRestorePointService _restorePointService;

    // --- Estado de la UI ---

    [ObservableProperty]
    private string _statusText = "Sin verificar";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _hasStatus;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // --- Estado del sistema ---

    [ObservableProperty]
    private bool _isAdministrator;

    [ObservableProperty]
    private string _adminStatusText = "No determinado";

    [ObservableProperty]
    private bool _isSystemRestoreAvailable;

    [ObservableProperty]
    private string _restoreServiceText = "No determinado";

    [ObservableProperty]
    private bool _isProtectionEnabled;

    [ObservableProperty]
    private string _protectionText = "No determinado";

    [ObservableProperty]
    private bool _canCreatePoint;

    // --- Nombre del punto ---

    [ObservableProperty]
    private string _restorePointName = string.Empty;

    // --- Resultado ---

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultSummary = string.Empty;

    [ObservableProperty]
    private string _resultDetails = string.Empty;

    [ObservableProperty]
    private bool _resultSuccess;

    // --- Historial ---

    public ObservableCollection<RestorePointResultSummary> History { get; } = new();

    [ObservableProperty]
    private bool _showHistory;

    public RestorePointViewModel(IRestorePointService restorePointService)
    {
        _restorePointService = restorePointService;
        RestorePointName = restorePointService.GenerateRestorePointName();
    }

    /// <summary>
    /// Verifica el estado del sistema.
    /// </summary>
    [RelayCommand]
    private async Task CheckStatusAsync()
    {
        ClearMessages();
        IsRunning = true;
        StatusText = "Verificando...";

        try
        {
            var status = await _restorePointService.CheckStatusAsync();

            IsAdministrator = status.IsAdministrator;
            AdminStatusText = status.IsAdministrator ? "Sí (administrador)" : "No (usuario estándar)";

            IsSystemRestoreAvailable = status.IsSystemRestoreAvailable;
            RestoreServiceText = status.IsSystemRestoreAvailable ? "Disponible" : "No disponible";

            IsProtectionEnabled = status.IsProtectionEnabled;
            ProtectionText = status.IsProtectionEnabled ? "Habilitada" : "Deshabilitada";

            CanCreatePoint = status.CanCreatePoint;

            HasStatus = true;
            StatusText = status.CanCreatePoint ? "Listo para crear punto" : "No listo";

            if (status.Warnings.Count > 0)
            {
                StatusMessage = string.Join(" | ", status.Warnings);
                HasError = !status.CanCreatePoint;
                IsSuccess = status.CanCreatePoint;
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error al verificar";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Crea un punto de restauración.
    /// </summary>
    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        if (string.IsNullOrWhiteSpace(RestorePointName))
        {
            ShowError("Ingresá un nombre para el punto de restauración.");
            return;
        }

        if (!CanCreatePoint)
        {
            ShowError("No se puede crear el punto. Verificá el estado del sistema primero.");
            return;
        }

        ClearMessages();
        IsRunning = true;
        StatusText = "Creando punto...";

        try
        {
            var result = await _restorePointService.CreateRestorePointAsync(RestorePointName);

            HasResult = true;
            ResultSuccess = result.Success;

            if (result.Success)
            {
                ResultSummary = "Punto de restauración creado correctamente";
                ResultDetails = $"Nombre: {result.RestorePointName}\nMétodo: {result.MethodUsed}";
                StatusText = "Creado correctamente";
                IsSuccess = true;
            }
            else
            {
                ResultSummary = "No se pudo crear el punto de restauración";
                ResultDetails = $"Error: {result.ErrorMessage}";
                StatusText = "No se pudo crear";
                HasError = true;
            }

            // Guardar resultado
            await _restorePointService.SaveResultAsync(result);

            // Actualizar nombre para el siguiente
            RestorePointName = _restorePointService.GenerateRestorePointName();
        }
        catch (Exception ex)
        {
            StatusText = "Error al crear";
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Carga el historial de intentos.
    /// </summary>
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        ShowHistory = !ShowHistory;

        if (ShowHistory)
        {
            History.Clear();
            var results = await _restorePointService.ListResultsAsync();
            foreach (var result in results)
                History.Add(result);
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
