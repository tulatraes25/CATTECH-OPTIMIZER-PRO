namespace Cattech.Optimizer.Pro.Core.Models.Repair;

/// <summary>Resultado de observación de servicios, no diagnóstico de salud del subsistema.</summary>
public enum WindowsUpdateReadinessStatus
{
    Observed,
    Partial,
    AccessDenied,
    ReadError,
    UnsupportedPlatform
}

public enum WindowsServiceRunState
{
    Running,
    Stopped,
    Other,
    NotFound
}

/// <summary>No contiene hostname, usuario ni otras propiedades de WMI.</summary>
public sealed class WindowsUpdateServiceObservation
{
    public string ServiceName { get; init; } = string.Empty;
    public WindowsServiceRunState State { get; init; }
    public string StartMode { get; init; } = "N/D";
}

public sealed class WindowsUpdateReadinessResult
{
    public WindowsUpdateReadinessStatus Status { get; init; }
    public IReadOnlyList<WindowsUpdateServiceObservation> Services { get; init; }
        = Array.Empty<WindowsUpdateServiceObservation>();
    public string Message { get; init; } = string.Empty;
}
