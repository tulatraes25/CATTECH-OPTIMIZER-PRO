using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Configuration;

namespace Cattech.Optimizer.Pro.Infrastructure.Helpers;

/// <summary>
/// Helper compartido para obtener datos de configuración.
/// Evita duplicación de lógica entre servicios.
/// </summary>
public static class SettingsHelper
{
    /// <summary>
    /// Obtiene el nombre del técnico desde la configuración.
    /// </summary>
    public static async Task<string> GetTechnicianNameAsync(ISettingsService settingsService)
    {
        try
        {
            var settings = await settingsService.LoadSettingsAsync();
            return settings.Company.TechnicianName;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtiene el nombre de la empresa desde la configuración.
    /// </summary>
    public static async Task<string> GetCompanyNameAsync(ISettingsService settingsService)
    {
        try
        {
            var settings = await settingsService.LoadSettingsAsync();
            return settings.Company.Name;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Obtiene la configuración completa.
    /// </summary>
    public static async Task<AppSettings> GetSettingsAsync(ISettingsService settingsService)
    {
        try
        {
            return await settingsService.LoadSettingsAsync();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
