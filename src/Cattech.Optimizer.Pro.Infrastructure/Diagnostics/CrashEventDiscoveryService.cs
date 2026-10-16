using System.Diagnostics.Eventing.Reader;
using System.Security;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

internal interface ICrashEventXmlEnumerator
{
    IEnumerable<string> Enumerate(int maxEvents);
}

/// <summary>Accede solo al registro System existente, en orden de eventos recientes.</summary>
internal sealed class WindowsCrashEventXmlEnumerator : ICrashEventXmlEnumerator
{
    public IEnumerable<string> Enumerate(int maxEvents)
    {
        const string queryText = "*[System[(EventID=1001 or EventID=41)]]";
        var query = new EventLogQuery("System", PathType.LogName, queryText)
        {
            ReverseDirection = true
        };

        using var reader = new EventLogReader(query);
        for (var index = 0; index < maxEvents; index++)
        {
            using var record = reader.ReadEvent();
            if (record is null)
                yield break;

            yield return record.ToXml();
        }
    }
}

/// <summary>
/// Descubrimiento read-only y conservador de eventos BugCheck/WER.
/// No ejecuta herramientas, no persiste XML ni registra información privada.
/// </summary>
public sealed class CrashEventDiscoveryService : ICrashEventDiscoveryService
{
    private const int DefaultMaxEvents = 400;
    private readonly ICrashEventXmlEnumerator _enumerator;
    private readonly int _maxEvents;
    private readonly bool _requireWindows;

    public CrashEventDiscoveryService()
        : this(new WindowsCrashEventXmlEnumerator(), DefaultMaxEvents, requireWindows: true)
    {
    }

    internal CrashEventDiscoveryService(
        ICrashEventXmlEnumerator enumerator, int maxEvents = DefaultMaxEvents, bool requireWindows = false)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        if (maxEvents is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(maxEvents));

        _maxEvents = maxEvents;
        _requireWindows = requireWindows;
    }

    public CrashEventDiscoveryResult Discover()
    {
        if (_requireWindows && !OperatingSystem.IsWindows())
            return new CrashEventDiscoveryResult
            {
                Status = CrashEventDiscoveryStatus.UnsupportedPlatform,
                Message = "La lectura de eventos BugCheck requiere Windows."
            };

        var events = new List<CrashEventEvidence>();
        var scanned = 0;

        try
        {
            foreach (var xml in _enumerator.Enumerate(_maxEvents + 1))
            {
                scanned++;
                if (scanned > _maxEvents)
                    break;

                if (CrashEventXmlParser.TryParse(xml, out var item) && item != null)
                    events.Add(item);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(CrashEventDiscoveryStatus.AccessDenied, "Acceso denegado al registro System.");
        }
        catch (SecurityException)
        {
            return Failure(CrashEventDiscoveryStatus.AccessDenied, "Acceso restringido al registro System.");
        }
        catch (Exception ex) when (ex is EventLogException or IOException or ArgumentException)
        {
            return Failure(CrashEventDiscoveryStatus.ReadError, "No se pudo completar la consulta del registro System.");
        }

        return new CrashEventDiscoveryResult
        {
            Status = events.Count > 0 ? CrashEventDiscoveryStatus.Found : CrashEventDiscoveryStatus.NoEvents,
            Events = events,
            EventsScanned = Math.Min(scanned, _maxEvents),
            IsTruncated = scanned > _maxEvents,
            Message = events.Count > 0
                ? "Eventos asociados a BugCheck detectados; podrían referirse al mismo incidente."
                : "Sin eventos confirmados en esta consulta. No implica ausencia histórica de BSOD."
        };
    }

    private static CrashEventDiscoveryResult Failure(CrashEventDiscoveryStatus status, string message)
        => new() { Status = status, Message = message };
}
