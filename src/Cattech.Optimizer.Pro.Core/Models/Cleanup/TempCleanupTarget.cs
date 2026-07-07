namespace Cattech.Optimizer.Pro.Core.Models.Cleanup;

/// <summary>
/// Nivel de riesgo de una ubicación de limpieza.
/// </summary>
public enum CleanupRiskLevel
{
    Low,
    Medium
}

/// <summary>
/// Ubicación de archivos temporales que puede ser limpiada.
/// </summary>
public class TempCleanupTarget
{
    /// <summary>
    /// ID único del target.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

    /// <summary>
    /// Nombre para mostrar.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta de la ubicación.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Descripción de qué contiene.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Si es una ubicación del sistema (requiere permisos).
    /// </summary>
    public bool IsSystemLocation { get; set; }

    /// <summary>
    /// Si es opcional (puede ser seleccionada o no).
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Si está seleccionado para limpieza.
    /// </summary>
    public bool IsSelected { get; set; }

    /// <summary>
    /// Tamaño estimado en bytes.
    /// </summary>
    public long EstimatedSizeBytes { get; set; }

    /// <summary>
    /// Tamaño estimado en MB.
    /// </summary>
    public double EstimatedSizeMB => Math.Round((double)EstimatedSizeBytes / (1024 * 1024), 2);

    /// <summary>
    /// Cantidad aproximada de archivos.
    /// </summary>
    public int EstimatedFileCount { get; set; }

    /// <summary>
    /// Nivel de riesgo.
    /// </summary>
    public CleanupRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Si la ubicación existe y es accesible.
    /// </summary>
    public bool IsAccessible { get; set; }

    /// <summary>
    /// Si fue escaneado exitosamente.
    /// </summary>
    public bool IsScanned { get; set; }

    /// <summary>
    /// Notas del escaneo (ej: "Requiere permisos de admin").
    /// </summary>
    public string ScanNotes { get; set; } = string.Empty;
}

/// <summary>
/// Objetos de limpieza predefinidos para v0.1.
/// </summary>
public static class CleanupTargets
{
    public static List<TempCleanupTarget> GetDefaultTargets()
    {
        var tempPath = Path.GetTempPath();
        var userTemp = Path.GetFullPath(tempPath);
        var windowsTemp = @"C:\Windows\Temp";

        return
        [
            new TempCleanupTarget
            {
                Id = "USER_TEMP",
                DisplayName = "Temporales del usuario",
                Path = userTemp,
                Description = "Archivos temporales creados por aplicaciones del usuario. Generalmente seguro de limpiar.",
                IsSystemLocation = false,
                IsOptional = false,
                IsSelected = true,
                RiskLevel = CleanupRiskLevel.Low
            },
            new TempCleanupTarget
            {
                Id = "WINDOWS_TEMP",
                DisplayName = "Temporales de Windows",
                Path = windowsTemp,
                Description = "Archivos temporales del sistema. Puede requerir permisos de administrador.",
                IsSystemLocation = true,
                IsOptional = false,
                IsSelected = false,
                RiskLevel = CleanupRiskLevel.Low,
                ScanNotes = "Puede requerir permisos de administrador"
            },
            new TempCleanupTarget
            {
                Id = "RECYCLE_BIN",
                DisplayName = "Papelera de reciclaje",
                Path = @"::{645FF040-5081-101B-9F08-00AA002F954E}",
                Description = "Archivos eliminados que aún están en la papelera. Seleccione solo si está seguro.",
                IsSystemLocation = false,
                IsOptional = true,
                IsSelected = false,
                RiskLevel = CleanupRiskLevel.Medium,
                ScanNotes = "Opcional: solo limpiar si está seguro"
            },
            new TempCleanupTarget
            {
                Id = "THUMBNAILS",
                DisplayName = "Caché de miniaturas",
                Path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\Windows\Explorer"),
                Description = "Caché de miniaturas de imágenes y archivos. Se regenerará automáticamente.",
                IsSystemLocation = false,
                IsOptional = true,
                IsSelected = false,
                RiskLevel = CleanupRiskLevel.Low,
                ScanNotes = "Solo archivos thumbcache_*.db"
            }
        ];
    }
}
