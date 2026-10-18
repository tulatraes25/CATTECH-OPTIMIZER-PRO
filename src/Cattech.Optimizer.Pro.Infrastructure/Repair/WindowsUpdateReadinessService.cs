using System.Management;
using System.Runtime.InteropServices;
using System.Security;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Repair;

namespace Cattech.Optimizer.Pro.Infrastructure.Repair;

internal sealed class WindowsServiceRawState
{
    public string Name { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string StartMode { get; init; } = string.Empty;
}

internal interface IWindowsUpdateServiceReader
{
    IReadOnlyList<WindowsServiceRawState> Read();
}

/// <summary>
/// Una sola consulta WMI SELECT explícita. No Win32_Process, no usuarios,
/// no registry write, no StartService/StopService.
/// </summary>
internal sealed class WindowsUpdateWmiServiceReader : IWindowsUpdateServiceReader
{
    public IReadOnlyList<WindowsServiceRawState> Read()
    {
        const string query = "SELECT Name,State,StartMode FROM Win32_Service " +
            "WHERE Name='wuauserv' OR Name='BITS' OR Name='cryptsvc' OR Name='UsoSvc'";
        var result = new List<WindowsServiceRawState>();

        using var searcher = new ManagementObjectSearcher(query);
        using var collection = searcher.Get();
        foreach (ManagementObject item in collection)
        {
            using (item)
            {
                result.Add(new WindowsServiceRawState
                {
                    Name = item["Name"]?.ToString() ?? string.Empty,
                    State = item["State"]?.ToString() ?? string.Empty,
                    StartMode = item["StartMode"]?.ToString() ?? string.Empty
                });
            }
        }
        return result;
    }
}

/// <summary>
/// Observación conservadora de servicios asociados con Windows Update.
/// 'Stopped' puede ser normal (servicios bajo demanda) y NO indica automáticamente
/// Windows Update roto. No modifica servicios ni archivos.
/// </summary>
public sealed class WindowsUpdateReadinessService : IWindowsUpdateReadinessService
{
    private static readonly string[] ServiceNames =
        { "wuauserv", "BITS", "cryptsvc", "UsoSvc" };

    private readonly IWindowsUpdateServiceReader _reader;
    private readonly bool _requireWindows;

    public WindowsUpdateReadinessService()
        : this(new WindowsUpdateWmiServiceReader(), requireWindows: true)
    {
    }

    internal WindowsUpdateReadinessService(
        IWindowsUpdateServiceReader reader, bool requireWindows = false)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _requireWindows = requireWindows;
    }

    public WindowsUpdateReadinessResult Inspect()
    {
        if (_requireWindows && !OperatingSystem.IsWindows())
            return Failure(WindowsUpdateReadinessStatus.UnsupportedPlatform,
                "Esta inspección requiere Windows.");

        IReadOnlyList<WindowsServiceRawState> raw;
        try
        {
            raw = _reader.Read();
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            return Failure(WindowsUpdateReadinessStatus.AccessDenied,
                "No hay permisos para consultar servicios de Windows Update.");
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return Failure(WindowsUpdateReadinessStatus.AccessDenied,
                "No hay permisos para consultar servicios de Windows Update.");
        }
        catch (Exception ex) when (ex is ManagementException or COMException or IOException or InvalidOperationException)
        {
            return Failure(WindowsUpdateReadinessStatus.ReadError,
                "No se pudo completar la consulta de servicios.");
        }

        // WMI normally returns at most one entry for each of the four
        // explicitly requested service names. Duplicates or foreign rows
        // make a "complete" observation ambiguous; never choose the first
        // contradictory result or repeat provider-supplied private text.
        if (raw is null)
            return Failure(WindowsUpdateReadinessStatus.ReadError,
                "La consulta devolvió información inconsistente.");

        var allowed = new HashSet<string>(ServiceNames,
            StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in raw)
        {
            if (item is null || !allowed.Contains(item.Name)
                || !seen.Add(item.Name))
                return Failure(WindowsUpdateReadinessStatus.ReadError,
                    "La consulta devolvió información inconsistente.");
        }

        var observations = ServiceNames.Select(name =>
        {
            var item = raw.FirstOrDefault(x =>
                string.Equals(name, x.Name, StringComparison.OrdinalIgnoreCase));
            return new WindowsUpdateServiceObservation
            {
                ServiceName = name,
                State = item == null ? WindowsServiceRunState.NotFound
                    : ParseState(item.State),
                StartMode = item == null ? "N/D" : SanitizeStartMode(item.StartMode)
            };
        }).ToArray();

        var missing = observations.Count(x => x.State == WindowsServiceRunState.NotFound);
        return new WindowsUpdateReadinessResult
        {
            Status = missing == 0 ? WindowsUpdateReadinessStatus.Observed
                : WindowsUpdateReadinessStatus.Partial,
            Services = observations,
            Message = missing == 0
                ? "Se observó el estado de cuatro servicios. Esto no confirma que Windows Update funcione correctamente."
                : $"Consulta parcial: {missing} servicio(s) no encontrado(s). No significa necesariamente una avería."
        };
    }

    private static WindowsServiceRunState ParseState(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "running" => WindowsServiceRunState.Running,
            "stopped" => WindowsServiceRunState.Stopped,
            _ => WindowsServiceRunState.Other
        };

    private static string SanitizeStartMode(string? raw)
        => raw?.Trim().ToLowerInvariant() switch
        {
            "auto" => "Automático",
            "manual" => "Manual",
            "disabled" => "Deshabilitado",
            _ => "N/D"
        };

    private static WindowsUpdateReadinessResult Failure(
        WindowsUpdateReadinessStatus status, string message)
        => new() { Status = status, Message = message };
}
