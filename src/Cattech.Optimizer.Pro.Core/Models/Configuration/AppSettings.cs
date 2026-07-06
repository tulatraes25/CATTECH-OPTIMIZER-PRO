namespace Cattech.Optimizer.Pro.Core.Models.Configuration;

/// <summary>
/// Información de la empresa que utiliza la herramienta.
/// </summary>
public class CompanyInfo
{
    /// <summary>
    /// Nombre de la empresa.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Logo de la empresa en base64 (PNG/JPG).
    /// </summary>
    public string LogoBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Dirección de la empresa.
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono de contacto.
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// Email de contacto.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Sitio web de la empresa.
    /// </summary>
    public string Website { get; set; } = string.Empty;
}

/// <summary>
/// Información del técnico que utiliza la herramienta.
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
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Información de la empresa.
    /// </summary>
    public CompanyInfo Company { get; set; } = new();

    /// <summary>
    /// Información del técnico.
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
