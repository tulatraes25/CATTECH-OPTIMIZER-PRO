using Cattech.Optimizer.Pro.Core.Models.Configuration;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Interfaz para persistir y cargar configuración.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Carga la configuración desde disco.
    /// </summary>
    Task<AppSettings> LoadSettingsAsync();

    /// <summary>
    /// Guarda la configuración en disco.
    /// </summary>
    Task SaveSettingsAsync(AppSettings settings);

    /// <summary>
    /// Obtiene la configuración actual (cacheada en memoria).
    /// </summary>
    AppSettings CurrentSettings { get; }

    /// <summary>
    /// Evento que se dispara cuando cambia la configuración.
    /// </summary>
    event EventHandler<AppSettings>? SettingsChanged;
}
