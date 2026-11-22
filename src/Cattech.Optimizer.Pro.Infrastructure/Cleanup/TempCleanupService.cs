using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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
    public Task<List<TempCleanupTarget>> ScanAsync(IProgress<int>? progress = null) =>
        ScanTargetsAsync(CleanupTargets.GetDefaultTargets(), progress);

    /// <summary>
    /// Offload potentially expensive bounded enumeration to a worker thread.
    /// WPF owns the fresh target objects only after the awaited task completes.
    /// Progress<T> continues to marshal reports to the captured UI context.
    /// </summary>
    internal static Task<List<TempCleanupTarget>> ScanTargetsAsync(
        List<TempCleanupTarget> targets, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return Task.Run(() => ScanTargets(targets, progress));
    }

    private static List<TempCleanupTarget> ScanTargets(
        List<TempCleanupTarget> targets, IProgress<int>? progress)
    {
        progress?.Report(10);

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];

            // Verificar accesibilidad
            if (target.Id == "RECYCLE_BIN")
            {
                ScanRecycleBin(target);
            }
            else if (GetTargetRejectionReason(target,
                requireCompletedScan: false) is { } rejection)
            {
                // The read-side scan must enforce the same known-root and
                // reparse-point policy as the write-side cleanup preflight.
                // Do not enumerate a forged thumbnail or unknown target.
                target.IsAccessible = false;
                target.IsSelected = false;
                target.IsScanned = true;
                target.ScanNotes = $"No disponible: {rejection}";
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
        return targets;
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
                result.Errors.Add(new CleanupError
                {
                    // Do not expose an untrusted absolute path in diagnostics.
                    FilePath = rejected.Target.Id,
                    Message = rejected.Reason!,
                    ErrorType = "UnsafeTarget"
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
                    EmptyRecycleBin(targetResult, result);
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
        if (result.Errors is null || result.TargetResults is null
            || result.Errors.Any(error => error is null)
            || result.TargetResults.Any(target => target is null))
            throw new ArgumentException(
                "El registro técnico tiene listas de auditoría inválidas.",
                nameof(result));

        // Validate the exact JSON snapshot before creating directories or
        // publishing any audit file. An invalid new history record must not
        // poison the technician's record inventory.
        var json = JsonSerializer.Serialize(result, SerializerOptions);
        if (DeserializeVerifiedHistory(json) is null)
            throw new ArgumentException(
                "El registro técnico contiene datos estructurales inválidos.",
                nameof(result));

        EnsureDirectoryExists(_resultsDirectory);

        // A second-resolution timestamp, even combined with a model-supplied
        // ID, is not a unique or safe audit-file identity across instances.
        var token = Guid.NewGuid().ToString("N");
        var fileName = $"cleanup-result-{result.StartedAt:yyyyMMdd-HHmmss}-{token}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);
        var staging = Path.Combine(_resultsDirectory, $".cattech-cleanup-{token}.tmp");

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

        if (maxResults <= 0 || !Directory.Exists(_resultsDirectory))
            return summaries;

        // Invalid audit files must not consume the pagination window and
        // conceal earlier valid records. The legacy list is read-only.
        var files = Directory.GetFiles(_resultsDirectory, "cleanup-result-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f));

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var result = DeserializeVerifiedHistory(json);
                if (result is null)
                    continue;

                summaries.Add(new TempCleanupResultSummary
                {
                    Id = result.Id,
                    StartedAt = result.StartedAt,
                    DeletedMB = result.DeletedMB,
                    DeletedFiles = result.DeletedFiles,
                    HasWarnings = result.HasWarnings,
                    FileName = Path.GetFileName(file)
                });
                if (summaries.Count >= maxResults)
                    break;
            }
            catch (IOException) { /* Invalid record is not advertised. */ }
            catch (UnauthorizedAccessException) { /* Retain original file. */ }
            catch (JsonException) { /* Retain malformed history for review. */ }
        }
        return summaries;
    }


    /// <summary>
    /// Do not invent audit IDs or timestamps from default .NET initializers.
    /// A structurally invalid history file is never presented as an actual
    /// cleanup result. No files are repaired or deleted by this reader.
    /// </summary>
    private static TempCleanupResult? DeserializeVerifiedHistory(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!TryUniqueHistoryProperty(root, "id", JsonValueKind.String, out var id)
            || string.IsNullOrWhiteSpace(id.GetString())
            || !TryUniqueHistoryProperty(root, "startedAt", JsonValueKind.String, out _)
            || !TryUniqueHistoryProperty(root, "finishedAt", JsonValueKind.String, out _))
            return null;

        var result = JsonSerializer.Deserialize<TempCleanupResult>(json, SerializerOptions);
        if (result is null || !string.Equals(result.Id, id.GetString(), StringComparison.Ordinal)
            || result.StartedAt == default || result.FinishedAt == default
            || result.FinishedAt < result.StartedAt || result.Errors is null
            || result.TargetResults is null
            || result.Errors.Any(error => error is null)
            || result.TargetResults.Any(target => target is null)
            || result.DeletedBytes < 0
            || result.TotalSelectedBytes < 0 || result.DeletedFiles < 0
            || result.SkippedFiles < 0 || result.FailedFiles < 0)
            return null;
        return result;
    }

    private static bool TryUniqueHistoryProperty(
        JsonElement root, string key, JsonValueKind kind, out JsonElement value)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        var seen = false;
        foreach (var entry in root.EnumerateObject())
        {
            if (!string.Equals(entry.Name, key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (seen || !entry.NameEquals(key) || entry.Value.ValueKind != kind)
                return false;
            value = entry.Value;
            seen = true;
        }
        return seen;
    }

    /// <summary>
    /// Only exact, previously scanned default targets may be cleaned. Reject
    /// redirected roots and every reparse-point ancestor. This is a conservative
    /// preflight, not an atomic guarantee against concurrent link replacement.
    /// </summary>
    private static string? GetTargetRejectionReason(
        TempCleanupTarget target, bool requireCompletedScan = true)
    {
        if (target.Id == "RECYCLE_BIN")
        {
            // The recycle bin is a shell namespace, not a filesystem path:
            // it is emptied through the Shell API, never through file paths.
            if (!OperatingSystem.IsWindows())
                return "La Papelera requiere Windows.";
            if (requireCompletedScan && (!target.IsScanned || !target.IsAccessible))
                return "Es necesario escanear y verificar la Papelera antes de vaciarla.";
            return null;
        }

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

        if (requireCompletedScan && (!target.IsScanned || !target.IsAccessible))
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

    // Avoid materializing an unbounded directory listing before applying
    // time limits. A failed preflight never returns partial candidates.
    private const int MaxEnumeratedFilesPerTarget = 200_000;

    internal static bool TryEnumerateBounded(
        string directory, string pattern, bool recursive,
        int maxFiles, TimeSpan maxDuration, out List<string> files)
    {
        files = new List<string>();
        if (maxFiles <= 0 || maxDuration <= TimeSpan.Zero)
            return false;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            // Unreadable entries are skipped instead of aborting the whole
            // target: a single protected file used to make temp cleanup
            // unusable on real machines. Each skipped entry simply is not
            // counted and never reaches the deletion gate.
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
                | FileAttributes.ReparsePoint,
            MaxRecursionDepth = recursive ? 16 : 0
        };
        var watch = Stopwatch.StartNew();
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, pattern, options))
            {
                // Bound memory and reject rather than operate on a partial list.
                if (watch.Elapsed >= maxDuration || files.Count >= maxFiles)
                {
                    files.Clear();
                    return false;
                }
                files.Add(path);
            }
            if (watch.Elapsed >= maxDuration)
            {
                files.Clear();
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException)
        {
            files.Clear();
            return false;
        }
    }

    /// <summary>
    /// A scan is complete only when every candidate was inspected. Even one
    /// unreadable file invalidates the aggregate; never publish a partial
    /// count or let a failed scan enable the cleanup checkbox.
    /// The evaluator is injectable only to test I/O failures without touching
    /// real protected filesystem objects.
    /// </summary>
    internal static bool TrySummarizeCandidates(
        IEnumerable<string> files, Func<string, (bool Protected, long Length)> evaluate,
        out long bytes, out int count)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(evaluate);
        bytes = 0;
        count = 0;
        try
        {
            foreach (var file in files)
            {
                var (isProtected, length) = evaluate(file);
                if (isProtected)
                    continue;
                if (length < 0)
                {
                    bytes = 0;
                    count = 0;
                    return false;
                }
                bytes = checked(bytes + length);
                count = checked(count + 1);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException or System.Security.SecurityException
            or ArgumentException or NotSupportedException or OverflowException)
        {
            bytes = 0;
            count = 0;
            return false;
        }
    }

    // --- Métodos de escaneo ---

    /// <summary>
    /// Reads the recycle bin inventory through the Shell API. Never deletes.
    /// The recycle bin is a shell namespace, so it is measured and emptied
    /// with Shell calls instead of filesystem paths.
    /// </summary>
    internal static void ScanRecycleBin(TempCleanupTarget target)
    {
        target.EstimatedSizeBytes = 0;
        target.EstimatedFileCount = 0;
        target.IsAccessible = false;
        target.IsSelected = false;
        target.IsScanned = true;

        if (!OperatingSystem.IsWindows())
        {
            target.ScanNotes = "No disponible: requiere Windows.";
            return;
        }

        var info = new RecycleBinInfo
        {
            Size = Marshal.SizeOf<RecycleBinInfo>()
        };
        try
        {
            if (SHQueryRecycleBin(null, ref info) != 0)
            {
                target.ScanNotes = "No disponible: no se pudo consultar la Papelera.";
                return;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException
            or EntryPointNotFoundException or MarshalDirectiveException)
        {
            target.ScanNotes = "No disponible: la API de la Papelera no está disponible.";
            return;
        }

        if (info.ItemCount < 0 || info.Bytes < 0)
        {
            target.ScanNotes = "No disponible: inventario de Papelera inválido.";
            return;
        }

        target.EstimatedSizeBytes = info.Bytes;
        target.EstimatedFileCount = (int)Math.Min(info.ItemCount, int.MaxValue);
        target.IsAccessible = true;
        target.ScanNotes = info.ItemCount == 0
            ? "Papelera vacía"
            : "Vaciado definitivo: no se puede deshacer.";
    }

    /// <summary>
    /// Empties the recycle bin with a silent Shell operation. The inventory is
    /// read first so the result can account for what was removed. Failures are
    /// reported as cleanup errors, never as success.
    /// </summary>
    internal static void EmptyRecycleBin(
        TargetResult targetResult, TempCleanupResult result)
    {
        if (!OperatingSystem.IsWindows())
        {
            result.Errors.Add(new CleanupError
            {
                FilePath = "RECYCLE_BIN",
                Message = "La Papelera requiere Windows.",
                ErrorType = "UnsupportedOperation"
            });
            targetResult.IsSuccess = false;
            return;
        }

        try
        {
            var before = new RecycleBinInfo { Size = Marshal.SizeOf<RecycleBinInfo>() };
            var measured = SHQueryRecycleBin(null, ref before) == 0;

            var hr = SHEmptyRecycleBin(IntPtr.Zero, null,
                ShellEmptyRecycleBinNoConfirmation | ShellEmptyRecycleBinNoProgressUi
                | ShellEmptyRecycleBinNoSound);
            if (hr != 0)
            {
                result.Errors.Add(new CleanupError
                {
                    FilePath = "RECYCLE_BIN",
                    Message = $"No se pudo vaciar la Papelera (código 0x{hr:X8}).",
                    ErrorType = "CleanError"
                });
                targetResult.IsSuccess = false;
                return;
            }

            if (measured && before.ItemCount > 0)
            {
                targetResult.DeletedFiles = (int)Math.Min(before.ItemCount, int.MaxValue);
                targetResult.DeletedBytes = Math.Max(0, before.Bytes);
                result.DeletedFiles += targetResult.DeletedFiles;
                result.DeletedBytes += targetResult.DeletedBytes;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException
            or EntryPointNotFoundException or MarshalDirectiveException)
        {
            result.Errors.Add(new CleanupError
            {
                FilePath = "RECYCLE_BIN",
                Message = "La API de la Papelera no está disponible.",
                ErrorType = "UnsupportedOperation"
            });
            targetResult.IsSuccess = false;
        }
    }

    private const uint ShellEmptyRecycleBinNoConfirmation = 0x00000001;
    private const uint ShellEmptyRecycleBinNoProgressUi = 0x00000002;
    private const uint ShellEmptyRecycleBinNoSound = 0x00000004;

    // SHQUERYRBINFO: DWORD cbSize; __int64 i64Size; __int64 i64NumItems;
    [StructLayout(LayoutKind.Sequential)]
    private struct RecycleBinInfo
    {
        public int Size;
        public long Bytes;
        public long ItemCount;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref RecycleBinInfo info);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    // Internal for read-only, isolated synthetic scan regressions.
    internal static void ScanFolder(TempCleanupTarget target)
    {
        var wasSelected = target.IsSelected;
        target.EstimatedSizeBytes = 0;
        target.EstimatedFileCount = 0;
        target.IsAccessible = false;
        target.IsSelected = false; // Fail closed until the scan succeeds.
        target.IsScanned = true;
        if (!Directory.Exists(target.Path))
        {
            target.ScanNotes = "Ubicación no encontrada";
            return;
        }

        // A partially readable inventory must not look like a successful
        // complete scan. No deletion occurs while scanning.
        // The scan walks subfolders, matching exactly what cleanup removes.
        if (!TryEnumerateBounded(target.Path, "*", recursive: true,
            MaxEnumeratedFilesPerTarget, TimeSpan.FromSeconds(20), out var files))
        {
            target.ScanNotes = "No disponible: demasiados archivos, tiempo agotado o acceso denegado.";
            return;
        }

        // A partially readable inventory must not look like a successful
        // complete scan. No deletion occurs while scanning.
        if (!TrySummarizeCandidates(files, file =>
            {
                var info = new FileInfo(file);
                return (IsProtectedFile(info), info.Length);
            }, out var bytes, out var count))
        {
            target.ScanNotes = "No disponible: un archivo no se pudo verificar.";
            return;
        }

        target.EstimatedSizeBytes = bytes;
        target.EstimatedFileCount = count;
        target.IsAccessible = true;
        target.IsSelected = wasSelected; // Preserve trusted default selection only.
        target.ScanNotes = target.IsSystemLocation
            ? "Requiere permisos de administrador" : string.Empty;
    }

    // Exposed internally for isolated scan-only fixture tests.
    internal static void ScanThumbnailsFolder(TempCleanupTarget target)
    {
        var wasSelected = target.IsSelected;
        target.EstimatedSizeBytes = 0;
        target.EstimatedFileCount = 0;
        target.IsAccessible = false;
        target.IsSelected = false; // Fail closed until the scan succeeds.
        target.IsScanned = true;
        if (!Directory.Exists(target.Path))
        {
            target.ScanNotes = "Carpeta de miniaturas no encontrada";
            return;
        }

        if (!TryEnumerateBounded(target.Path, "thumbcache_*.db",
            recursive: false, MaxEnumeratedFilesPerTarget,
            TimeSpan.FromSeconds(3), out var files))
        {
            target.ScanNotes = "No disponible: no se pudo completar el escaneo.";
            return;
        }

        // A partially readable inventory must not look like a successful
        // complete scan. No deletion occurs while scanning.
        if (!TrySummarizeCandidates(files, file =>
            {
                var info = new FileInfo(file);
                return (IsProtectedFile(info), info.Length);
            }, out var bytes, out var count))
        {
            target.ScanNotes = "No disponible: un archivo no se pudo verificar.";
            return;
        }

        target.EstimatedSizeBytes = bytes;
        target.EstimatedFileCount = count;
        target.IsAccessible = true;
        target.IsSelected = wasSelected;
        target.ScanNotes = "Solo archivos thumbcache_*.db";
    }

    // --- Métodos de limpieza ---

    // Internal only for isolated synthetic fixture regression tests.
    internal async Task CleanFolderAsync(
        TempCleanupTarget target, TargetResult targetResult, TempCleanupResult result,
        int maxCandidates = MaxEnumeratedFilesPerTarget)
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
            var sw = Stopwatch.StartNew();

            // Finish a bounded, non-destructive listing BEFORE the first delete.
            // Subfolders are included because most temporary garbage lives one
            // or more levels below %TEMP%; reparse points are never followed.
            if (!TryEnumerateBounded(target.Path, "*", recursive: true,
                maxCandidates, TimeSpan.FromSeconds(20), out var files))
            {
                result.Errors.Add(new CleanupError
                {
                    FilePath = target.Id,
                    Message = "Enumeración incompleta o límite de archivos excedido.",
                    ErrorType = "EnumerationError"
                });
                targetResult.IsSuccess = false;
                return;
            }

            foreach (var filePath in files)
            {
                var file = new FileInfo(filePath);
                // Límite de tiempo por ubicación
                if (sw.ElapsedMilliseconds > 60000)
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

            // Leave the target tree as empty of leftovers as possible without
            // touching anything that still has content or a reparse point.
            RemoveEmptySubdirectories(target.Path);
        });
    }

    /// <summary>
    /// Removes empty subdirectories (never the target root) deepest-first.
    /// Only truly empty, ordinary directories are removed; anything that
    /// still has content, a junction or an access problem is left untouched.
    /// </summary>
    internal static void RemoveEmptySubdirectories(string root)
    {
        if (!Directory.Exists(root))
            return;

        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
                    | FileAttributes.ReparsePoint,
                MaxRecursionDepth = 16
            };

            var directories = Directory.EnumerateDirectories(root, "*", options)
                .OrderByDescending(path => path.Length)
                .ToArray();

            foreach (var directory in directories)
            {
                try
                {
                    var info = new DirectoryInfo(directory);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    if (info.EnumerateFileSystemInfos().Any())
                        continue;
                    info.Delete(recursive: false);
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    // A directory that cannot be removed is not a cleanup error:
                    // its files may still have been deleted successfully.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException
            or System.Security.SecurityException)
        {
            // Best effort: empty-directory pruning never fails the cleanup.
        }
    }

    internal static void CleanThumbnailsFolder(
        TempCleanupTarget target, TargetResult targetResult, TempCleanupResult result,
        int maxCandidates = MaxEnumeratedFilesPerTarget)
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
            // Never start deleting from a partial/unbounded cache listing.
            if (!TryEnumerateBounded(target.Path, "thumbcache_*.db",
                recursive: false, maxCandidates, TimeSpan.FromSeconds(5),
                out var thumbFiles))
            {
                result.Errors.Add(new CleanupError
                {
                    FilePath = target.Id,
                    Message = "No se pudo completar la enumeración de miniaturas.",
                    ErrorType = "EnumerationError"
                });
                targetResult.IsSuccess = false;
                return;
            }

            foreach (var file in thumbFiles)
            {
                try
                {
                    // Preserve the same filename, reparse-point and retention
                    // exclusions applied by ScanThumbnailsFolder. The native
                    // handle check is an additional, never a replacement, gate.
                    if (IsProtectedFile(new FileInfo(file)))
                    {
                        targetResult.SkippedFiles++;
                        result.SkippedFiles++;
                        continue;
                    }

                    // Keep the verified-handle deletion gate; never delete
                    // by pathname after this managed preflight.
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
        // The native handle-level delete rejects ReadOnly, Hidden and System
        // files too. Exclude them from the reclaim estimate, not only at
        // deletion time; never promise bytes the safe gate will refuse.
        const FileAttributes nativeExcluded =
            FileAttributes.ReparsePoint | FileAttributes.ReadOnly
            | FileAttributes.Hidden | FileAttributes.System;
        if ((file.Attributes & nativeExcluded) != 0)
            return true;

        // Use one shared filename policy in managed scan, cleanup and native
        // disposition to avoid bypass through a future direct helper call.
        if (CleanupTargets.IsProtectedTemporaryFileName(file.Name))
            return true;

        // No age retention: temporary files are meant to be disposable and
        // an unlocked file is safe to remove regardless of its timestamp.
        // Files an installer still needs are held open and are refused by the
        // verified-handle deletion gate.
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
