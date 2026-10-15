using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Interfaces;

/// <summary>Inspección opcional read-only de un dump mediante herramienta externa autorizada.</summary>
public interface IDumpCheckService
{
    Task<DumpCheckExecutionResult> VerifyDumpAsync(
        string dumpFileName,
        bool technicianConfirmed,
        CancellationToken cancellationToken = default);
}
