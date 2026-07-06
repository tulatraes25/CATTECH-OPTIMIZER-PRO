namespace Cattech.Optimizer.Pro.Core.Models.Configuration;

/// <summary>
/// Información de la empresa que utiliza la herramienta.
/// </summary>
public class CompanyInfo
{
    /// <summary>
    /// Nombre comercial de la empresa.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Técnico responsable.
    /// </summary>
    public string TechnicianName { get; set; } = string.Empty;

    /// <summary>
    /// CUIT o DNI del técnico/empresa.
    /// </summary>
    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono de contacto.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Número de WhatsApp.
    /// </summary>
    public string WhatsApp { get; set; } = string.Empty;

    /// <summary>
    /// Email de contacto.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Dirección de la empresa.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Ciudad.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Ruta al archivo de logo (PNG/JPG).
    /// TODO: En futuras versiones, copiar el logo a config/assets/logo_empresa.*
    /// para mantener las rutas relativas y facilitar la portabilidad.
    /// </summary>
    public string LogoPath { get; set; } = string.Empty;

    /// <summary>
    /// Color principal de la marca en formato hex (ej: #0078D4).
    /// </summary>
    public string PrimaryColor { get; set; } = "#0078D4";

    /// <summary>
    /// Leyenda que aparece al pie de los informes.
    /// </summary>
    public string FooterLegend { get; set; } = string.Empty;
}

/// <summary>
/// Información del técnico que utiliza la herramienta.
/// Mantiene compatibilidad con la estructura anterior.
/// </summary>
public class TechnicianInfo
{
    /// <summary>
    /// Nombre del técnico.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ID o matrícula del técnico.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Email del técnico.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Configuración general de la aplicación.
/// Se persiste en config/empresa.json.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Información de la empresa.
    /// </summary>
    public CompanyInfo Company { get; set; } = new();

    /// <summary>
    /// Información del técnico (compatibilidad).
    /// </summary>
    public TechnicianInfo Technician { get; set; } = new();

    /// <summary>
    /// Idioma de la aplicación.
    /// </summary>
    public string Language { get; set; } = "es-AR";

    /// <summary>
    /// Tema de la interfaz (light/dark).
    /// </summary>
    public string Theme { get; set; } = "light";
}
