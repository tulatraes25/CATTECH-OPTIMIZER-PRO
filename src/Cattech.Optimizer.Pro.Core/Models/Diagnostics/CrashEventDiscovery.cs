namespace Cattech.Optimizer.Pro.Core.Models.Diagnostics;

/// <summary>Estado de consulta del registro System, no del equipo.</summary>
public enum CrashEventDiscoveryStatus
{
    Found,
    NoEvents,
    AccessDenied,
    ReadError,
    UnsupportedPlatform
}

/// <summary>
/// Evidencia de Event Log reducida y despersonalizada.
/// No contiene XML original, mensajes localizados ni rutas de dumps.
/// </summary>
public sealed class CrashEventEvidence
{
    public string Source { get; init; } = string.Empty;
    public int EventId { get; init; }
    public DateTimeOffset? TimeCreatedUtc { get; init; }
    public uint BugCheckCode { get; init; }
    public string HexCode => $"0x{BugCheckCode:X8}";
}

/// <summary>
/// Eventos candidatos relacionados con bugchecks. Dos eventos pueden corresponder
/// al mismo crash; no deben contarse automáticamente como incidentes distintos.
/// </summary>
public sealed class CrashEventDiscoveryResult
{
    public CrashEventDiscoveryStatus Status { get; init; }
    public IReadOnlyList<CrashEventEvidence> Events { get; init; } = Array.Empty<CrashEventEvidence>();
    public int EventsScanned { get; init; }
    public bool IsTruncated { get; init; }
    public string Message { get; init; } = string.Empty;
}
