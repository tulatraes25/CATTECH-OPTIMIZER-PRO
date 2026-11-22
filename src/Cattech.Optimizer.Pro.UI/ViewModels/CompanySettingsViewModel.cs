using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Infrastructure.Smart;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de configuración: identidad de empresa/técnico,
/// herramientas externas (smartctl) y resumen técnico de rutas de la aplicación.
/// </summary>
public partial class CompanySettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IToolsConfigService? _toolsConfigService;
    private readonly Func<string, ISmartctlRunner>? _smartctlProbeFactory;
    private readonly string _appBaseDirectory;

    private AppSettings _originalSettings = new();
    private ToolsConfig _savedToolsConfig = ToolsConfig.CreateDefault();

    // --- Campos del formulario ---

    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _technicianName = string.Empty;

    [ObservableProperty]
    private string _taxId = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _whatsApp = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private string _logoPath = string.Empty;

    [ObservableProperty]
    private string _primaryColor = "#0078D4";

    [ObservableProperty]
    private string _footerLegend = string.Empty;

    // --- Estado de la UI (datos de empresa) ---

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
    private bool _hasLogo;

    // --- Herramientas externas: smartctl ---

    [ObservableProperty]
    private string _smartctlPath = string.Empty;

    [ObservableProperty]
    private bool _smartctlAutoDetect = true;

    [ObservableProperty]
    private bool _hasToolsChanges;

    [ObservableProperty]
    private string _toolsMessage = string.Empty;

    [ObservableProperty]
    private bool _toolsIsSuccess;

    [ObservableProperty]
    private bool _toolsIsWarning;

    [ObservableProperty]
    private bool _toolsIsError;

    [ObservableProperty]
    private bool _isProbing;

    // --- Resultado de la sonda de smartctl (CheckAvailabilityAsync) ---

    [ObservableProperty]
    private bool _hasProbeResult;

    [ObservableProperty]
    private bool _probeIsAvailable;

    [ObservableProperty]
    private string _probeStatusText = string.Empty;

    [ObservableProperty]
    private string _probeVersion = string.Empty;

    [ObservableProperty]
    private string _probeSupportsJsonText = string.Empty;

    [ObservableProperty]
    private string _probePath = string.Empty;

    [ObservableProperty]
    private string _probeCheckedAt = string.Empty;

    [ObservableProperty]
    private string _probeDetail = string.Empty;

    // --- Rutas técnicas de la aplicación ---

    /// <summary>Ruta efectiva del archivo config/herramientas.json.</summary>
    public string ToolsConfigFilePath { get; }

    /// <summary>Ruta del directorio config/.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Ruta del directorio data/.</summary>
    public string DataDirectory { get; }

    /// <summary>Ruta del directorio backups/.</summary>
    public string BackupsDirectory { get; }

    /// <summary>Ruta del directorio reports/.</summary>
    public string ReportsDirectory { get; }

    /// <summary>Ruta del directorio logs/.</summary>
    public string LogsDirectory { get; }

    /// <summary>Si existe el directorio de logs.</summary>
    public bool LogsDirectoryExists { get; }

    /// <summary>Resumen de rutas de la app para el panel técnico.</summary>
    public IReadOnlyList<AppPathEntry> AppPaths { get; }

    public CompanySettingsViewModel(
        ISettingsService settingsService,
        IToolsConfigService? toolsConfigService = null,
        string? appBaseDirectory = null,
        Func<string, ISmartctlRunner>? smartctlProbeFactory = null)
    {
        _settingsService = settingsService;
        _toolsConfigService = toolsConfigService;
        _smartctlProbeFactory = smartctlProbeFactory;

        var baseDirectory = string.IsNullOrWhiteSpace(appBaseDirectory)
            ? AppDomain.CurrentDomain.BaseDirectory
            : appBaseDirectory;
        _appBaseDirectory = baseDirectory.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        ToolsConfigFilePath = toolsConfigService?.ConfigFilePath
            ?? Path.Combine(_appBaseDirectory, "config", "herramientas.json");
        ConfigDirectory = Path.Combine(_appBaseDirectory, "config");
        DataDirectory = Path.Combine(_appBaseDirectory, "data");
        BackupsDirectory = Path.Combine(_appBaseDirectory, "backups");
        ReportsDirectory = Path.Combine(_appBaseDirectory, "reports");
        LogsDirectory = Path.Combine(_appBaseDirectory, "logs");
        LogsDirectoryExists = Directory.Exists(LogsDirectory);
        AppPaths = BuildAppPaths();
    }

    /// <summary>
    /// Carga la configuración guardada (datos de empresa y herramientas externas).
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            _originalSettings = settings;
            LoadFromSettings(settings);
            HasUnsavedChanges = false;
            ClearMessages();
        }
        catch (Exception)
        {
            // Settings/IO exceptions can include private local file paths.
            ShowError("No se pudo cargar la configuración. Revisá el almacenamiento e intentá nuevamente.");
        }

        await LoadToolsAsync();
    }

    /// <summary>
    /// F5 en Configuración: recarga desde disco. No descarta cambios sin guardar.
    /// </summary>
    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (HasUnsavedChanges || HasToolsChanges)
        {
            ShowError("Hay cambios sin guardar. Guardá o usá Cancelar antes de recargar desde disco (F5).");
            return;
        }

        await LoadAsync();

        if (HasError || ToolsIsError)
        {
            return;
        }

        ShowSuccess("Configuración recargada desde disco.");
    }

    /// <summary>
    /// Selecciona un archivo de logo usando el diálogo de archivos.
    /// </summary>
    [RelayCommand]
    private void BrowseLogo()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar logo de la empresa",
                Filter = "Imágenes|*.png;*.jpg;*.jpeg|Todos los archivos|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                LogoPath = dialog.FileName;
                HasLogo = true;
                HasUnsavedChanges = true;
                ClearMessages();
            }
        }
        catch (Exception)
        {
            ShowError("No se pudo abrir el selector de logo. Reintentá la operación.");
        }
    }

    /// <summary>
    /// Guarda la configuración actual en disco.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        ClearMessages();

        // Validaciones
        var validationError = Validate();
        if (!string.IsNullOrEmpty(validationError))
        {
            ShowError(validationError);
            return;
        }

        try
        {
            var settings = BuildSettingsFromForm();
            await _settingsService.SaveSettingsAsync(settings);
            _originalSettings = settings;
            HasUnsavedChanges = false;
            ShowSuccess("Configuración guardada correctamente.");
        }
        catch (Exception)
        {
            // Never claim success or surface filesystem paths on a failed save.
            HasUnsavedChanges = true;
            ShowError("No se pudo guardar la configuración. Los cambios siguen sin guardar; revisá los permisos y reintentá.");
        }
    }

    /// <summary>
    /// Cancela los cambios y recarga la configuración original (empresa y herramientas).
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        LoadFromSettings(_originalSettings);
        ApplyToolsConfig(_savedToolsConfig);
        HasUnsavedChanges = false;
        HasToolsChanges = false;
        HasProbeResult = false;
        ClearMessages();
        ClearToolsMessages();
    }

    // --- Herramientas externas ---

    /// <summary>
    /// Elige smartctl.exe con el diálogo de archivos; la ruta explícita tiene
    /// prioridad sobre la autodetección al resolver el binario.
    /// </summary>
    [RelayCommand]
    private void BrowseSmartctl()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar smartctl.exe",
                Filter = "smartctl.exe|smartctl.exe|Ejecutables (*.exe)|*.exe|Todos los archivos (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                SmartctlPath = dialog.FileName;
                ClearToolsMessages();
            }
        }
        catch (Exception)
        {
            ShowToolsError("No se pudo abrir el selector de smartctl.exe.");
        }
    }

    /// <summary>
    /// Guarda config/herramientas.json de forma atómica con la configuración del
    /// formulario. Una ruta inexistente se advierte pero no bloquea el guardado.
    /// </summary>
    [RelayCommand]
    private async Task SaveToolsAsync()
    {
        ClearToolsMessages();

        if (_toolsConfigService is null)
        {
            ShowToolsError("El editor de herramientas no está disponible en este contexto.");
            return;
        }

        var path = (SmartctlPath ?? string.Empty).Trim();

        if (path.Length == 0 && !SmartctlAutoDetect)
        {
            ShowToolsError("Configuración inválida: indicá la ruta de smartctl.exe o activá \"Autodetectar\".");
            return;
        }

        var pathMissing = path.Length > 0 && !File.Exists(path);

        try
        {
            await _toolsConfigService.SaveAsync(new ToolsConfig
            {
                SmartctlPath = path,
                SmartctlAutoDetect = SmartctlAutoDetect
            });
        }
        catch (UnauthorizedAccessException)
        {
            ShowToolsError("Sin permisos de escritura para guardar config/herramientas.json. Revisá la carpeta de configuración.");
            return;
        }
        catch (IOException)
        {
            ShowToolsError("No se pudo guardar config/herramientas.json: archivo en uso o almacenamiento no disponible.");
            return;
        }
        catch (Exception)
        {
            ShowToolsError("No se pudo guardar la configuración de herramientas.");
            return;
        }

        _savedToolsConfig = new ToolsConfig
        {
            SmartctlPath = path,
            SmartctlAutoDetect = SmartctlAutoDetect
        };
        HasToolsChanges = false;

        if (pathMissing)
        {
            ShowToolsWarning(
                "Configuración guardada. Advertencia: la ruta de smartctl.exe no existe en este equipo; smartctl no podrá ejecutarse hasta corregirla.");
        }
        else
        {
            ShowToolsSuccess("Configuración de herramientas guardada.");
        }
    }

    /// <summary>
    /// Prueba smartctl con la configuración actual del formulario usando el runner
    /// real (CheckAvailabilityAsync: solo lectura, no repara ni modifica discos).
    /// </summary>
    [RelayCommand]
    private async Task TestSmartctlAsync()
    {
        ClearToolsMessages();
        HasProbeResult = false;

        var path = (SmartctlPath ?? string.Empty).Trim();

        if (path.Length == 0 && !SmartctlAutoDetect)
        {
            ShowToolsError("Autodetección desactivada y sin ruta: no hay smartctl que probar.");
            return;
        }

        if (path.Length > 0 && !File.Exists(path))
        {
            ShowToolsWarning("La ruta indicada no existe; no se ejecutó smartctl. Corregí la ruta o activá autodetección.");
            return;
        }

        IsProbing = true;
        try
        {
            var runner = CreateProbeRunner(path);
            var availability = await runner.CheckAvailabilityAsync();

            ProbePath = string.IsNullOrWhiteSpace(availability.SmartctlPath)
                ? "N/D"
                : availability.SmartctlPath;
            ProbeVersion = string.IsNullOrWhiteSpace(availability.Version)
                ? "N/D"
                : availability.Version;
            ProbeSupportsJsonText = availability.SupportsJson ? "sí" : "no";
            ProbeCheckedAt = availability.CheckedAt.ToString("HH:mm:ss");
            ProbeIsAvailable = availability.IsAvailable;
            ProbeStatusText = availability.IsAvailable ? "smartctl: OK" : "smartctl: no disponible";
            ProbeDetail = string.IsNullOrWhiteSpace(availability.ErrorMessage)
                ? "CheckAvailabilityAsync completado sin errores de invocación."
                : availability.ErrorMessage;
            HasProbeResult = true;

            if (availability.IsAvailable)
            {
                ShowToolsSuccess("Sonda completada: smartctl disponible.");
            }
            else
            {
                ShowToolsWarning("Sonda completada: smartctl no disponible.");
            }
        }
        catch (Exception)
        {
            ShowToolsError("No se pudo ejecutar la sonda de smartctl. Revisá la ruta y los permisos.");
        }
        finally
        {
            IsProbing = false;
        }
    }

    /// <summary>Abre una carpeta de la aplicación en el Explorador de Windows.</summary>
    [RelayCommand]
    private void OpenFolder(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                throw new DirectoryNotFoundException();
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Nunca exponer rutas absolutas del equipo en el mensaje de error.
            ShowError("No se pudo abrir la carpeta. Verificá que exista y que el Explorador de Windows esté disponible.");
        }
    }

    /// <summary>Copia una ruta completa al portapapeles.</summary>
    [RelayCommand]
    private void CopyPath(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Clipboard.SetText(path);
            }
        }
        catch (Exception)
        {
            ShowError("No se pudo copiar la ruta al portapapeles. Reintentá la operación.");
        }
    }

    // --- Helpers privados ---

    private async Task LoadToolsAsync()
    {
        if (_toolsConfigService is null)
        {
            return;
        }

        try
        {
            var config = await _toolsConfigService.LoadAsync();
            _savedToolsConfig = config.Clone();
            ApplyToolsConfig(config);
            ClearToolsMessages();
        }
        catch (Exception)
        {
            ShowToolsError("No se pudo cargar la configuración de herramientas.");
        }
    }

    private void ApplyToolsConfig(ToolsConfig config)
    {
        SmartctlPath = config.SmartctlPath;
        SmartctlAutoDetect = config.SmartctlAutoDetect;
        HasToolsChanges = false;
    }

    private ISmartctlRunner CreateProbeRunner(string configuredPath)
    {
        if (_smartctlProbeFactory is not null)
        {
            return _smartctlProbeFactory(configuredPath);
        }

        if (configuredPath.Length > 0)
        {
            return new SmartctlRunner(configuredPath, _appBaseDirectory);
        }

        // Autodetección desde el formulario: no permitir que un smartctlPath
        // persistido (que el formulario ya no declara) imponga la ruta de la sonda.
        var baseDirectory = string.IsNullOrWhiteSpace(_savedToolsConfig.SmartctlPath)
            ? _appBaseDirectory
            : Path.Combine(Path.GetTempPath(), "cattech-probe-no-tools-config");
        return new SmartctlRunner(null, baseDirectory);
    }

    private IReadOnlyList<AppPathEntry> BuildAppPaths()
    {
        var paths = new List<AppPathEntry>
        {
            new("datos", DataDirectory),
            new("backups", BackupsDirectory),
            new("informes", ReportsDirectory),
            new("config", ConfigDirectory)
        };

        if (LogsDirectoryExists)
        {
            paths.Add(new AppPathEntry("logs", LogsDirectory));
        }

        return paths;
    }

    private void LoadFromSettings(AppSettings settings)
    {
        CompanyName = settings.Company.Name;
        TechnicianName = settings.Company.TechnicianName;
        TaxId = settings.Company.TaxId;
        Phone = settings.Company.Phone;
        WhatsApp = settings.Company.WhatsApp;
        Email = settings.Company.Email;
        Address = settings.Company.Address;
        City = settings.Company.City;
        LogoPath = settings.Company.LogoPath;
        PrimaryColor = settings.Company.PrimaryColor;
        FooterLegend = settings.Company.FooterLegend;
        HasLogo = !string.IsNullOrEmpty(LogoPath);
    }

    private AppSettings BuildSettingsFromForm()
    {
        return new AppSettings
        {
            Company = new CompanyInfo
            {
                Name = CompanyName.Trim(),
                TechnicianName = TechnicianName.Trim(),
                TaxId = TaxId.Trim(),
                Phone = Phone.Trim(),
                WhatsApp = WhatsApp.Trim(),
                Email = Email.Trim(),
                Address = Address.Trim(),
                City = City.Trim(),
                LogoPath = LogoPath.Trim(),
                PrimaryColor = PrimaryColor.Trim(),
                FooterLegend = FooterLegend.Trim()
            },
            Language = _originalSettings.Language,
            Theme = _originalSettings.Theme
        };
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
            return "El nombre comercial es obligatorio.";

        if (string.IsNullOrWhiteSpace(TechnicianName))
            return "El nombre del técnico es obligatorio.";

        if (!string.IsNullOrWhiteSpace(Email) && !IsValidEmail(Email))
            return "El formato del email no es válido.";

        // The report renderer rejects arbitrary CSS. Tell the operator at
        // save time rather than silently losing the intended brand color.
        if (!IsValidBrandColor(PrimaryColor?.Trim()))
            return "El color principal debe tener formato hexadecimal #RGB o #RRGGBB (por ejemplo #0078D4).";

        return null;
    }

    public static bool IsValidBrandColor(string? color)
    {
        if (color is null || color.Length is not (4 or 7)
            || color[0] != '#')
            return false;

        for (var i = 1; i < color.Length; i++)
            if (!Uri.IsHexDigit(color[i]))
                return false;
        return true;
    }

    /// <summary>
    /// Valida formato básico de email.
    /// </summary>
    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            // Validación simple: contiene @ y al menos un .
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

    private void ShowToolsSuccess(string message)
    {
        ToolsMessage = message;
        ToolsIsSuccess = true;
        ToolsIsWarning = false;
        ToolsIsError = false;
    }

    private void ShowToolsWarning(string message)
    {
        ToolsMessage = message;
        ToolsIsSuccess = false;
        ToolsIsWarning = true;
        ToolsIsError = false;
    }

    private void ShowToolsError(string message)
    {
        ToolsMessage = message;
        ToolsIsSuccess = false;
        ToolsIsWarning = false;
        ToolsIsError = true;
    }

    private void ClearToolsMessages()
    {
        ToolsMessage = string.Empty;
        ToolsIsSuccess = false;
        ToolsIsWarning = false;
        ToolsIsError = false;
    }

    // Detectar cambios en campos del formulario
    partial void OnCompanyNameChanged(string value) => MarkDirty();
    partial void OnTechnicianNameChanged(string value) => MarkDirty();
    partial void OnTaxIdChanged(string value) => MarkDirty();
    partial void OnPhoneChanged(string value) => MarkDirty();
    partial void OnWhatsAppChanged(string value) => MarkDirty();
    partial void OnEmailChanged(string value) => MarkDirty();
    partial void OnAddressChanged(string value) => MarkDirty();
    partial void OnCityChanged(string value) => MarkDirty();
    partial void OnPrimaryColorChanged(string value) => MarkDirty();
    partial void OnFooterLegendChanged(string value) => MarkDirty();

    partial void OnSmartctlPathChanged(string value) => MarkToolsDirty();
    partial void OnSmartctlAutoDetectChanged(bool value) => MarkToolsDirty();

    private void MarkDirty()
    {
        HasUnsavedChanges = true;
        ClearMessages();
    }

    private void MarkToolsDirty()
    {
        HasToolsChanges = true;
        ClearToolsMessages();
    }

    /// <summary>
    /// Entrada del resumen técnico de rutas de la app.
    /// </summary>
    public sealed class AppPathEntry
    {
        public AppPathEntry(string label, string path)
        {
            Label = label;
            Path = path;
        }

        public string Label { get; }

        public string Path { get; }

        public bool Exists => Directory.Exists(Path);

        public string ExistsText => Exists ? "existe" : "falta";
    }
}
