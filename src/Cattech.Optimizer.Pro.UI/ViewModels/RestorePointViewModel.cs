using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de puntos de restauración.
/// La protección del sistema se muestra como estado probable (advisory): nunca bloquea
/// el intento de creación. Todo fallo o advertencia se publica en los banners de la vista.
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

    [ObservableProperty]
    private bool _hasInfoMessage;

    [ObservableProperty]
    private bool _hasWarnings;

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
    private string _availabilityBadgeText = "?";

    [ObservableProperty]
    private string _availabilityBadgeColor = "#9E9E9E";

    [ObservableProperty]
    private bool _isProtectionEnabled;

    [ObservableProperty]
    private string _protectionText = "No determinado";

    [ObservableProperty]
    private string _protectionBadgeText = "?";

    [ObservableProperty]
    private string _protectionBadgeColor = "#9E9E9E";

    [ObservableProperty]
    private bool _canCreatePoint;

    [ObservableProperty]
    private string _attemptBadgeColor = "#9E9E9E";

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

    /// <summary>
    /// Advertencias visibles de la última verificación o creación.
    /// </summary>
    public ObservableCollection<string> WarningMessages { get; } = new();

    [ObservableProperty]
    private bool _showHistory;

    public RestorePointViewModel(IRestorePointService restorePointService)
    {
        _restorePointService = restorePointService;
        RestorePointName = restorePointService.GenerateRestorePointName();
    }

    /// <summary>
    /// Los botones de verificar e historial se deshabilitan mientras hay una operación en curso.
    /// </summary>
    public bool NotRunning => !IsRunning;

    /// <summary>
    /// El botón de crear se habilita sólo cuando no hay una operación en curso y el
    /// estado conocido permite intentarlo (la protección del sistema no bloquea).
    /// </summary>
    public bool CreateButtonEnabled => !IsRunning && CanCreatePoint;

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(NotRunning));
        OnPropertyChanged(nameof(CreateButtonEnabled));
    }

    partial void OnCanCreatePointChanged(bool value)
    {
        OnPropertyChanged(nameof(CreateButtonEnabled));
    }

    /// <summary>
    /// Verifica el estado del sistema.
    /// </summary>
    [RelayCommand]
    private async Task CheckStatusAsync()
    {
        if (IsRunning)
        {
            return;
        }

        ClearMessages();
        SetWarnings(Array.Empty<string>());
        IsRunning = true;
        StatusText = "Verificando...";

        try
        {
            var status = await _restorePointService.CheckStatusAsync();
            ApplyStatus(status);
        }
        catch (Exception ex)
        {
            StatusText = "Error al verificar";
            CanCreatePoint = false;
            AttemptBadgeColor = "#F44336";
            ShowError($"No se pudo verificar el estado del sistema: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    /// <summary>
    /// Crea un punto de restauración. Windows es la única fuente de verdad del resultado:
    /// el éxito sólo se muestra cuando Checkpoint-Computer devolvió éxito o el punto se verificó.
    /// </summary>
    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        if (IsRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RestorePointName))
        {
            ShowError("Ingresá un nombre para el punto de restauración.");
            return;
        }

        if (!CanCreatePoint)
        {
            ShowError("No se puede crear el punto todavía: verificá el estado del sistema primero.");
            return;
        }

        ClearMessages();
        SetWarnings(Array.Empty<string>());
        IsRunning = true;
        StatusText = "Creando punto de restauración...";

        try
        {
            var result = await _restorePointService.CreateRestorePointAsync(RestorePointName);

            HasResult = true;
            ResultSuccess = result.Success;
            SetWarnings(result.Warnings);

            if (result.Success)
            {
                ResultSummary = "Punto de restauración creado correctamente";
                ResultDetails = BuildSuccessDetails(result);
                StatusText = "Creado correctamente";
                ShowSuccess(string.IsNullOrWhiteSpace(result.Notes)
                    ? "Punto de restauración creado correctamente."
                    : result.Notes);
            }
            else
            {
                ResultSummary = "No se pudo crear el punto de restauración";
                ResultDetails = BuildFailureDetails(result);
                StatusText = "No se pudo crear";
                ShowError(BuildErrorMessage(result));
            }

            try
            {
                await _restorePointService.SaveResultAsync(result);
            }
            catch (Exception saveException)
            {
                var warnings = WarningMessages.ToList();
                warnings.Add($"No se pudo guardar el resultado en disco: {saveException.Message}");
                SetWarnings(warnings);
            }

            RestorePointName = _restorePointService.GenerateRestorePointName();
        }
        catch (Exception ex)
        {
            StatusText = "Error al crear";
            ShowError($"Error inesperado al crear el punto de restauración: {ex.Message}");
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
        if (IsRunning)
        {
            return;
        }

        ShowHistory = !ShowHistory;

        if (!ShowHistory)
        {
            return;
        }

        IsRunning = true;
        StatusText = "Cargando historial...";

        try
        {
            History.Clear();
            var results = await _restorePointService.ListResultsAsync();
            foreach (var result in results)
            {
                History.Add(result);
            }

            StatusText = "Historial cargado";
        }
        catch (Exception ex)
        {
            StatusText = "Error al cargar el historial";
            ShowError($"No se pudo cargar el historial de puntos de restauración: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ApplyStatus(RestorePointStatus status)
    {
        IsAdministrator = status.IsAdministrator;
        AdminStatusText = status.IsAdministrator ? "Sí (administrador)" : "No (usuario estándar)";

        IsSystemRestoreAvailable =
            status.AvailabilityState == RestoreAvailabilityState.LikelyAvailable;
        (RestoreServiceText, AvailabilityBadgeText, AvailabilityBadgeColor) =
            status.AvailabilityState switch
            {
                RestoreAvailabilityState.LikelyAvailable => ("Disponible", "Sí", "#4CAF50"),
                RestoreAvailabilityState.NotAvailable => ("No detectado", "No", "#F44336"),
                _ => ("Sin determinar", "?", "#9E9E9E")
            };

        IsProtectionEnabled = status.ProtectionState == RestoreProtectionState.LikelyEnabled;
        (ProtectionText, ProtectionBadgeText, ProtectionBadgeColor) =
            status.ProtectionState switch
            {
                RestoreProtectionState.LikelyEnabled => ("Probablemente habilitada", "Sí", "#4CAF50"),
                RestoreProtectionState.LikelyDisabled => ("Probablemente deshabilitada", "No", "#FF9800"),
                _ => ("Desconocida (no bloquea)", "?", "#9E9E9E")
            };

        CanCreatePoint = status.CanAttemptCreate;
        AttemptBadgeColor = status.CanAttemptCreate ? "#0078D4" : "#F44336";

        HasStatus = true;
        StatusText = status.CanAttemptCreate ? "Estado verificado" : "No se puede crear";
        SetWarnings(status.Warnings);

        if (status.CanAttemptCreate)
        {
            if (status.Warnings.Count == 0)
            {
                ShowSuccess(status.StatusMessage);
            }
            else
            {
                ShowInfo(status.StatusMessage);
            }
        }
        else
        {
            var blockedReason = string.IsNullOrWhiteSpace(status.AttemptBlockedReason)
                ? status.StatusMessage
                : $"No se puede intentar crear el punto: {status.AttemptBlockedReason}.";
            ShowError(blockedReason);
        }
    }

    private void SetWarnings(IEnumerable<string> warnings)
    {
        WarningMessages.Clear();

        foreach (var warning in warnings)
        {
            if (!string.IsNullOrWhiteSpace(warning))
            {
                WarningMessages.Add(warning);
            }
        }

        HasWarnings = WarningMessages.Count > 0;
    }

    private void ShowSuccess(string message)
    {
        StatusMessage = message;
        IsSuccess = true;
        HasError = false;
        HasInfoMessage = false;
        ErrorMessage = string.Empty;
    }

    private void ShowInfo(string message)
    {
        StatusMessage = message;
        IsSuccess = false;
        HasError = false;
        HasInfoMessage = !string.IsNullOrWhiteSpace(message);
        ErrorMessage = string.Empty;
    }

    private void ShowError(string message)
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
        HasError = true;
        HasInfoMessage = false;
        ErrorMessage = message;
    }

    private void ClearMessages()
    {
        StatusMessage = string.Empty;
        IsSuccess = false;
        HasError = false;
        HasInfoMessage = false;
        ErrorMessage = string.Empty;
    }

    private static string BuildErrorMessage(RestorePointResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? "Windows no creó el punto de restauración y no devolvió un mensaje de error."
            : result.ErrorMessage;

        return string.IsNullOrWhiteSpace(result.ErrorCode)
            ? message
            : $"{message} (código: {result.ErrorCode})";
    }

    private static string BuildSuccessDetails(RestorePointResult result)
    {
        var details = new List<string>
        {
            $"Nombre: {result.RestorePointName}",
            $"Método: {DescribeMethod(result.MethodUsed)}",
            result.Verified
                ? "Verificación: punto nuevo detectado"
                : "Verificación: no se pudo confirmar automáticamente"
        };

        if (result.Warnings.Count > 0)
        {
            details.Add($"Advertencias: {result.Warnings.Count}");
        }

        return string.Join("\n", details);
    }

    private static string BuildFailureDetails(RestorePointResult result)
    {
        var details = new List<string>
        {
            $"Nombre: {result.RestorePointName}",
            $"Código: {(string.IsNullOrWhiteSpace(result.ErrorCode) ? "DESCONOCIDO" : result.ErrorCode)}",
            $"Detalle: {(string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Sin mensaje de Windows" : result.ErrorMessage)}"
        };

        return string.Join("\n", details);
    }

    private static string DescribeMethod(RestorePointMethod method)
    {
        return method switch
        {
            RestorePointMethod.PowerShellCheckpoint => "Checkpoint-Computer (PowerShell)",
            RestorePointMethod.WmiSystemRestore => "WMI SystemRestore",
            RestorePointMethod.PowerShellDisable => "PowerShell (deshabilitar)",
            RestorePointMethod.ManualRecommendation => "Recomendación manual",
            _ => "No determinado"
        };
    }
}
