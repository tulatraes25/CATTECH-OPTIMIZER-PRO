using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Parsea XML de Event Log estructurado, sin depender del idioma del mensaje.
/// Rechaza eventos 41 sin BugcheckCode: un reinicio inesperado no prueba BSOD.
/// Nunca conserva XML ni rutas potencialmente sensibles.
/// </summary>
internal static class CrashEventXmlParser
{
    private const string WerProvider = "Microsoft-Windows-WER-SystemErrorReporting";
    private const string PowerProvider = "Microsoft-Windows-Kernel-Power";

    public static bool TryParse(string? xml, out CrashEventEvidence? evidence)
    {
        evidence = null;

        if (string.IsNullOrWhiteSpace(xml) || xml.Length > 256_000)
            return false;

        try
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 256_000
            });

            var root = XDocument.Load(reader).Root;
            if (root?.Name.LocalName != "Event")
                return false;

            var system = root.Elements().FirstOrDefault(x => x.Name.LocalName == "System");
            var provider = system?.Elements().FirstOrDefault(x => x.Name.LocalName == "Provider")
                ?.Attribute("Name")?.Value;
            var rawId = system?.Elements().FirstOrDefault(x => x.Name.LocalName == "EventID")?.Value;

            if (!int.TryParse(rawId, NumberStyles.None, CultureInfo.InvariantCulture, out var eventId))
                return false;

            var isWer = eventId == 1001
                && string.Equals(provider, WerProvider, StringComparison.OrdinalIgnoreCase);
            var isPower = eventId == 41
                && string.Equals(provider, PowerProvider, StringComparison.OrdinalIgnoreCase);

            if (!isWer && !isPower)
                return false;

            var eventData = root.Elements().FirstOrDefault(x => x.Name.LocalName == "EventData");
            var key = isWer ? "param1" : "BugcheckCode";
            var value = eventData?.Elements().FirstOrDefault(x =>
                x.Name.LocalName == "Data"
                && string.Equals(x.Attribute("Name")?.Value, key, StringComparison.OrdinalIgnoreCase))?.Value;

            if (!TryParseCodePrefix(value, out var code) || code == 0)
                return false;

            var stamp = system?.Elements().FirstOrDefault(x => x.Name.LocalName == "TimeCreated")
                ?.Attribute("SystemTime")?.Value;
            DateTimeOffset? createdUtc = null;
            if (DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                createdUtc = parsed.ToUniversalTime();

            evidence = new CrashEventEvidence
            {
                Source = isWer ? WerProvider : PowerProvider,
                EventId = eventId,
                TimeCreatedUtc = createdUtc,
                BugCheckCode = code
            };
            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    internal static bool TryParseCodePrefix(string? raw, out uint code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();
        var end = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '(', ')', ',' });
        var token = end < 0 ? value : value[..end];
        return BugCheckDecoder.TryParseCode(token, out code);
    }
}
