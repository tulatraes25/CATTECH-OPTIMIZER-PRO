using System.ComponentModel;
using System.Security;
using System.Security.Principal;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.Infrastructure.Repair;

internal interface ISystemRepairEnvironmentProbe
{
    bool IsSupportedWindows { get; }
    bool IsElevatedAdministrator { get; }
    string SystemDirectory { get; }
    bool FileExists(string path);
    FileAttributes GetAttributes(string path);
}

/// <summary>
/// Reads Windows process privilege and standard OS paths; never invokes SFC/DISM.
/// </summary>
internal sealed class WindowsSystemRepairEnvironmentProbe : ISystemRepairEnvironmentProbe
{
    public bool IsSupportedWindows => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763);

    public bool IsElevatedAdministrator
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return false;

            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity)
                .IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public string SystemDirectory => OperatingSystem.IsWindows()
        ? Environment.SystemDirectory : string.Empty;

    public bool FileExists(string path) => File.Exists(path);
    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
}

/// <summary>
/// Closed-list, read-only preflight. Even a successful result grants no execution.
/// </summary>
public sealed class SystemRepairPreflightService : ISystemRepairPreflightService
{
    private readonly ISystemRepairPlanService _plans;
    private readonly ISystemRepairEnvironmentProbe _probe;

    public SystemRepairPreflightService()
        : this(new SystemRepairPlanService(), new WindowsSystemRepairEnvironmentProbe())
    {
    }

    internal SystemRepairPreflightService(
        ISystemRepairPlanService plans, ISystemRepairEnvironmentProbe probe)
    {
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public SystemRepairPreflightResult Inspect(SystemRepairOperation operation)
    {
        var plan = _plans.CreatePlan(operation);

        SystemRepairPreflightResult Result(
            SystemRepairPreflightStatus status, string message, string toolPath = "")
            => new()
            {
                Operation = operation,
                Status = status,
                ExecutableName = plan.ExecutableName,
                TrustedToolPath = toolPath,
                SafetyNote = plan.SafetyNote,
                RequiresExplicitConfirmation = true,
                Message = message
            };

        try
        {
            if (!_probe.IsSupportedWindows)
                return Result(SystemRepairPreflightStatus.UnsupportedPlatform,
                    "Solo Windows 10 1809+ o Windows 11 son compatibles.");

            if (!_probe.IsElevatedAdministrator)
                return Result(SystemRepairPreflightStatus.AdministratorRequired,
                    "Se requiere una sesion elevada para revisar esta operacion.");

            if (plan.ExecutableName is not ("sfc.exe" or "dism.exe"))
                return Result(SystemRepairPreflightStatus.UntrustedToolPath,
                    "Herramienta fuera de la lista permitida.");

            var directory = _probe.SystemDirectory;
            if (string.IsNullOrWhiteSpace(directory)
                || !Path.IsPathFullyQualified(directory))
                return Result(SystemRepairPreflightStatus.ProbeUnavailable,
                    "No se pudo determinar System32 de forma confiable.");

            var root = Path.GetFullPath(directory);
            var toolPath = Path.GetFullPath(Path.Combine(root, plan.ExecutableName));
            if (!string.Equals(Path.GetDirectoryName(toolPath), root,
                    StringComparison.OrdinalIgnoreCase))
                return Result(SystemRepairPreflightStatus.UntrustedToolPath,
                    "La ruta de herramienta escapa de System32.");

            var directoryAttributes = _probe.GetAttributes(root);
            if ((directoryAttributes & FileAttributes.Directory) == 0
                || (directoryAttributes & FileAttributes.ReparsePoint) != 0)
                return Result(SystemRepairPreflightStatus.UntrustedToolPath,
                    "El directorio de herramientas no es un System32 regular.");

            if (!_probe.FileExists(toolPath))
                return Result(SystemRepairPreflightStatus.TrustedToolMissing,
                    "No se encontro el ejecutable del sistema esperado.");

            var fileAttributes = _probe.GetAttributes(toolPath);
            if ((fileAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                return Result(SystemRepairPreflightStatus.UntrustedToolPath,
                    "El ejecutable no es un archivo regular.");

            return Result(SystemRepairPreflightStatus.ReviewRequired,
                "Preflight de lectura completo; la ejecucion sigue deshabilitada.",
                toolPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or SecurityException or ArgumentException or NotSupportedException
            or Win32Exception or InvalidOperationException)
        {
            return Result(SystemRepairPreflightStatus.ProbeUnavailable,
                "No pudo verificarse el estado de plataforma o herramientas.");
        }
    }
}
