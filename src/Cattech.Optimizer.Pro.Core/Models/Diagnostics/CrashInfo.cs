namespace Cattech.Optimizer.Pro.Core.Models.Diagnostics;

/// <summary>
/// Nivel de confianza de una inferencia de crash. No implica causalidad probada.
/// </summary>
public enum CrashAnalysisConfidence
{
    Unknown,
    Low,
    Medium,
    High
}

/// <summary>
/// Resultado normalizado de la decodificación de un Bug Check de Windows.
/// </summary>
public sealed class BugCheckDecodeResult
{
    public uint Code { get; init; }
    public string HexCode => $"0x{Code:X8}";
    public string Name { get; init; } = "UNKNOWN_BUG_CHECK";
    public string Description { get; init; } = "Código de detención no reconocido por el catálogo local.";
    public IReadOnlyList<string> TypicalAreas { get; init; } = Array.Empty<string>();
    public bool IsKnown { get; init; }
}

/// <summary>
/// Información de un crash de Windows obtenida de una fuente read-only.
/// En esta fase no se atribuye automáticamente responsabilidad a un driver.
/// </summary>
public sealed class CrashInfo
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string DumpFileName { get; init; } = string.Empty;
    public DateTimeOffset? OccurredAt { get; init; }
    public uint BugCheckCode { get; init; }
    public string BugCheckName { get; init; } = string.Empty;
    public string BugCheckDescription { get; init; } = string.Empty;
    public IReadOnlyList<ulong> Parameters { get; init; } = Array.Empty<ulong>();
    public string SuspectedModule { get; init; } = string.Empty;
    public CrashAnalysisConfidence Confidence { get; init; } = CrashAnalysisConfidence.Unknown;
    public string EvidenceSummary { get; init; } = string.Empty;
}
