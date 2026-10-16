namespace Cattech.Optimizer.Pro.Core.Models.Repair;

/// <summary>
/// Review statuses only. No status authorizes a repair command to execute.
/// </summary>
public enum SystemRepairPreflightStatus
{
    UnsupportedPlatform,
    AdministratorRequired,
    TrustedToolMissing,
    UntrustedToolPath,
    ProbeUnavailable,
    ReviewRequired
}

/// <summary>
/// Read-only preflight for one fixed SFC/DISM plan.
/// The separate execution gate remains intentionally absent.
/// </summary>
public sealed class SystemRepairPreflightResult
{
    public SystemRepairOperation Operation { get; init; }
    public SystemRepairPreflightStatus Status { get; init; }
    public string ExecutableName { get; init; } = string.Empty;
    public string TrustedToolPath { get; init; } = string.Empty;
    public string SafetyNote { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool RequiresExplicitConfirmation { get; init; } = true;
    public bool ExecutionAuthorized => false;
}
