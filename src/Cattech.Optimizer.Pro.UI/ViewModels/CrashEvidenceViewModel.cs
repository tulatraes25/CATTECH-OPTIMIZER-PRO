using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.UI.ViewModels;

/// <summary>Entrada UI sin rutas absolutas, XML ni datos privados.</summary>
public sealed class CrashEventDisplayItem
{
    public string DateText { get; init; } = "N/D";
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Pantalla BSOD exclusivamente de lectura. La consulta ocurre solo al pulsar
/// Actualizar: no ejecuta reparaciones ni lee contenido de archivos .dmp.
/// </summary>
public partial class CrashEvidenceViewModel : ObservableObject
{
    private readonly ICrashDumpDiscoveryService _dumpDiscovery;
    private readonly ICrashEventDiscoveryService _eventDiscovery;
    private readonly IBugCheckDecoder _decoder;
    private readonly IDumpCheckService? _dumpCheckService;
    private readonly Func<string, bool>? _confirmDumpCheck;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Presioná «Buscar evidencia» para consultar el equipo.";

    [ObservableProperty]
    private string _dumpStatusText = "Sin consultar";

    [ObservableProperty]
    private string _eventStatusText = "Sin consultar";

    [ObservableProperty]
    private string _summaryText = "La ausencia de archivos o eventos no demuestra ausencia de fallas.";

    [ObservableProperty]
    private CrashDumpEntry? _selectedDump;

    [ObservableProperty]
    private string _dumpCheckStatusText =
        "Seleccioná un archivo .dmp y confirmá expresamente si querés verificarlo con DumpChk.";

    public ObservableCollection<CrashDumpEntry> Dumps { get; } = new();
    public ObservableCollection<CrashEventDisplayItem> Events { get; } = new();

    public CrashEvidenceViewModel(
        ICrashDumpDiscoveryService dumpDiscovery,
        ICrashEventDiscoveryService eventDiscovery,
        IBugCheckDecoder decoder,
        IDumpCheckService? dumpCheckService = null,
        Func<string, bool>? confirmDumpCheck = null)
    {
        _dumpDiscovery = dumpDiscovery ?? throw new ArgumentNullException(nameof(dumpDiscovery));
        _eventDiscovery = eventDiscovery ?? throw new ArgumentNullException(nameof(eventDiscovery));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _dumpCheckService = dumpCheckService;
        _confirmDumpCheck = confirmDumpCheck;
    }

    partial void OnSelectedDumpChanged(CrashDumpEntry? value)
        => VerifySelectedDumpCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
        => VerifySelectedDumpCommand.NotifyCanExecuteChanged();

    private bool CanVerifySelectedDump()
        => !IsBusy
           && SelectedDump != null
           && _dumpCheckService != null
           && _confirmDumpCheck != null;

    [RelayCommand(CanExecute = nameof(CanVerifySelectedDump))]
    private async Task VerifySelectedDumpAsync()
    {
        if (!CanVerifySelectedDump())
            return;

        var fileName = SelectedDump!.FileName;

        // La confirmación ocurre desde una interacción directa del técnico, no
        // en el constructor, Refresh ni selección automática de un archivo.
        if (!_confirmDumpCheck!(fileName))
        {
            DumpCheckStatusText = "Comprobación cancelada por el técnico. No se ejecutó DumpChk.";
            return;
        }

        IsBusy = true;
        DumpCheckStatusText = "Comprobando archivo seleccionado con DumpChk...";
        try
        {
            var result = await _dumpCheckService!.VerifyDumpAsync(
                fileName, technicianConfirmed: true);

            DumpCheckStatusText = result.Summary?.BugCheckCode is uint code
                ? $"{result.Message} Código registrado: 0x{code:X8}. " +
                  "No se determinó ningún driver culpable."
                : result.Message;
        }
        catch (Exception)
        {
            DumpCheckStatusText = "No fue posible comprobar el archivo. No se conservó la salida.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusText = "Leyendo metadatos y eventos (sin cambios en Windows)...";
        try
        {
            // No bloquear el hilo WPF al consultar disco/Event Log.
            var dumps = await Task.Run(_dumpDiscovery.Discover);
            var events = await Task.Run(_eventDiscovery.Discover);

            SelectedDump = null;
            Dumps.Clear();
            foreach (var dump in dumps.Dumps)
                Dumps.Add(dump);

            Events.Clear();
            foreach (var item in events.Events)
            {
                var decoded = _decoder.Decode(item.BugCheckCode);
                Events.Add(new CrashEventDisplayItem
                {
                    DateText = item.TimeCreatedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "N/D",
                    Code = item.HexCode,
                    Name = decoded.IsKnown ? decoded.Name : "No identificado en catálogo",
                    Source = item.Source,
                    Description = decoded.Description
                });
            }

            DumpStatusText = DescribeDumps(dumps);
            EventStatusText = DescribeEvents(events);
            SummaryText = $"Archivos .dmp: {Dumps.Count}. Eventos asociados: {Events.Count}. " +
                "Ambas fuentes pueden referirse al mismo incidente; no representan fallas distintas.";
            StatusText = "Consulta de evidencia finalizada. No se modificó Windows.";
        }
        catch (Exception)
        {
            // No exponer mensajes internos que puedan incluir rutas o datos personales.
            StatusText = "No se pudo completar la consulta. No se modificó Windows.";
            DumpStatusText = "Consulta incompleta";
            EventStatusText = "Consulta incompleta";
            SummaryText = "No interpretar un resultado incompleto como ausencia de fallas.";
            SelectedDump = null;
            Dumps.Clear();
            Events.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string DescribeDumps(CrashDumpDiscoveryResult value) => value.Status switch
    {
        CrashDumpDiscoveryStatus.Found => value.IsTruncated
            ? $"Encontrados {value.FilesObserved}; se muestran los {value.Dumps.Count} más recientes."
            : $"Encontrados {value.Dumps.Count} archivo(s) de volcado.",
        CrashDumpDiscoveryStatus.NoDumps => "No se encontraron archivos .dmp. Esto no descarta fallas.",
        CrashDumpDiscoveryStatus.DirectoryMissing => "La carpeta Minidump no existe.",
        CrashDumpDiscoveryStatus.AccessDenied => "Sin permisos para consultar la carpeta Minidump.",
        CrashDumpDiscoveryStatus.UnsupportedPlatform => "Requiere Windows.",
        _ => "No fue posible consultar la carpeta Minidump."
    };

    private static string DescribeEvents(CrashEventDiscoveryResult value) => value.Status switch
    {
        CrashEventDiscoveryStatus.Found => value.IsTruncated
            ? $"Se muestran {value.Events.Count} eventos del rango consultado (límite alcanzado)."
            : $"Encontrados {value.Events.Count} evento(s) BugCheck asociado(s).",
        CrashEventDiscoveryStatus.NoEvents => "Sin eventos BugCheck confirmados en el rango consultado.",
        CrashEventDiscoveryStatus.AccessDenied => "Sin permisos para consultar el registro System.",
        CrashEventDiscoveryStatus.UnsupportedPlatform => "Requiere Windows.",
        _ => "No fue posible consultar el registro System."
    };
}
