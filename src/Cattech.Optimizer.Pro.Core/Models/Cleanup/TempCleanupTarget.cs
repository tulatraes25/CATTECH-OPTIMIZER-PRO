using System.ComponentModel;

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
public class TempCleanupTarget : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>
    /// Updates the bound CheckBox after bulk selection or safety rejection.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
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
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

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
    // Canonical filename policy shared by scan and native deletion. Any
    // unfamiliar caller of WindowsScopedFileDelete must respect the same
    // lock/PID/socket exclusions; this does not rely on the calling UI.
    private static readonly string[] ProtectedNameFragments =
        [".lock", ".pid", ".socket"];

    public static bool IsProtectedTemporaryFileName(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
        || ProtectedNameFragments.Any(fragment =>
            fileName.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    // Minimum retention before an explicitly confirmed temporary-file cleanup.
    // Both NTFS creation and modification times must be older than this limit.
    public const int MinimumAgeDays = 7;

    /// <summary>
    /// TEMP/TMP can redirect Path.GetTempPath. Automatic cleanup requires
    /// the independent OS-known per-user LocalApplicationData\Temp location.
    /// Custom temporary roots need separate security design and approval.
    /// </summary>
    public static bool IsTrustedUserTempPath(string? candidate)
    {
        var known = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        return OperatingSystem.IsWindows()
            && !string.IsNullOrWhiteSpace(known)
            && IsExactTrustedPath(candidate, Path.Combine(known, "Temp"));
    }

    /// <summary>
    /// Determine the actual Windows installation folder, not hardcoded C:\.
    /// </summary>
    public static bool IsTrustedWindowsTempPath(string? candidate)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return OperatingSystem.IsWindows()
            && !string.IsNullOrWhiteSpace(windows)
            && IsExactTrustedPath(candidate, Path.Combine(windows, "Temp"));
    }

    private static bool IsExactTrustedPath(string? candidate, string expected)
    {
        if (string.IsNullOrWhiteSpace(candidate)
            || candidate.StartsWith(@"\\", StringComparison.Ordinal)
            || expected.StartsWith(@"\\", StringComparison.Ordinal))
            return false;

        try
        {
            if (!Path.IsPathFullyQualified(candidate)
                || !Path.IsPathFullyQualified(expected))
                return false;

            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException
            or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static List<TempCleanupTarget> GetDefaultTargets()
    {
        var userTemp = Path.GetFullPath(Path.GetTempPath());
        var windowsRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var windowsTemp = string.IsNullOrWhiteSpace(windowsRoot)
            ? string.Empty : Path.Combine(windowsRoot, "Temp");
        var userTempIsTrusted = IsTrustedUserTempPath(userTemp);

        return
        [
            new TempCleanupTarget
            {
                Id = "USER_TEMP",
                DisplayName = "Temporales del usuario",
                Path = userTemp,
                Description = "Solo archivos directos creados y modificados hace al menos 7 días. Revisá la selección antes de borrar.",
                IsSystemLocation = false,
                IsOptional = false,
                IsSelected = userTempIsTrusted,
                RiskLevel = CleanupRiskLevel.Low,
                ScanNotes = userTempIsTrusted ? string.Empty
                    : "No disponible: la carpeta TEMP no coincide con el perfil local."
            },
            new TempCleanupTarget
            {
                Id = "WINDOWS_TEMP",
                DisplayName = "Temporales de Windows",
                Path = windowsTemp,
                Description = "Archivos directos con al menos 7 días de antigüedad; puede requerir administrador.",
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
                Description = "Caché de miniaturas: solo archivos creados y modificados hace más de 7 días.",
                IsSystemLocation = false,
                IsOptional = true,
                IsSelected = false,
                RiskLevel = CleanupRiskLevel.Low,
                ScanNotes = "Solo archivos thumbcache_*.db"
            }
        ];
    }
}
