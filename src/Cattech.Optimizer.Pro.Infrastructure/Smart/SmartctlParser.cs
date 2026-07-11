using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Infrastructure.Smart;

/// <summary>
/// Parser para salidas de smartctl.
/// Parsea tanto salida JSON como texto plano.
/// </summary>
public static class SmartctlParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parsea la salida de smartctl --version para extraer la versión.
    /// Formato típico: "smartctl 7.4 2023-08-01 r5155"
    /// </summary>
    public static string ParseVersion(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return string.Empty;

        var lines = output.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("smartctl", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("r", StringComparison.OrdinalIgnoreCase))
            {
                // Extraer versión: "smartctl 7.4 2023-08-01 r5155"
                var parts = line.Trim().Split(' ');
                if (parts.Length >= 2)
                {
                    return $"smartctl {parts[1]}";
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Parsea la salida JSON de smartctl --scan-open -j o smartctl --scan -j.
    /// </summary>
    public static IReadOnlyList<SmartDiskDevice> ParseScanJson(string json)
    {
        var devices = new List<SmartDiskDevice>();

        if (string.IsNullOrWhiteSpace(json))
            return devices;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // smartctl --scan retorna un array en la raíz
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in root.EnumerateArray())
                {
                    var device = ParseDeviceFromJson(element);
                    if (device != null)
                        devices.Add(device);
                }
            }
            // O puede ser un objeto con propiedad "devices"
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("devices", out var devicesArray))
            {
                foreach (var element in devicesArray.EnumerateArray())
                {
                    var device = ParseDeviceFromJson(element);
                    if (device != null)
                        devices.Add(device);
                }
            }
        }
        catch (JsonException)
        {
            // JSON inválido, retornar lista vacía
        }

        return devices;
    }

    /// <summary>
    /// Parsea la salida de texto de smartctl --scan (sin -j).
    /// Formato típico:
    /// /dev/sda -d scsi # /dev/sda
    /// /dev/nvme0 -d nvme # /dev/nvme0
    /// </summary>
    public static IReadOnlyList<SmartDiskDevice> ParseScanText(string text)
    {
        var devices = new List<SmartDiskDevice>();

        if (string.IsNullOrWhiteSpace(text))
            return devices;

        var lines = text.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Saltar líneas vacías o comentarios
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            // Buscar patrón: /dev/sdX -d type # info
            var parts = line.Split('#', 2);
            if (parts.Length < 1) continue;

            var devicePart = parts[0].Trim();
            var infoPart = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            // Extraer nombre del dispositivo y tipo
            var deviceParts = devicePart.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (deviceParts.Length < 1) continue;

            var deviceName = deviceParts[0];
            var deviceType = deviceParts.Length > 2 ? deviceParts[2] : "scsi";

            var device = new SmartDiskDevice
            {
                Name = deviceName,
                InfoName = infoPart,
                Type = deviceType,
                IsAvailable = true,
                ApproximateDiskType = DetectDiskTypeFromProtocol(deviceType)
            };

            devices.Add(device);
        }

        return devices;
    }

    /// <summary>
    /// Parsea un elemento JSON individual de dispositivo.
    /// </summary>
    private static SmartDiskDevice? ParseDeviceFromJson(JsonElement element)
    {
        try
        {
            var device = new SmartDiskDevice
            {
                Name = GetStringProperty(element, "name"),
                InfoName = GetStringProperty(element, "info_name"),
                Type = GetStringProperty(element, "type"),
                Protocol = GetStringProperty(element, "protocol"),
                ModelName = GetStringProperty(element, "model_name"),
                SerialNumber = GetStringProperty(element, "serial_number"),
                IsAvailable = true
            };

            device.ApproximateDiskType = DetectDiskType(device);

            return device;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Detecta el tipo de disco basándose en la información del dispositivo.
    /// </summary>
    private static string DetectDiskType(SmartDiskDevice device)
    {
        // Por protocolo
        if (device.Protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
            return "NVMe";

        if (device.Protocol.Contains("USB", StringComparison.OrdinalIgnoreCase))
            return "USB";

        // Por tipo
        if (device.Type.Contains("nvme", StringComparison.OrdinalIgnoreCase))
            return "NVMe";

        // Por modelo (heurística)
        var model = device.ModelName.ToLowerInvariant();
        if (model.Contains("ssd") || model.Contains("nvme"))
            return "SSD";

        if (model.Contains("hdd") || model.Contains("barracuda") ||
            model.Contains("wd blue") || model.Contains("seagate"))
            return "HDD";

        // Por defecto, asumir SATA si no se puede determinar
        return "SATA";
    }

    /// <summary>
    /// Detecta tipo de disco desde protocolo en salida de texto.
    /// </summary>
    private static string DetectDiskTypeFromProtocol(string protocol)
    {
        if (protocol.Contains("nvme", StringComparison.OrdinalIgnoreCase))
            return "NVMe";
        if (protocol.Contains("usb", StringComparison.OrdinalIgnoreCase))
            return "USB";
        return "SATA";
    }

    /// <summary>
    /// Obtiene una propiedad string de un elemento JSON.
    /// </summary>
    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}
