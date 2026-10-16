namespace Cattech.Optimizer.Pro.Core.Models.Repair;

/// <summary>Catálogo cerrado de operaciones compatibles con CATTECH v0.3.</summary>
public enum SystemRepairOperation
{
    DismCheckHealth,
    DismScanHealth,
    SfcVerifyOnly,
    DismRestoreHealth,
    SfcScanNow
}

public enum SystemRepairImpact
{
    InspectionOnly,
    ModifiesWindows
}

/// <summary>
/// Plan de operación: no ejecuta comandos. Describe riesgos y argumentos
/// fijos para revisión explícita por el técnico.
/// </summary>
public sealed class SystemRepairOperationPlan
{
    public SystemRepairOperation Operation { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutableName { get; init; } = string.Empty;
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public SystemRepairImpact Impact { get; init; }
    public bool RequiresAdministrator { get; init; } = true;
    public bool RequiresExplicitConfirmation { get; init; } = true;
    public bool MayUseNetwork { get; init; }
    public string SafetyNote { get; init; } = string.Empty;
    public bool ExecutionAvailable { get; init; }
}
