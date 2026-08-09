using System.Text;

namespace Cattech.Optimizer.Pro.Infrastructure.Smart;

/// <summary>
/// Construye argumentos de smartctl para comandos por dispositivo,
/// preservando el transporte -d TYPE detectado.
/// ApproximateDiskType (clasificación visual CATTECH) NO se usa como -d.
/// </summary>
internal static class SmartctlCommandBuilder
{
    /// <summary>
    /// Construye: [opciones] [-d TYPE] deviceName
    /// Si deviceType está vacío o es "auto", se omite -d (autodetección smartctl).
    /// </summary>
    public static string BuildDeviceArguments(
        IEnumerable<string> options,
        string deviceName,
        string? deviceType)
    {
        var args = new List<string>(options);

        if (!string.IsNullOrWhiteSpace(deviceType) &&
            !string.Equals(deviceType, "auto", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("-d");
            args.Add(deviceType);
        }

        args.Add(deviceName);

        return string.Join(" ", args);
    }

    /// <summary>
    /// Construye los argumentos de análisis completo: -a -j [-d TYPE] device
    /// </summary>
    public static string BuildAnalyzeArguments(string deviceName, string? deviceType)
        => BuildDeviceArguments(["-a", "-j"], deviceName, deviceType);

    /// <summary>
    /// Construye los argumentos de inicio de self-test: -t short|long -j [-d TYPE] device
    /// </summary>
    public static string BuildStartTestArguments(
        string deviceName,
        string? deviceType,
        bool extended)
        => BuildDeviceArguments(extended ? ["-t", "long", "-j"] : ["-t", "short", "-j"], deviceName, deviceType);

    /// <summary>
    /// Construye los argumentos de consulta del self-test log: -l selftest -j [-d TYPE] device
    /// </summary>
    public static string BuildSelfTestLogArguments(string deviceName, string? deviceType)
        => BuildDeviceArguments(["-l", "selftest", "-j"], deviceName, deviceType);
}
