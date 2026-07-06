using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// ViewModel para la pantalla de configuración de empresa/técnico.
/// </summary>
public partial class CompanySettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private AppSettings _originalSettings = new();

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
    private bool _hasLogo;

    public CompanySettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Carga la configuración guardada al inicializar la vista.
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
        catch (Exception ex)
        {
            ShowError($"Error al cargar configuración: {ex.Message}");
        }
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
        catch (Exception ex)
        {
            ShowError($"Error al seleccionar archivo: {ex.Message}");
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
        catch (Exception ex)
        {
            // TODO: Integrar con módulo de logging cuando esté disponible
            ShowError($"Error al guardar: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancela los cambios y recarga la configuración original.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        LoadFromSettings(_originalSettings);
        HasUnsavedChanges = false;
        ClearMessages();
    }

    // --- Helpers privados ---

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

        return null;
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

    private void MarkDirty()
    {
        HasUnsavedChanges = true;
        ClearMessages();
    }
}
