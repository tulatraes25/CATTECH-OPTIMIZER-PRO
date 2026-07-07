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
                target.IsAccessible = true;
                target.IsScanned = true;
                target.ScanNotes = "Opcional: solo limpiar si está seguro";
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

    /// <inheritdoc/>
    public async Task<TempCleanupResult> CleanupAsync(
        IEnumerable<TempCleanupTarget> targets, IProgress<int>? progress = null)
    {
        var result = new TempCleanupResult
        {
            StartedAt = DateTime.Now
        };

        var selectedTargets = targets.Where(t => t.IsSelected).ToList();
        int processedCount = 0;

        foreach (var target in selectedTargets)
        {
            var targetResult = new TargetResult
            {
                TargetId = target.Id,
                TargetName = target.DisplayName,
                Path = target.Path
            };

            try
            {
                if (target.Id == "RECYCLE_BIN")
                {
                    await CleanRecycleBinAsync(targetResult, result);
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
            catch (Exception ex)
            {
                targetResult.IsSuccess = false;
                result.Errors.Add(new CleanupError
                {
                    FilePath = target.Path,
                    Message = ex.Message,
                    ErrorType = "CleanError"
                });
            }

            targetResult.IsSuccess = targetResult.FailedFiles == 0;
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
        EnsureDirectoryExists(_resultsDirectory);

        var fileName = $"cleanup-result-{result.StartedAt:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"cleanup-result-{result.StartedAt:yyyyMMdd-HHmmss}-{result.Id}.json";
            filePath = Path.Combine(_resultsDirectory, fileName);
        }

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
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

    private async Task CleanFolderAsync(
        TempCleanupTarget target, TargetResult targetResult, TempCleanupResult result)
    {
        if (!Directory.Exists(target.Path))
        {
            targetResult.IsSuccess = true;
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
                    MaxRecursionDepth = 3
                });
            }
            catch
            {
                return;
            }

            foreach (var file in files)
            {
                // Timeout de 30 segundos por ubicación
                if (sw.ElapsedMilliseconds > 30000) break;

                try
                {
                    // Verificar si es archivo protegido
                    if (IsProtectedFile(file))
                    {
                        targetResult.SkippedFiles++;
                        result.SkippedFiles++;
                        continue;
                    }

                    // Intentar borrar
                    file.Delete();
                    targetResult.DeletedFiles++;
                    targetResult.DeletedBytes += file.Length;
                    result.DeletedFiles++;
                    result.DeletedBytes += file.Length;
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
                        FilePath = file.FullName,
                        Message = "Acceso denegado",
                        ErrorType = "AccessDenied"
                    });
                }
                catch (Exception ex)
                {
                    targetResult.FailedFiles++;
                    result.FailedFiles++;
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = file.FullName,
                        Message = ex.Message,
                        ErrorType = "Error"
                    });
                }
            }
        });
    }

    private static void CleanThumbnailsFolder(
        TempCleanupTarget target, TargetResult targetResult, TempCleanupResult result)
    {
        if (!Directory.Exists(target.Path)) return;

        try
        {
            var thumbFiles = Directory.GetFiles(target.Path, "thumbcache_*.db");

            foreach (var file in thumbFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var size = fileInfo.Length;

                    fileInfo.Delete();

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
                catch (Exception ex)
                {
                    targetResult.FailedFiles++;
                    result.FailedFiles++;
                    result.Errors.Add(new CleanupError
                    {
                        FilePath = file,
                        Message = ex.Message,
                        ErrorType = "Error"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(new CleanupError
            {
                FilePath = target.Path,
                Message = ex.Message,
                ErrorType = "CleanError"
            });
        }
    }

    private static async Task CleanRecycleBinAsync(TargetResult targetResult, TempCleanupResult result)
    {
        try
        {
            // Usar Shell32 para vaciar papelera de reciclaje
            await Task.Run(() =>
            {
                var shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType != null)
                {
                    var shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic shellApp = shell;
                        shellApp.NameSpace(0xA); // 0x0A = Recycle Bin
                        // No hay forma directa de vaciar con Shell32, usar comando
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                    }
                }
            });

            // Alternativa: usar comando shell
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c echo Y | rd /s C:\\$Recycle.Bin",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                targetResult.IsSuccess = true;
                targetResult.DeletedFiles = 1; // Estimado
            }
        }
        catch (Exception ex)
        {
            targetResult.FailedFiles++;
            result.Errors.Add(new CleanupError
            {
                FilePath = "RecycleBin",
                Message = ex.Message,
                ErrorType = "CleanError"
            });
        }
    }

    // --- Helpers ---

    private static bool IsProtectedFile(FileInfo file)
    {
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
        try
        {
            var settingsService = new Data.JsonSettingsService();
            var settings = await settingsService.LoadSettingsAsync();
            return settings.Company.TechnicianName;
        }
        catch { return string.Empty; }
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
