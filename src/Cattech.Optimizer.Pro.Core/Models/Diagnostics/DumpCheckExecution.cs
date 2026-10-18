namespace Cattech.Optimizer.Pro.Core.Models.Diagnostics;

/// <summary>Estado de una operación opcional de lectura por DumpChk.</summary>
public enum DumpCheckExecutionStatus
{
    ConfirmationRequired,
    InvalidFileName,
    DumpMissing,
    ToolUnavailable,
    AccessDenied,
    TimedOut,
    Cancelled,
    Completed,
    Failed
}

/// <summary>No incluye stdout/stderr ni rutas del sistema.</summary>
public sealed class DumpCheckExecutionResult
{
    public DumpCheckExecutionStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? ExitCode { get; init; }
    public DumpCheckResult? Summary { get; init; }
}
