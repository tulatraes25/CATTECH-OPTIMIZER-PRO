using System.Globalization;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Decoder read-only de Bug Check Codes basado en un catálogo JSON local.
/// No ejecuta procesos, no abre dumps y no modifica Windows.
/// </summary>
public sealed class BugCheckDecoder : IBugCheckDecoder
{
    private readonly IReadOnlyDictionary<uint, BugCheckDecodeResult> _catalog;

    /// <summary>
    /// Indica si el catálogo pudo cargarse. False no significa que el código
    /// consultado sea desconocido: la fuente local no está disponible.
    /// </summary>
    public bool CatalogAvailable { get; }

    public BugCheckDecoder(string? catalogPath = null)
    {
        var path = catalogPath ?? Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            "bugcheckcodes.json");

        try
        {
            _catalog = File.Exists(path)
                ? LoadCatalog(File.ReadAllText(path))
                : new Dictionary<uint, BugCheckDecodeResult>();
            CatalogAvailable = _catalog.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fuente inaccesible/corrupta: diagnóstico conservador, nunca crash.
            _catalog = new Dictionary<uint, BugCheckDecodeResult>();
            CatalogAvailable = false;
        }
    }

    internal BugCheckDecoder(IReadOnlyDictionary<uint, BugCheckDecodeResult> catalog)
    {
        _catalog = catalog;
        CatalogAvailable = catalog.Count > 0;
    }

    public BugCheckDecodeResult Decode(uint code)
    {
        if (_catalog.TryGetValue(code, out var known))
            return known;

        return new BugCheckDecodeResult
        {
            Code = code,
            IsKnown = false
        };
    }

    internal static IReadOnlyDictionary<uint, BugCheckDecodeResult> LoadCatalog(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<uint, BugCheckDecodeResult>();

        var entries = JsonSerializer.Deserialize<List<CatalogEntry>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<CatalogEntry>();

        var result = new Dictionary<uint, BugCheckDecodeResult>();

        foreach (var entry in entries)
        {
            if (!TryParseCode(entry.Code, out var code))
                continue;

            if (string.IsNullOrWhiteSpace(entry.Name))
                continue;

            // El primer valor gana para evitar que un duplicado posterior cambie
            // silenciosamente el significado de un código ya definido.
            if (result.ContainsKey(code))
                continue;

            result[code] = new BugCheckDecodeResult
            {
                Code = code,
                Name = entry.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(entry.Description)
                    ? "Sin descripción disponible en el catálogo local."
                    : entry.Description.Trim(),
                TypicalAreas = entry.TypicalAreas?
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToArray()
                    ?? Array.Empty<string>(),
                IsKnown = true
            };
        }

        return result;
    }

    internal static bool TryParseCode(string? raw, out uint code)
    {
        code = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var value = raw.Trim();

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return uint.TryParse(
                value.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out code);
        }

        return uint.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out code);
    }

    private sealed class CatalogEntry
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string>? TypicalAreas { get; set; }
    }
}
