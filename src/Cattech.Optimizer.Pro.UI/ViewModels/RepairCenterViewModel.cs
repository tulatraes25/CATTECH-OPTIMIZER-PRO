using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>
/// Presentation-only description of an allowlisted system operation.
/// This type exposes no execution capability.
/// </summary>
public sealed class RepairPlanDisplayItem
{
    public SystemRepairOperation Operation { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string CommandPreview { get; init; } = string.Empty;
    public string ImpactText { get; init; } = string.Empty;
    public string SafetyNote { get; init; } = string.Empty;
    public string NetworkText { get; init; } = string.Empty;
    public bool IsModifying { get; init; }

    internal static RepairPlanDisplayItem FromPlan(SystemRepairOperationPlan plan) => new()
    {
        Operation = plan.Operation,
        DisplayName = plan.DisplayName,
        CommandPreview = plan.ExecutableName + " " + string.Join(" ", plan.Arguments),
        ImpactText = plan.Impact == SystemRepairImpact.ModifiesWindows
            ? "MODIFICA WINDOWS — NO DISPONIBLE"
            : "Inspección — puede escribir logs del sistema",
        SafetyNote = plan.SafetyNote,
        NetworkText = plan.MayUseNetwork
            ? "Podría utilizar conexión de red"
            : "No prevé conexión de red",
        IsModifying = plan.Impact == SystemRepairImpact.ModifiesWindows
    };
}

/// <summary>
/// Sanitized view of the four Windows Update services inspected by the provider.
/// </summary>
public sealed class WindowsUpdateServiceDisplayItem
{
    public string Name { get; init; } = string.Empty;
    public string StateText { get; init; } = string.Empty;
    public string StartModeText { get; init; } = string.Empty;
}

/// <summary>
/// Read-only repair center. Never launches SFC, DISM or Windows Update repairs.
/// No checks run automatically on opening the screen.
/// </summary>
public partial class RepairCenterViewModel : ObservableObject
{
    private static readonly HashSet<string> AllowedServiceNames =
        new(StringComparer.OrdinalIgnoreCase)
        { "wuauserv", "BITS", "cryptsvc", "UsoSvc" };

    private readonly IWindowsUpdateReadinessService _updateInspection;
    private readonly ISystemRepairPlanService _planService;
    private readonly ISystemRepairPreflightService _preflight;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _updateStatusText =
        "Sin consultar. Los servicios detenidos no indican necesariamente una falla.";

    [ObservableProperty]
    private string _preflightStatusText =
        "Seleccioná un plan y pulsá «Revisar requisitos». No se ejecutan comandos.";

    [ObservableProperty]
    private RepairPlanDisplayItem? _selectedPlan;

    /// <summary>
    /// Comandos exactos del plan seleccionado, en texto monoespaciado read-only.
    /// Solo describe el comando: esta versión no ejecuta reparaciones.
    /// </summary>
    [ObservableProperty]
    private string _selectedPlanCommandsText = "(sin plan seleccionado)";

    [ObservableProperty]
    private string _copyStatusText = string.Empty;

    public ObservableCollection<WindowsUpdateServiceDisplayItem> UpdateServices { get; } = new();
    public ObservableCollection<RepairPlanDisplayItem> Plans { get; } = new();

    public RepairCenterViewModel(
        IWindowsUpdateReadinessService updateInspection,
        ISystemRepairPlanService planService,
        ISystemRepairPreflightService preflight)
    {
        _updateInspection = updateInspection ?? throw new ArgumentNullException(nameof(updateInspection));
        _planService = planService ?? throw new ArgumentNullException(nameof(planService));
        _preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));

        // Pure metadata only: does not inspect Windows or invoke a repair tool.
        foreach (var plan in _planService.GetAvailablePlans())
            Plans.Add(RepairPlanDisplayItem.FromPlan(plan));
    }

    private long _planSelectionRevision;

    partial void OnSelectedPlanChanged(RepairPlanDisplayItem? value)
    {
        // A -> B -> A must not resurrect an earlier asynchronous preflight.
        // This is a presentation revision, not execution authorization.
        _planSelectionRevision++;
        // The previous preflight must never be displayed for a newly selected plan.
        PreflightStatusText =
            "Sin revisar este plan. No se ejecutan comandos ni se autoriza una reparación.";
        // El panel monoespaciado siempre muestra el comando exacto del plan actual.
        SelectedPlanCommandsText = value == null
            ? "(sin plan seleccionado)"
            : BuildCommandsText(value);
        InspectSelectedPlanCommand.NotifyCanExecuteChanged();
        CopyCommandsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        InspectWindowsUpdateCommand.NotifyCanExecuteChanged();
        InspectSelectedPlanCommand.NotifyCanExecuteChanged();
    }

    private bool CanInspectWindowsUpdate() => !IsBusy;
    private bool CanInspectSelectedPlan() => !IsBusy && SelectedPlan != null && Plans.Contains(SelectedPlan);

    [RelayCommand(CanExecute = nameof(CanInspectWindowsUpdate))]
    private async Task InspectWindowsUpdateAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        UpdateStatusText = "Consultando servicios asociados con Windows Update...";
        // Drop previous evidence before a fresh inspection.
        UpdateServices.Clear();

        try
        {
            var result = await Task.Run(_updateInspection.Inspect);
            foreach (var service in result.Services)
            {
                // Only known Windows Update service names are shown in the UI.
                if (!AllowedServiceNames.Contains(service.ServiceName))
                    continue;

                UpdateServices.Add(new WindowsUpdateServiceDisplayItem
                {
                    Name = service.ServiceName,
                    StateText = service.State switch
                    {
                        WindowsServiceRunState.Running => "En ejecución",
                        WindowsServiceRunState.Stopped => "Detenido (puede ser normal)",
                        WindowsServiceRunState.NotFound => "No encontrado",
                        _ => "Estado no determinado"
                    },
                    StartModeText = service.StartMode is "Automático" or "Manual"
                        or "Deshabilitado" ? service.StartMode : "N/D"
                });
            }

            UpdateStatusText = result.Status switch
            {
                WindowsUpdateReadinessStatus.Observed =>
                    "Consulta realizada. Esto NO confirma que Windows Update funcione correctamente.",
                WindowsUpdateReadinessStatus.Partial =>
                    "Consulta parcial: no se encontraron todos los servicios esperados. No implica necesariamente una avería.",
                WindowsUpdateReadinessStatus.AccessDenied =>
                    "Sin permisos para consultar el estado de Windows Update.",
                WindowsUpdateReadinessStatus.UnsupportedPlatform =>
                    "Esta consulta requiere Windows.",
                _ => "No se pudo completar la consulta. Estado no determinado."
            };
        }
        catch (Exception)
        {
            UpdateServices.Clear();
            UpdateStatusText = "No se pudo completar la consulta. Estado no determinado.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInspectSelectedPlan))]
    private async Task InspectSelectedPlanAsync()
    {
        var plan = SelectedPlan;
        if (IsBusy || plan == null || !Plans.Contains(plan))
            return;

        var selectionRevision = _planSelectionRevision;
        IsBusy = true;
        PreflightStatusText = "Revisando requisitos sin ejecutar la herramienta...";
        try
        {
            var result = await Task.Run(() => _preflight.Inspect(plan.Operation));
            // Reject selection ABA even if the exact same plan object returns.
            // The source check is read-only and never authorizes execution.
            if (_planSelectionRevision != selectionRevision || SelectedPlan != plan)
                return;

            if (result.Operation != plan.Operation || result.ExecutionAuthorized)
            {
                PreflightStatusText = "No se ejecutó nada: el resultado no corresponde al plan seleccionado.";
                return;
            }

            PreflightStatusText = result.Status switch
            {
                SystemRepairPreflightStatus.ReviewRequired =>
                    "Requisitos inspeccionados. La ejecución SIGUE DESHABILITADA; falta aprobación y revisión.",
                SystemRepairPreflightStatus.AdministratorRequired =>
                    "No se ejecutó nada: se requieren privilegios de administrador.",
                SystemRepairPreflightStatus.UnsupportedPlatform =>
                    "No se ejecutó nada: Windows no compatible.",
                SystemRepairPreflightStatus.TrustedToolMissing =>
                    "No se ejecutó nada: falta la herramienta del sistema.",
                SystemRepairPreflightStatus.UntrustedToolPath =>
                    "No se ejecutó nada: herramienta o ruta no confiable.",
                _ => "No se ejecutó nada: no pudieron verificarse los requisitos."
            };
        }
        catch (Exception)
        {
            if (_planSelectionRevision == selectionRevision && SelectedPlan == plan)
                PreflightStatusText = "No se ejecutó nada: no se pudieron verificar los requisitos.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Copia de comandos/planes (solo texto; nunca ejecuta) ---

    private bool CanCopyCommands()
        => SelectedPlan != null && Plans.Contains(SelectedPlan);

    [RelayCommand(CanExecute = nameof(CanCopyCommands))]
    private void CopyCommands()
    {
        if (SelectedPlan == null)
        {
            CopyStatusText = "No hay plan seleccionado.";
            return;
        }

        try
        {
            Clipboard.SetText(BuildCommandsText(SelectedPlan));
            CopyStatusText = "Comandos del plan copiados al portapapeles.";
        }
        catch (Exception)
        {
            CopyStatusText = "No se pudieron copiar los comandos.";
        }
    }

    [RelayCommand]
    private void CopyFullPlan()
    {
        try
        {
            Clipboard.SetText(BuildFullPlanText());
            CopyStatusText = "Plan completo copiado al portapapeles.";
        }
        catch (Exception)
        {
            CopyStatusText = "No se pudo copiar el plan completo.";
        }
    }

    /// <summary>
    /// Comandos exactos de un plan (ejecutable + argumentos fijos). Solo texto:
    /// no autoriza ni ejecuta nada.
    /// </summary>
    public static string BuildCommandsText(RepairPlanDisplayItem plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // El comando se reconstruye desde el catálogo fijo, nunca desde input de usuario.
        return $"{plan.CommandPreview}{Environment.NewLine}";
    }

    /// <summary>
    /// Descripción completa del catálogo de planes + preflight del plan
    /// seleccionado. Texto para revisión manual controlada; la ejecución no
    /// está implementada en esta versión.
    /// </summary>
    public string BuildFullPlanText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("CATTECH Optimizer Pro — Centro de reparación (solo preparación)");
        sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("EJECUCIÓN NO IMPLEMENTADA EN ESTA VERSIÓN.");
        sb.AppendLine("Los comandos son para ejecución manual controlada por el técnico.");
        sb.AppendLine();

        for (var i = 0; i < Plans.Count; i++)
        {
            var plan = Plans[i];
            sb.AppendLine($"[{i + 1}] {plan.DisplayName}");
            sb.AppendLine($"    comando: {plan.CommandPreview}");
            sb.AppendLine($"    impacto: {plan.ImpactText}");
            sb.AppendLine($"    red: {plan.NetworkText}");
            sb.AppendLine($"    nota: {plan.SafetyNote}");
            sb.AppendLine();
        }

        sb.AppendLine("PREFLIGHT DEL PLAN SELECCIONADO");
        if (SelectedPlan == null)
        {
            sb.AppendLine("- (sin plan seleccionado)");
        }
        else
        {
            sb.AppendLine($"plan: {SelectedPlan.DisplayName}");
            sb.AppendLine($"comando: {SelectedPlan.CommandPreview}");
            sb.AppendLine($"estado: {PreflightStatusText}");
        }

        sb.AppendLine();
        sb.AppendLine($"ESTADO WINDOWS UPDATE: {UpdateStatusText}");

        if (UpdateServices.Count > 0)
        {
            sb.AppendLine("SERVICIOS OBSERVADOS");
            foreach (var service in UpdateServices)
            {
                sb.AppendLine(
                    $"{service.Name}\testado={service.StateText}\tinicio={service.StartModeText}");
            }
        }

        return sb.ToString();
    }
}
