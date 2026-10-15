using System.Globalization;
using System.Text.RegularExpressions;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Parsea exclusivamente el resumen diagnóstico de DumpChk (Microsoft).
/// No ejecuta DumpChk, no lee archivos .dmp y no retiene la salida completa.
/// Las cadenas numéricas de DumpChk se interpretan como hexadecimal.
/// </summary>
public static class DumpCheckOutputParser
{
    private const int MaxOutputChars = 256_000;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Multiline
        | RegexOptions.CultureInvariant;

    public static DumpCheckResult Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Indeterminate("No hay salida suficiente para evaluar el archivo.");

        if (raw.Length > MaxOutputChars)
            return Indeterminate("La salida excedió el tamaño máximo admitido.");

        if (raw.Contains("DebugClient cannot open DumpFile", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Could not match Dump File signature", StringComparison.OrdinalIgnoreCase))
        {
            return new DumpCheckResult
            {
                Readability = DumpCheckReadability.InvalidFormat,
                Message = "DumpChk informó que no pudo abrir el archivo o que su formato es inválido."
            };
        }

        if (!raw.Contains("Finished dump check", StringComparison.OrdinalIgnoreCase))
            return Indeterminate("DumpChk no confirmó el final de la comprobación.");

        try
        {
            uint? code = null;
            var codeMatch = Regex.Match(
                raw, @"\bBUGCHECK_CODE\s*:\s*(?:0x)?([0-9a-f]{1,8})\b",
                Options, RegexTimeout);
            if (codeMatch.Success
                && uint.TryParse(codeMatch.Groups[1].Value,
                    NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed)
                && parsed != 0)
                code = parsed;

            var parameters = new Dictionary<int, ulong>();
            var paramMatches = Regex.Matches(
                raw, @"\bBUGCHECK_P([1-4])\s*:\s*(?:0x)?([0-9a-f]{1,16})\b",
                Options, RegexTimeout);
            foreach (Match match in paramMatches)
            {
                if (!match.Success
                    || !int.TryParse(match.Groups[1].Value, out var index)
                    || parameters.ContainsKey(index))
                    continue;

                if (ulong.TryParse(match.Groups[2].Value,
                        NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var param))
                    parameters[index] = param;
            }

            return new DumpCheckResult
            {
                Readability = DumpCheckReadability.ProbablyReadable,
                BugCheckCode = code,
                Parameters = parameters,
                Message = "DumpChk finalizó. El archivo es probablemente legible; no se garantiza su integridad completa."
            };
        }
        catch (RegexMatchTimeoutException)
        {
            return Indeterminate("No fue posible interpretar la salida dentro del límite de tiempo.");
        }
    }

    private static DumpCheckResult Indeterminate(string message)
        => new() { Readability = DumpCheckReadability.Indeterminate, Message = message };
}
