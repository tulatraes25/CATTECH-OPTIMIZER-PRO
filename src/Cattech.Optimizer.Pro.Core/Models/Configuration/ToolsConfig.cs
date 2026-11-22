namespace Cattech.Optimizer.Pro.Core.Models.Configuration;

/// <summary>
/// Configuración de herramientas externas de la aplicación.
/// Se persiste en <c>config/herramientas.json</c> con el formato
/// <c>{"smartctlPath":"...","smartctlAutoDetect":true}</c>.
/// smartctl se invoca como binario externo: la aplicación no integra código GPL.
/// </summary>
public sealed class ToolsConfig
{
    /// <summary>
    /// Ruta explícita a smartctl.exe. Vacía significa resolverlo por autodetección
    /// (rutas locales conocidas, instalación estándar de smartmontools o PATH).
    /// </summary>
    public string SmartctlPath { get; set; } = string.Empty;

    /// <summary>
    /// Si se permite autodetectar smartctl cuando la ruta explícita no es válida.
    /// Por defecto <c>true</c> (mismo comportamiento que el lector de herramientas).
    /// </summary>
    public bool SmartctlAutoDetect { get; set; } = true;

    /// <summary>
    /// Configuración por defecto: sin ruta fija y con autodetección habilitada.
    /// </summary>
    public static ToolsConfig CreateDefault() => new();

    /// <summary>
    /// Copia independiente de la configuración, para no compartir estado mutable
    /// entre el formulario y la última versión persistida.
    /// </summary>
    public ToolsConfig Clone() => new()
    {
        SmartctlPath = SmartctlPath,
        SmartctlAutoDetect = SmartctlAutoDetect
    };
}
