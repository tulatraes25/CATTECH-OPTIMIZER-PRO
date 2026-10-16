namespace Cattech.Optimizer.Pro.Core.Models.Diagnostics;

/// <summary>Estado observable de búsqueda de minidumps. Nunca infiere salud del sistema.</summary>
public enum CrashDumpDiscoveryStatus
{
    Found,
    NoDumps,
    DirectoryMissing,
    AccessDenied,
    ReadError,
    UnsupportedPlatform
}

/// <summary>Metadatos mínimos de un dump; no contiene ruta absoluta ni contenido del dump.</summary>
public sealed class CrashDumpEntry
{
    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset LastModifiedUtc { get; init; }
}

/// <summary>Resultado de enumeración read-only, acotado en memoria.</summary>
public sealed class CrashDumpDiscoveryResult
{
    public CrashDumpDiscoveryStatus Status { get; init; }
    public IReadOnlyList<CrashDumpEntry> Dumps { get; init; } = Array.Empty<CrashDumpEntry>();
    public int FilesObserved { get; init; }
    public bool IsTruncated { get; init; }
    public string Message { get; init; } = string.Empty;
}
