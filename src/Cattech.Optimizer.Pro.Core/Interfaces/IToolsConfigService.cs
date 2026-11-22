using Cattech.Optimizer.Pro.Core.Models.Configuration;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>
/// Persiste y carga la configuración de herramientas externas
/// (<c>config/herramientas.json</c>).
/// </summary>
public interface IToolsConfigService
{
    /// <summary>
    /// Ruta absoluta del archivo de configuración que usa el servicio.
    /// </summary>
    string ConfigFilePath { get; }

    /// <summary>
    /// Carga la configuración desde disco. Si el archivo no existe o es inválido
    /// devuelve los valores por defecto (autodetección habilitada, sin ruta fija).
    /// No lanza por archivo ausente ni por JSON corrupto.
    /// </summary>
    Task<ToolsConfig> LoadAsync();

    /// <summary>
    /// Guarda la configuración de forma atómica (archivo temporal en el mismo
    /// directorio y reemplazo). Crea el directorio <c>config</c> si no existe.
    /// Propaga las excepciones de E/S para que la UI pueda mostrar el error.
    /// </summary>
    Task SaveAsync(ToolsConfig config);
}
