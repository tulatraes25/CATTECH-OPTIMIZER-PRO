using System.Security;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

internal interface ICrashDumpFileEnumerator
{
    IEnumerable<CrashDumpEntry> Enumerate(string directory);
}

/// <summary>
/// Enumerador real. Lee solamente metadatos; nunca abre un .dmp como stream.
/// No sigue subdirectorios ni enlaces/reparse points de archivos.
/// </summary>
internal sealed class FileSystemCrashDumpFileEnumerator : ICrashDumpFileEnumerator
{
    public IEnumerable<CrashDumpEntry> Enumerate(string directory)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.dmp", SearchOption.TopDirectoryOnly))
        {
            var file = new FileInfo(path);
            long size;
            DateTime utc;

            try
            {
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                size = file.Length;
                utc = file.LastWriteTimeUtc;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            {
                // Un archivo puede desaparecer entre enumeración y lectura de metadatos.
                continue;
            }

            yield return new CrashDumpEntry
            {
                FileName = file.Name,
                SizeBytes = size,
                LastModifiedUtc = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc))
            };
        }
    }
}

/// <summary>
/// Descubre minidumps sin ejecutarlos ni leerlos. Mantiene los N más recientes
/// con memoria acotada y sin registrar rutas absolutas en el resultado.
/// </summary>
public sealed class CrashDumpDiscoveryService : ICrashDumpDiscoveryService
{
    private const int DefaultMaxDumps = 200;
    private readonly ICrashDumpFileEnumerator _enumerator;
    private readonly string? _directoryOverride;
    private readonly int _maxDumps;

    public CrashDumpDiscoveryService()
        : this(new FileSystemCrashDumpFileEnumerator(), null, DefaultMaxDumps)
    {
    }

    internal CrashDumpDiscoveryService(
        ICrashDumpFileEnumerator enumerator,
        string? directoryOverride,
        int maxDumps = DefaultMaxDumps)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _directoryOverride = directoryOverride;
        if (maxDumps < 1 || maxDumps > 1000)
            throw new ArgumentOutOfRangeException(nameof(maxDumps));

        _maxDumps = maxDumps;
    }

    public CrashDumpDiscoveryResult Discover()
    {
        if (_directoryOverride is null && !OperatingSystem.IsWindows())
        {
            return new CrashDumpDiscoveryResult
            {
                Status = CrashDumpDiscoveryStatus.UnsupportedPlatform,
                Message = "El descubrimiento de minidumps requiere Windows."
            };
        }

        var directory = _directoryOverride ?? ResolveWindowsMinidumpDirectory();
        if (string.IsNullOrWhiteSpace(directory))
        {
            return new CrashDumpDiscoveryResult
            {
                Status = CrashDumpDiscoveryStatus.ReadError,
                Message = "No se pudo determinar la carpeta de minidumps."
            };
        }

        // PriorityQueue almacena solo los N dumps más recientes, no la lista completa.
        var newest = new PriorityQueue<CrashDumpEntry, long>();
        var observed = 0;

        try
        {
            foreach (var entry in _enumerator.Enumerate(directory))
            {
                // El enumerador real ya filtra *.dmp. Volvemos a validar el dato
                // por seguridad al recibir implementaciones alternativas.
                if (string.IsNullOrWhiteSpace(entry.FileName)
                    || !entry.FileName.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = Path.GetFileName(entry.FileName);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                observed++;
                var safe = new CrashDumpEntry
                {
                    FileName = fileName,
                    SizeBytes = Math.Max(0, entry.SizeBytes),
                    LastModifiedUtc = entry.LastModifiedUtc.ToUniversalTime()
                };
                var priority = safe.LastModifiedUtc.UtcDateTime.Ticks;

                if (newest.Count < _maxDumps)
                {
                    newest.Enqueue(safe, priority);
                }
                else if (newest.TryPeek(out _, out var lowest) && priority > lowest)
                {
                    newest.Dequeue();
                    newest.Enqueue(safe, priority);
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            return Failure(CrashDumpDiscoveryStatus.DirectoryMissing, "La carpeta Minidump no existe.");
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(CrashDumpDiscoveryStatus.AccessDenied, "Sin permiso para enumerar minidumps.");
        }
        catch (SecurityException)
        {
            return Failure(CrashDumpDiscoveryStatus.AccessDenied, "Acceso restringido a minidumps.");
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return Failure(CrashDumpDiscoveryStatus.ReadError, "No se pudo completar la lectura de metadatos.");
        }

        var list = newest.UnorderedItems
            .Select(x => x.Element)
            .OrderByDescending(x => x.LastModifiedUtc)
            .ThenBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CrashDumpDiscoveryResult
        {
            Status = observed > 0 ? CrashDumpDiscoveryStatus.Found : CrashDumpDiscoveryStatus.NoDumps,
            Dumps = list,
            FilesObserved = observed,
            IsTruncated = observed > list.Length,
            Message = observed == 0
                ? "No se encontraron minidumps. Esto no demuestra que nunca haya habido fallas."
                : "Minidumps encontrados; todavía no se analizó su contenido."
        };
    }

    private static CrashDumpDiscoveryResult Failure(CrashDumpDiscoveryStatus status, string message)
        => new() { Status = status, Message = message };

    private static string? ResolveWindowsMinidumpDirectory()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return string.IsNullOrWhiteSpace(windows) ? null : Path.Combine(windows, "Minidump");
    }
}
