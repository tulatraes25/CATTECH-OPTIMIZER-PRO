using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.Infrastructure.Repair;

/// <summary>
/// Catálogo cerrado y no ejecutable de SFC/DISM. Ningún parámetro proviene del usuario.
/// Debe complementarse con un ejecutor aislado y preflight antes de permitir operaciones.
/// </summary>
public sealed class SystemRepairPlanService : ISystemRepairPlanService
{
    private static readonly IReadOnlyList<SystemRepairOperation> Operations = new[]
    {
        SystemRepairOperation.DismCheckHealth,
        SystemRepairOperation.DismScanHealth,
        SystemRepairOperation.SfcVerifyOnly,
        SystemRepairOperation.DismRestoreHealth,
        SystemRepairOperation.SfcScanNow
    };

    public IReadOnlyList<SystemRepairOperationPlan> GetAvailablePlans()
        => Operations.Select(CreatePlan).ToArray();

    public SystemRepairOperationPlan CreatePlan(SystemRepairOperation operation) => operation switch
    {
        SystemRepairOperation.DismCheckHealth => Build(
            operation, "Comprobar indicadores DISM (CheckHealth)", "dism.exe",
            new[] { "/Online", "/Cleanup-Image", "/CheckHealth" },
            SystemRepairImpact.InspectionOnly, false,
            "Consulta indicadores del almacén de componentes. Un resultado sin daño registrado no garantiza integridad total."),

        SystemRepairOperation.DismScanHealth => Build(
            operation, "Analizar el almacén de componentes (ScanHealth)", "dism.exe",
            new[] { "/Online", "/Cleanup-Image", "/ScanHealth" },
            SystemRepairImpact.InspectionOnly, false,
            "Análisis profundo sin reparación explícita; puede tardar y registrar actividad/logs del sistema."),

        SystemRepairOperation.SfcVerifyOnly => Build(
            operation, "Verificar archivos protegidos (SFC VerifyOnly)", "sfc.exe",
            new[] { "/verifyonly" },
            SystemRepairImpact.InspectionOnly, false,
            "Verifica archivos protegidos sin intentar repararlos; no detecta todos los problemas posibles."),

        SystemRepairOperation.DismRestoreHealth => Build(
            operation, "Reparar almacén de componentes (DISM RestoreHealth)", "dism.exe",
            new[] { "/Online", "/Cleanup-Image", "/RestoreHealth" },
            SystemRepairImpact.ModifiesWindows, true,
            "Reparación que modifica componentes de Windows y podría contactar servicios de actualización. Requiere backup y autorización."),

        SystemRepairOperation.SfcScanNow => Build(
            operation, "Verificar y reparar archivos protegidos (SFC ScanNow)", "sfc.exe",
            new[] { "/scannow" },
            SystemRepairImpact.ModifiesWindows, false,
            "SFC /scannow intenta reparar archivos protegidos automáticamente; no es una operación de solo lectura."),

        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation,
            "Operación de reparación no admitida por CATTECH.")
    };

    private static SystemRepairOperationPlan Build(
        SystemRepairOperation operation,
        string displayName,
        string executable,
        IReadOnlyList<string> args,
        SystemRepairImpact impact,
        bool mayUseNetwork,
        string safetyNote)
        => new()
        {
            Operation = operation,
            DisplayName = displayName,
            ExecutableName = executable,
            Arguments = args,
            Impact = impact,
            RequiresAdministrator = true,
            RequiresExplicitConfirmation = true,
            MayUseNetwork = mayUseNetwork,
            SafetyNote = safetyNote,
            ExecutionAvailable = false
        };
}
