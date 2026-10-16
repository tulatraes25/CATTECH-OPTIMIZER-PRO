using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de cliente y equipo.
/// </summary>
public partial class ClientEquipmentViewModel : ObservableObject
{
    private readonly IServiceReportService _reportService;
    private readonly ISettingsService _settingsService;
    private readonly IHardwareService _hardwareService;
    private bool _detectedDiskFreeKnown;
    private double _detectedSystemVolumeCapacityGb;
    private double _detectedSystemVolumeFreeGb;
    private int _hardwareDetectionGeneration;

    // --- Campos del formulario: Cliente ---

    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    private string _clientPhone = string.Empty;

    [ObservableProperty]
    private string _clientEmail = string.Empty;

    [ObservableProperty]
    private string _clientCompany = string.Empty;

    [ObservableProperty]
    private string _clientAddress = string.Empty;

    [ObservableProperty]
    private string _clientNotes = string.Empty;

    // --- Campos del formulario: Equipo ---

    [ObservableProperty]
    private string _equipmentBrand = string.Empty;

    [ObservableProperty]
    private string _equipmentModel = string.Empty;

    [ObservableProperty]
    private string _serialNumber = string.Empty;

    [ObservableProperty]
    private string _equipmentType = string.Empty;

    [ObservableProperty]
    private string _serviceReason = string.Empty;

    [ObservableProperty]
    private string _equipmentNotes = string.Empty;

    // --- Campos detectados automáticamente ---

    [ObservableProperty]
    private string _detectedOS = string.Empty;

    [ObservableProperty]
    private string _detectedEdition = string.Empty;

    [ObservableProperty]
    private string _detectedArchitecture = string.Empty;

    [ObservableProperty]
    private string _detectedProcessor = string.Empty;

    [ObservableProperty]
    private string _detectedRam = string.Empty;

    [ObservableProperty]
    private string _detectedDisk = string.Empty;

    [ObservableProperty]
    private string _detectedDiskCapacity = string.Empty;

    [ObservableProperty]
    private string _detectedDiskFree = string.Empty;

    [ObservableProperty]
    private string _detectedDiskType = string.Empty;

    [ObservableProperty]
    private string _detectedComputerName = string.Empty;

    [ObservableProperty]
    private string _detectedUser = string.Empty;

    // --- Estado de la UI ---

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _detectionStatus = "Sin detectar";

    [ObservableProperty]
    private bool _isDetecting;

    [ObservableProperty]
    private bool _hasDetectedData;

    /// <summary>
    /// Opciones para el combo de tipo de equipo.
    /// </summary>
    public string[] EquipmentTypes { get; } =
    [
        "Notebook",
        "PC de escritorio",
        "All-in-One",
        "Otro"
    ];

    public ClientEquipmentViewModel(
        IServiceReportService reportService,
        ISettingsService settingsService,
        IHardwareService hardwareService)
    {
        _reportService = reportService;
        _settingsService = settingsService;
        _hardwareService = hardwareService;
    }

    /// <summary>
    /// Detecta datos del equipo de forma no invasiva.
    /// </summary>
    [RelayCommand]
    private async Task DetectHardwareAsync()
    {
        // A later NewRecord invalidates this pending response.
        var generation = ++_hardwareDetectionGeneration;
        ClearMessages();
        IsDetecting = true;
        DetectionStatus = "Detectando...";
        _detectedDiskFreeKnown = false;
        _detectedSystemVolumeCapacityGb = 0;
        _detectedSystemVolumeFreeGb = 0;
        DetectedDisk = "No detectado";
        DetectedDiskCapacity = "No disponible";
        DetectedDiskFree = "No disponible";
        DetectedDiskType = "No determinado";

        try
        {
            var report = await _hardwareService.GetHardwareReportAsync();
            if (generation != _hardwareDetectionGeneration)
                return; // A newer record owns the form; discard old data.

            // Sistema operativo
            DetectedOS = report.System.OsName;
            DetectedEdition = report.System.OsVersion;
            DetectedArchitecture = report.System.Architecture;
            DetectedComputerName = report.System.ComputerName;
            WindowsVersion = $"{report.System.OsName} (Build {report.System.BuildNumber})";

            // CPU
            DetectedProcessor = report.Cpu.Name;

            // RAM
            DetectedRam = report.Memory.TotalGB > 0
                ? $"{report.Memory.TotalGB} GB"
                : "No disponible";

            // Disco del volumen del sistema: no asumir primer disco WMI.
            _detectedDiskFreeKnown = false;
            var candidates = report.Disks.Where(d => d.VolumeAssociationsVerified &&
                d.Volumes.Count(v => v.IsSystemVolume) == 1).ToArray();
            var mainDisk = candidates.Length == 1 ? candidates[0] : null;
            if (mainDisk != null)
            {
                var volume = mainDisk.SystemVolume!;
                DetectedDisk = mainDisk.Name;
                DetectedDiskCapacity = volume.CapacityGB.HasValue
                    ? $"{volume.CapacityGB.Value:F2} GB"
                    : "No disponible";
                DetectedDiskFree = volume.FreeGB.HasValue
                    ? $"{volume.FreeGB.Value:F2} GB"
                    : "No disponible";
                DetectedDiskType = string.IsNullOrWhiteSpace(mainDisk.MediaType)
                    ? "No determinado" : mainDisk.MediaType;
                _detectedDiskFreeKnown = volume.FreePercent.HasValue;
                if (_detectedDiskFreeKnown)
                {
                    _detectedSystemVolumeCapacityGb = volume.CapacityGB!.Value;
                    _detectedSystemVolumeFreeGb = volume.FreeGB!.Value;
                }
            }
            else
            {
                DetectedDisk = "No detectado";
                DetectedDiskCapacity = "No disponible";
                DetectedDiskFree = "No disponible";
                DetectedDiskType = "No detectado";
            }

            // Usuario actual
            DetectedUser = Environment.UserName;

            DetectionStatus = "Detectado correctamente";
            HasDetectedData = true;
        }
        catch (Exception)
        {
            if (generation == _hardwareDetectionGeneration)
            {
                DetectionStatus = "Error al detectar";
                ShowError("No se pudo completar la detección de hardware.");
            }
        }
        finally
        {
            if (generation == _hardwareDetectionGeneration)
                IsDetecting = false;
        }
    }

    /// <summary>
    /// Guarda el reporte de servicio en disco.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ClearMessages();

        var validationError = Validate();
        if (!string.IsNullOrEmpty(validationError))
        {
            ShowError(validationError);
            return;
        }

        try
        {
            var report = BuildReportFromForm();
            await _reportService.SaveReportAsync(report);

            HasUnsavedChanges = false;
            // Storage filenames and raw exceptions can contain private
            // directory, technician or customer data. Keep UI messages safe.
            ShowSuccess("Registro guardado correctamente. Podés consultarlo en Informes.");
        }
        catch (Exception)
        {
            // A storage failure may happen after some I/O; do not claim that
            // the write was certainly absent or advise a blind retry.
            ShowError("No se pudo confirmar el guardado. Revisá Informes antes de intentar nuevamente.");
        }
    }

    /// <summary>
    /// Limpia todos los campos para un nuevo registro.
    /// </summary>
    [RelayCommand]
    private void NewRecord()
    {
        // Invalidate outstanding async detection before touching bound fields.
        ++_hardwareDetectionGeneration;
        _detectedDiskFreeKnown = false;
        _detectedSystemVolumeCapacityGb = 0;
        _detectedSystemVolumeFreeGb = 0;
        WindowsVersion = string.Empty;
        IsDetecting = false;

        // Cliente
        ClientName = string.Empty;
        ClientPhone = string.Empty;
        ClientEmail = string.Empty;
        ClientCompany = string.Empty;
        ClientAddress = string.Empty;
        ClientNotes = string.Empty;

        // Equipo
        EquipmentBrand = string.Empty;
        EquipmentModel = string.Empty;
        SerialNumber = string.Empty;
        EquipmentType = string.Empty;
        ServiceReason = string.Empty;
        EquipmentNotes = string.Empty;

        // Datos detectados
        DetectedOS = string.Empty;
        DetectedEdition = string.Empty;
        DetectedArchitecture = string.Empty;
        DetectedProcessor = string.Empty;
        DetectedRam = string.Empty;
        DetectedDisk = string.Empty;
        DetectedDiskCapacity = string.Empty;
        DetectedDiskFree = string.Empty;
        DetectedDiskType = string.Empty;
        DetectedComputerName = string.Empty;
        DetectedUser = string.Empty;

        DetectionStatus = "Sin detectar";
        HasDetectedData = false;
        HasUnsavedChanges = false;
        ClearMessages();
    }

    /// <summary>
    /// Cancela los cambios (resetea al último estado guardado).
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        HasUnsavedChanges = false;
        ClearMessages();
    }

    // --- Helpers privados ---

    private ServiceReport BuildReportFromForm()
    {
        var settings = _settingsService.CurrentSettings;

        return new ServiceReport
        {
            Client = new ClientInfo
            {
                Name = ClientName.Trim(),
                Phone = ClientPhone.Trim(),
                Email = ClientEmail.Trim(),
                Company = ClientCompany.Trim(),
                Address = ClientAddress.Trim(),
                Notes = ClientNotes.Trim()
            },
            Equipment = new EquipmentInfo
            {
                Brand = EquipmentBrand.Trim(),
                Model = EquipmentModel.Trim(),
                SerialNumber = SerialNumber.Trim(),
                EquipmentType = EquipmentType,
                ServiceReason = ServiceReason.Trim(),
                EquipmentNotes = EquipmentNotes.Trim(),
                OperatingSystem = DetectedOS,
                Architecture = DetectedArchitecture,
                Processor = DetectedProcessor,
                RamTotalGB = ParseRamGB(DetectedRam),
                PrimaryDisk = DetectedDisk,
                DiskCapacityGB = _detectedDiskFreeKnown
                    ? _detectedSystemVolumeCapacityGb : 0,
                DiskFreeGB = _detectedDiskFreeKnown
                    ? _detectedSystemVolumeFreeGb : 0,
                DiskFreeSpaceKnown = _detectedDiskFreeKnown,
                DiskType = DetectedDiskType,
                ComputerName = DetectedComputerName,
                CurrentUser = DetectedUser,
                WindowsEdition = DetectedEdition,
                WindowsVersion = WindowsVersion
            },
            Service = new ServiceInfo
            {
                ServiceDate = DateTime.Now,
                Reason = ServiceReason.Trim()
            },
            TechnicianName = settings.Company.TechnicianName
        };
    }

    private static double ParseRamGB(string ramText)
    {
        if (string.IsNullOrWhiteSpace(ramText))
            return 0;

        var match = Regex.Match(ramText, @"([\d.,]+)");
        if (match.Success && double.TryParse(match.Groups[1].Value.Replace(",", "."), out var gb))
            return gb;

        return 0;
    }

    private static double ParseDiskGB(string diskText)
    {
        return ParseRamGB(diskText); // Mismo formato
    }

    private string WindowsVersion { get; set; } = string.Empty;

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientName))
            return "El nombre del cliente es obligatorio.";

        if (string.IsNullOrWhiteSpace(ServiceReason))
            return "El motivo del servicio es obligatorio.";

        if (string.IsNullOrWhiteSpace(EquipmentType))
            return "Debe seleccionar un tipo de equipo.";

        if (!string.IsNullOrWhiteSpace(ClientEmail) && !IsValidEmail(ClientEmail))
            return "El formato del email no es válido.";

        return null;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            return Regex.IsMatch(email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
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

    // Detectar cambios
    partial void OnClientNameChanged(string value) => MarkDirty();
    partial void OnClientPhoneChanged(string value) => MarkDirty();
    partial void OnClientEmailChanged(string value) => MarkDirty();
    partial void OnClientCompanyChanged(string value) => MarkDirty();
    partial void OnClientAddressChanged(string value) => MarkDirty();
    partial void OnClientNotesChanged(string value) => MarkDirty();
    partial void OnEquipmentBrandChanged(string value) => MarkDirty();
    partial void OnEquipmentModelChanged(string value) => MarkDirty();
    partial void OnSerialNumberChanged(string value) => MarkDirty();
    partial void OnEquipmentTypeChanged(string value) => MarkDirty();
    partial void OnServiceReasonChanged(string value) => MarkDirty();
    partial void OnEquipmentNotesChanged(string value) => MarkDirty();

    private void MarkDirty()
    {
        HasUnsavedChanges = true;
        ClearMessages();
    }
}
