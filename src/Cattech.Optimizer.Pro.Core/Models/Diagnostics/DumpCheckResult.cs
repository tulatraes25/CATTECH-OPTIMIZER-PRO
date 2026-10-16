namespace Cattech.Optimizer.Pro.Core.Models.Diagnostics;

/// <summary>Evaluación conservadora del resumen de DumpChk, no garantía de integridad.</summary>
public enum DumpCheckReadability
{
    ProbablyReadable,
    InvalidFormat,
    Indeterminate
}

/// <summary>
/// Solo hechos extraídos de la salida temporal. No incluye stdout, rutas,
/// símbolos, datos de proceso ni módulos supuestamente culpables.
/// </summary>
public sealed class DumpCheckResult
{
    public DumpCheckReadability Readability { get; init; }
    public uint? BugCheckCode { get; init; }
    public string BugCheckHexCode => BugCheckCode.HasValue
        ? $"0x{BugCheckCode.Value:X8}" : "N/D";
    public IReadOnlyDictionary<int, ulong> Parameters { get; init; }
        = new Dictionary<int, ulong>();
    public string Message { get; init; } = string.Empty;
}
