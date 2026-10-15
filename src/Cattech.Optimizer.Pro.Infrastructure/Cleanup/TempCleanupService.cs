using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;

namespace Cattech.Optimizer.Pro.Infrastructure.Cleanup;

/// <summary>
/// Implementación de ITempCleanupService.
/// Escanea y limpia archivos temporales de forma segura.
/// </summary>
public class TempCleanupService : ITempCleanupService
{
    private readonly string _baseDirectory;
    private readonly string _resultsDirectory;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Patrones de archivos que NO se deben borrar
    private static readonly string[] ProtectedPatterns =
    [
        "*.lock", "*.pid", "*.socket"
    ];

    // Extensiones de caché de miniaturas
    private static readonly string[] ThumbnailExtensions =
    [
        ".db"
    ];

    public TempCleanupService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _resultsDirectory = Path.Combine(_baseDirectory, "data", "cleanup-results");
    }

    /// <inheritdoc/>
    public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null)
    {
        var targets = CleanupTargets.GetDefaultTargets();

        progress?.Report(10);

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];

            // Verificar accesibilidad
            if (target.Id == "RECYCLE_BIN")
            {
                // Disable until a scoped Shell operation and its explicit
                // confirmation have passed dedicated safety review.
                target.IsAccessible = false;
                target.IsSelected = false;
                target.IsScanned = true;
                target.ScanNotes = "No disponible: la limpieza segura de Papelera no está implementada.";
                target.EstimatedSizeBytes = 0;
                target.EstimatedFileCount = 0;
            }
            else if ((target.Id == "USER_TEMP"
                    && !CleanupTargets.IsTrustedUserTempPath(target.Path))
                || (target.Id == "WINDOWS_TEMP"
                    && !CleanupTargets.IsTrustedWindowsTempPath(target.Path)))
            {
                // A redirected environment-dependent target must not even be scanned.
                target.IsAccessible = false;
                target.IsSelected = false;
                target.IsScanned = true;
                target.ScanNotes = "No disponible: la ruta TEMP no es una carpeta autorizada.";
                target.EstimatedSizeBytes = 0;
                target.EstimatedFileCount = 0;
            }
            else if (target.Id == "THUMBNAILS")
            {
                ScanThumbnailsFolder(target);
            }
            else
            {
                ScanFolder(target);
            }

            progress?.Report(10 + (int)((i + 1.0) / targets.Count * 80));
        }

        progress?.Report(100);
        return Task.FromResult(targets);
    }

    /// <summary>
    /// Snapshot only the selected scope, copying all scalar metadata needed by
    /// validation, execution and audit. The caller owns the original objects;
    /// no mutable reference to one is retained after this method returns.
    /// </summary>
    internal static List<TempCleanupTarget> CaptureSelectedTargets(
        IEnumerable<TempCleanupTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        return targets.Where(target => target.IsSelected)
            .Select(target => new TempCleanupTarget
            {
                Id = target.Id,
                DisplayName = target.DisplayName,
                Description = target.Description,
                Path = target.Path,
                IsSelected = true,
                IsScanned = target.IsScanned,
                IsAccessible = target.IsAccessible,
                IsSystemLocation = target.IsSystemLocation,
                IsOptional = target.IsOptional,
                EstimatedSizeBytes = target.EstimatedSizeBytes,
                EstimatedFileCount = target.EstimatedFileCount,
                RiskLevel = target.RiskLevel,
                ScanNotes = target.ScanNotes
            }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TempCleanupResult> CleanupAsync(
        IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null)
    {
        var result = new TempCleanupResult
        {
            StartedAt = DateTime.Now
        };

        // Capture immutable-by-ownership copies before preflight or any await.
        // WPF, plugins and callers retain mutable TempCleanupTarget references;
        // their later edits must not redirect or broaden an approved cleanup.
        var selectedTargets = CaptureSelectedTargets(targets);
        var duplicateIds = selectedTargets.GroupBy(t => t.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        // All-or-nothing preflight: reject duplicates and unsafe roots before
        // starting any deletion, even when other targets appear legitimate.
        var rejectedTargets = selectedTargets.Select(target =>
            (Target: target, Reason: duplicateIds.Contains(target.Id)
                ? "La ubicación de limpieza está duplicada."
                : GetTargetRejectionReason(target)))
            .Where(entry => entry.Reason is not null)
            .ToArray();
        if (rejectedTargets.Length > 0)
        {
            foreach (var target in selectedTargets)
            {
                result.TargetResults.Add(new TargetResult
                {
                    TargetId = target.Id,
                    TargetName = target.DisplayName,
                    Path = target.Path,
                    IsSuccess = false
                });
            }

            foreach (var rejected in rejectedTargets)
            {
                var unsupported = rejected.Target.Id == "RECYCLE_BIN";
                result.Errors.Add(new CleanupError
                {
                    // Do not expose an untrusted absolute path in diagnostics.
                    FilePath = unsupported ? "RecycleBin" : rejected.Target.Id,
                    Message = rejected.Reason!,
                    ErrorType = unsupported ? "UnsupportedOperation" : "UnsafeTarget"
                });
            }

            result.FinishedAt = DateTime.Now;
            result.TechnicianName = await GetTechnicianNameAsync();
            return result;
        }

        int processedCount = 0;

        foreach (var target in selectedTargets)
        {
            var targetResult = new TargetResult
            {
                TargetId = target.Id,
                TargetName = target.DisplayName,
                Path = target.Path
            };

            var errorsBeforeTarget = result.Errors.Count;
            try
            {
                if (target.Id == "RECYCLE_BIN")
                {
                    // Defensive server-side check: reject selected targets even
                    // if a caller bypasses the disabled checkbox in the UI.
                    // No commands, Shell API, or filesystem mutations.
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = "RecycleBin",
                        Message = "La limpieza de la Papelera está deshabilitada por seguridad.",
                        ErrorType = "UnsupportedOperation"
                    });
                }
                else if (target.Id == "THUMBNAILS")
                {
                    CleanThumbnailsFolder(target, targetResult, result);
                }
                else
                {
                    await CleanFolderAsync(target, targetResult, result);
                }
            }
            catch (Exception)
            {
                targetResult.IsSuccess = false;
                result.Errors.Add(new CleanupError
                {
                    FilePath = target.Id,
                    Message = "La limpieza de esta ubicación no pudo completarse.",
                    ErrorType = "CleanError"
                });
            }

            targetResult.IsSuccess = targetResult.FailedFiles == 0
                && result.Errors.Count == errorsBeforeTarget;
            result.TargetResults.Add(targetResult);

            processedCount++;
            progress?.Report((int)((double)processedCount / selectedTargets.Count * 100));
        }

        result.FinishedAt = DateTime.Now;
        result.TechnicianName = await GetTechnicianNameAsync();

        return result;
    }

    /// <inheritdoc/>
    public async Task<string> SaveResultAsync(TempCleanupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureDirectoryExists(_resultsDirectory);

        // A second-resolution timestamp, even combined with a model-supplied
        // ID, is not a unique or safe audit-file identity across instances.
        var token = Guid.NewGuid().ToString("N");
        var fileName = $"cleanup-result-{result.StartedAt:yyyyMMdd-HHmmss}-{token}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);
        var staging = Path.Combine(_resultsDirectory, $".cattech-cleanup-{token}.tmp");
        var json = JsonSerializer.Serialize(result, SerializerOptions);

        try
        {
            // Write first to an OS-exclusive temporary file. Never publish
            // half-written JSON or replace an earlier technician audit.
            await using (var stream = new FileStream(staging, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 8192, useAsync: true))
            await using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8))
            {
                await writer.WriteAsync(json).ConfigureAwait(false);
            }

            File.Move(staging, filePath); // No-overwrite on the same filesystem.
            return fileName;
        }
        finally
        {
            try
            {
                if (File.Exists(staging))
                    File.Delete(staging);
            }
            catch (IOException) { /* Preserve the original error. */ }
            catch (UnauthorizedAccessException) { /* Preserve the original error. */ }
        }
    }

    /// <inheritdoc/>
    public async Task<List<TempCleanupResultSummary>> ListResultsAsync(int maxResults = 20)
    {
        var summaries = new List<TempCleanupResultSummary>();

        if (!Directory.Exists(_resultsDirectory))
            return summaries;

        var files = Directory.GetFiles(_resultsDirectory, "cleanup-result-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var result = JsonSerializer.Deserialize<TempCleanupResult>(json, SerializerOptions);

                if (result != null)
                {
                    summaries.Add(new TempCleanupResultSummary
                    {
                        Id = result.Id,
                        StartedAt = result.StartedAt,
                        DeletedMB = result.DeletedMB,
                        DeletedFiles = result.DeletedFiles,
                        HasWarnings = result.HasWarnings,
                        FileName = Path.GetFileName(file)
                    });
                }
            }
            catch { }
        }

        return summaries;
    }


    /// <summary>
    /// Only exact, previously scanned default targets may be cleaned. Reject
    /// redirected roots and every reparse-point ancestor. This is a conservative
    /// preflight, not an atomic guarantee against concurrent link replacement.
    /// </summary>
    private static string? GetTargetRejectionReason(TempCleanupTarget target)
    {
        if (target.Id == "RECYCLE_BIN")
            return "La limpieza de la Papelera está deshabilitada por seguridad.";

        if (!OperatingSystem.IsWindows())
            return "Esta limpieza requiere Windows.";

        if (target.Id is not ("USER_TEMP" or "WINDOWS_TEMP" or "THUMBNAILS"))
            return "Ubicación de limpieza no autorizada.";

        // Defend against a forged scan and an environment redirect: these
        // roots must match independent Windows known-folder locations.
        if (target.Id == "USER_TEMP"
            && !CleanupTargets.IsTrustedUserTempPath(target.Path))
            return "La carpeta TEMP no coincide con el perfil local autorizado.";
        if (target.Id == "WINDOWS_TEMP"
            && !CleanupTargets.IsTrustedWindowsTempPath(target.Path))
            return "La carpeta TEMP del sistema no coincide con Windows.";

        if (!target.IsScanned || !target.IsAccessible)
            return "Es necesario escanear y verificar la ubicación antes de limpiar.";

        if (string.IsNullOrWhiteSpace(target.Path) ||
            !Path.IsPathFullyQualified(target.Path))
            return "La ruta de limpieza no es absoluta o no es válida.";

        try
        {
            var canonical = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(target.Path));
            var allowedTarget = CleanupTargets.GetDefaultTargets().Single(
                item => item.Id == target.Id);
            var expected = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(allowedTarget.Path));
            if (!string.Equals(canonical, expected,
                StringComparison.OrdinalIgnoreCase))
                return "La ruta no coincide con la ubicación autorizada.";

            var root = Path.TrimEndingDirectorySeparator(
                Path.GetPathRoot(canonical) ?? string.Empty);
            if (string.Equals(root, canonical, StringComparison.OrdinalIgnoreCase))
                return "No se admite limpiar una raíz de volumen.";

            var directory = new DirectoryInfo(canonical);
            if (!directory.Exists)
                return "La ubicación ya no existe.";

            // Includes the target itself and every existing parent, so a
            // junction or symlink cannot redirect a textual allowed path.
            for (DirectoryInfo? current = directory; current is not null;
                current = current.Parent)
            {
                if (!current.Exists)
                    return "La ruta contiene un directorio no disponible.";

                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    return "La ruta contiene un enlace o punto de redirección.";
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or IOException or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            return "No se pudo validar de forma segura la ubicación.";
        }

        return null;
    }

    // --- Métodos de escaneo ---

    private static void ScanFolder(TempCleanupTarget target)
    {
        try
        {
            if (!Directory.Exists(target.Path))
            {
                target.IsAccessible = false;
                target.IsScanned = true;
                target.ScanNotes = "Ubicación no encontrada";
                return;
            }

            var dirInfo = new DirectoryInfo(target.Path);
            var sw = Stopwatch.StartNew();

            var files = dirInfo.GetFiles("*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System |
                    FileAttributes.ReparsePoint,
                MaxRecursionDepth = 3
            });

            foreach (var file in files)
            {
                try
                {
                    target.EstimatedSizeBytes += file.Length;
                    target.EstimatedFileCount++;
                }
                catch { }

                // Timeout de 3 segundos por ubicación
                if (sw.ElapsedMilliseconds > 3000) break;
            }

            target.IsAccessible = true;
            target.IsScanned = true;

            if (target.IsSystemLocation)
            {
                target.ScanNotes = "Requiere permisos de administrador";
            }
        }
        catch (Exception ex)
        {
            target.IsAccessible = false;
            target.IsScanned = true;
            target.ScanNotes = $"Error: {ex.Message}";
        }
    }

    private static void ScanThumbnailsFolder(TempCleanupTarget target)
    {
        try
        {
            if (!Directory.Exists(target.Path))
            {
                target.IsAccessible = false;
                target.IsScanned = true;
                target.ScanNotes = "Carpeta de miniaturas no encontrada";
                return;
            }

            var thumbFiles = Directory.GetFiles(target.Path, "thumbcache_*.db");
            foreach (var file in thumbFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    target.EstimatedSizeBytes += fileInfo.Length;
                    target.EstimatedFileCount++;
                }
                catch { }
            }

            target.IsAccessible = true;
            target.IsScanned = true;
        }
        catch (Exception ex)
        {
            target.IsAccessible = false;
            target.IsScanned = true;
            target.ScanNotes = $"Error: {ex.Message}";
        }
    }

    // --- Métodos de limpieza ---

    // Internal only for isolated synthetic fixture regression tests.
    internal async Task CleanFolderAsync(
        TempCleanupTarget target, TargetResult targetResult, TempCleanupResult result)
    {
        if (!Directory.Exists(target.Path))
        {
            result.Errors.Add(new CleanupError
            {
                FilePath = target.Id,
                Message = "La ubicación de temporales dejó de estar disponible.",
                ErrorType = "MissingTarget"
            });
            targetResult.IsSuccess = false;
            return;
        }

        await Task.Run(() =>
        {
            var dirInfo = new DirectoryInfo(target.Path);
            var sw = Stopwatch.StartNew();

            // Obtener todos los archivos
            FileInfo[] files;
            try
            {
                files = dirInfo.GetFiles("*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System |
                        FileAttributes.ReparsePoint,
                    MaxRecursionDepth = 3
                });
            }
            catch (Exception)
            {
                result.Errors.Add(new CleanupError
                {
                    FilePath = target.Id,
                    Message = "No se pudo enumerar la ubicación de temporales.",
                    ErrorType = "EnumerationError"
                });
                targetResult.IsSuccess = false;
                return;
            }

            foreach (var file in files)
            {
                // Timeout de 30 segundos por ubicación
                if (sw.ElapsedMilliseconds > 30000)
                {
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = target.Id,
                        Message = "Se alcanzó el límite de tiempo; la limpieza quedó incompleta.",
                        ErrorType = "IncompleteCleanup"
                    });
                    targetResult.IsSuccess = false;
                    break;
                }

                try
                {
                    // Verificar si es archivo protegido
                    if (IsProtectedFile(file))
                    {
                        targetResult.SkippedFiles++;
                        result.SkippedFiles++;
                        continue;
                    }

                    // The native handle is opened and verified before marking
                    // that SAME file for deletion. Never delete by pathname
                    // after the safety checks (TOCTOU vulnerability).
                    if (!WindowsScopedFileDelete.TryDelete(
                        target.Path, file.FullName, out var size))
                    {
                        targetResult.SkippedFiles++;
                        result.SkippedFiles++;
                        continue;
                    }

                    targetResult.DeletedFiles++;
                    targetResult.DeletedBytes += size;
                    result.DeletedFiles++;
                    result.DeletedBytes += size;
                }
                catch (IOException)
                {
                    // Archivo bloqueado
                    targetResult.SkippedFiles++;
                    result.SkippedFiles++;
                }
                catch (UnauthorizedAccessException)
                {
                    targetResult.FailedFiles++;
                    result.FailedFiles++;
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = target.Id,
                        Message = "Acceso denegado a un archivo temporal.",
                        ErrorType = "AccessDenied"
                    });
                }
                catch (Exception)
                {
                    targetResult.FailedFiles++;
                    result.FailedFiles++;
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = target.Id,
                        Message = "No se pudo limpiar un archivo temporal.",
                        ErrorType = "Error"
                    });
                }
            }
        });
    }

    internal static void CleanThumbnailsFolder(
        TempCleanupTarget target, TargetResult targetResult, TempCleanupResult result)
    {
        if (!Directory.Exists(target.Path))
        {
            result.Errors.Add(new CleanupError
            {
                FilePath = target.Id,
                Message = "La carpeta de miniaturas dejó de estar disponible.",
                ErrorType = "MissingTarget"
            });
            targetResult.IsSuccess = false;
            return;
        }

        try
        {
            var thumbFiles = Directory.GetFiles(target.Path, "thumbcache_*.db");

            foreach (var file in thumbFiles)
            {
                try
                {
                    // Cache files use the same handle-scoped safety policy:
                    // no following junctions, no deleting changed/recent files.
                    if (!WindowsScopedFileDelete.TryDelete(
                        target.Path, file, out var size))
                    {
                        targetResult.SkippedFiles++;
                        result.SkippedFiles++;
                        continue;
                    }

                    targetResult.DeletedFiles++;
                    targetResult.DeletedBytes += size;
                    result.DeletedFiles++;
                    result.DeletedBytes += size;
                }
                catch (IOException)
                {
                    targetResult.SkippedFiles++;
                    result.SkippedFiles++;
                }
                catch (Exception)
                {
                    targetResult.FailedFiles++;
                    result.FailedFiles++;
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = target.Id,
                        Message = "No se pudo limpiar un archivo de miniaturas.",
                        ErrorType = "Error"
                    });
                }
            }
        }
        catch (Exception)
        {
            result.Errors.Add(new CleanupError
            {
                FilePath = target.Id,
                Message = "No se pudo enumerar o limpiar la caché de miniaturas.",
                ErrorType = "CleanError"
            });
            targetResult.IsSuccess = false;
        }
    }

    // --- Helpers ---

    private static bool IsProtectedFile(FileInfo file)
    {
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            return true;

        var name = file.Name.ToLowerInvariant();

        // No borrar archivos de lock, PID, socket
        if (ProtectedPatterns.Any(p => name.Contains(p.Replace("*", ""))))
            return true;

        // No borrar archivos muy recientes (últimos 60 segundos)
        if (file.LastWriteTime > DateTime.Now.AddSeconds(-60))
            return true;

        return false;
    }

    private static async Task<string> GetTechnicianNameAsync()
    {
        return await Helpers.SettingsHelper.GetTechnicianNameAsync(new Data.JsonSettingsService());
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
