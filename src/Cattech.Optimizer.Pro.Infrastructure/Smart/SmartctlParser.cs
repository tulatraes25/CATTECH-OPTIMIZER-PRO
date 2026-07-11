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

    // =====================
    // SMART Analysis Methods
    // =====================

    /// <summary>
    /// Parsea la salida JSON completa de smartctl -a -j para un disco.
    /// </summary>
    public static SmartDiskReport ParseSmartJson(string json, SmartDiskDevice device, string smartctlVersion)
    {
        var report = new SmartDiskReport
        {
            Device = device.Name,
            DeviceName = device.InfoName,
            DeviceType = device.ApproximateDiskType,
            Protocol = device.Protocol,
            ModelName = device.ModelName,
            SerialNumber = device.SerialNumber,
            SmartctlVersion = smartctlVersion,
            IsAnalysisSuccessful = false
        };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Extraer información del dispositivo
            ExtractDeviceInfo(root, report);

            // Extraer estado de salud
            ExtractHealthStatus(root, report);

            // Extraer atributos importantes
            ExtractImportantAttributes(root, report);

            // Extraer temperatura
            ExtractTemperature(root, report);

            // Extraer contadores
            ExtractCounters(root, report);

            // Calcular estado general
            CalculateOverallHealth(report);

            report.IsAnalysisSuccessful = true;
        }
        catch (JsonException ex)
        {
            report.Errors.Add($"Error al parsear JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            report.Errors.Add($"Error inesperado: {ex.Message}");
        }

        return report;
    }

    /// <summary>
    /// Extrae información del dispositivo desde el JSON.
    /// </summary>
    private static void ExtractDeviceInfo(JsonElement root, SmartDiskReport report)
    {
        report.FirmwareVersion = GetStringProperty(root, "firmware_version");

        // Capacidad desde smart_status o size
        if (root.TryGetProperty("user_capacity", out var capacity) &&
            capacity.TryGetProperty("bytes", out var bytes))
        {
            report.CapacityBytes = bytes.GetInt64();
        }
    }

    /// <summary>
    /// Extrae el estado de salud general.
    /// </summary>
    private static void ExtractHealthStatus(JsonElement root, SmartDiskReport report)
    {
        if (root.TryGetProperty("smart_status", out var status))
        {
            if (status.TryGetProperty("passed", out var passed))
            {
                report.OverallHealthPassed = passed.GetBoolean();
            }
        }

        // Verificar si SMART está habilitado
        if (root.TryGetProperty("smart_status", out var smartStatus))
        {
            if (smartStatus.TryGetProperty("passed", out var passed) && !passed.GetBoolean())
            {
                report.HealthStatus = SmartHealthStatus.Critical;
                report.HealthSummary = "Self-assessment de salud FAILED";
                report.RequiresBackupRecommendation = true;
            }
        }
    }

    /// <summary>
    /// Extrae atributos SMART importantes.
    /// </summary>
    private static void ExtractImportantAttributes(JsonElement root, SmartDiskReport report)
    {
        if (!root.TryGetProperty("ata_smart_attributes", out var ataAttributes))
            return;

        if (!ataAttributes.TryGetProperty("table", out var table))
            return;

        // IDs importantes para ATA/SATA
        var importantIds = new Dictionary<int, string>
        {
            { 1, "Raw_Read_Error_Rate" },
            { 3, "Spin_Up_Time" },
            { 5, "Reallocated_Sector_Ct" },
            { 9, "Power_On_Hours" },
            { 12, "Power_Cycle_Count" },
            { 187, "Reported_Uncorrect" },
            { 188, "Command_Timeout" },
            { 197, "Current_Pending_Sector" },
            { 198, "Offline_Uncorrectable" },
            { 199, "UDMA_CRC_Error_Count" }
        };

        // Atributos SSD
        var ssdAttributes = new Dictionary<int, string>
        {
            { 177, "Wear_Leveling_Count" },
            { 175, "Program_Fail_Count_Chip" },
            { 176, "Erase_Fail_Count_Chip" },
            { 173, "Wear_Leveling_Count" },
            { 231, "SSD_Life_Left" },
            { 233, "Media_Wearout_Indicator" }
        };

        foreach (var attr in table.EnumerateArray())
        {
            var attrId = attr.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;

            // Solo incluir atributos importantes
            if (!importantIds.ContainsKey(attrId) && !ssdAttributes.ContainsKey(attrId))
                continue;

            var attribute = new SmartAttribute
            {
                Id = attrId,
                Name = GetStringProperty(attr, "name"),
                Value = attr.TryGetProperty("value", out var val) ? val.GetInt32() : 0,
                Worst = attr.TryGetProperty("worst", out var worst) ? worst.GetInt32() : 0,
                Threshold = attr.TryGetProperty("thresh", out var thresh) ? thresh.GetInt32() : 0,
                WhenFailed = GetStringProperty(attr, "when_failed"),
                Flags = GetFlagsString(attr)
            };

            // Extraer raw value
            if (attr.TryGetProperty("raw", out var raw) && raw.TryGetProperty("value", out var rawVal))
            {
                attribute.RawValue = rawVal.GetInt64();
            }

            // Descripción y severidad
            attribute.Description = GetAttributeDescription(attrId);
            attribute.Severity = CalculateAttributeSeverity(attribute);

            report.ImportantAttributes.Add(attribute);
        }

        // Extraer temperatura si no se obtuvo antes
        if (report.TemperatureCelsius == 0)
        {
            ExtractTemperatureFromAttributes(table, report);
        }
    }

    /// <summary>
    /// Extrae la temperatura del JSON.
    /// </summary>
    private static void ExtractTemperature(JsonElement root, SmartDiskReport report)
    {
        if (root.TryGetProperty("temperature", out var temp))
        {
            if (temp.TryGetProperty("current", out var current))
            {
                report.TemperatureCelsius = current.GetInt32();
            }
        }
    }

    /// <summary>
    /// Extrae temperatura desde atributos ATA.
    /// </summary>
    private static void ExtractTemperatureFromAttributes(JsonElement table, SmartDiskReport report)
    {
        foreach (var attr in table.EnumerateArray())
        {
            var id = attr.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            if (id == 194) // Temperature_Celsius
            {
                if (attr.TryGetProperty("raw", out var raw) && raw.TryGetProperty("value", out var val))
                {
                    report.TemperatureCelsius = val.GetInt32();
                }
                break;
            }
        }
    }

    /// <summary>
    /// Extrae contadores (horas, ciclos).
    /// </summary>
    private static void ExtractCounters(JsonElement root, SmartDiskReport report)
    {
        if (root.TryGetProperty("power_on_time", out var pot) && pot.TryGetProperty("hours", out var hours))
        {
            report.PowerOnHours = hours.GetInt64();
        }

        if (root.TryGetProperty("power_cycle_count", out var pcc) && pcc.TryGetProperty("value", out var val))
        {
            report.PowerCycleCount = val.GetInt64();
        }

        // Para NVMe: percentage_used
        if (root.TryGetProperty("nvme_smart_health_information", out var nvme))
        {
            if (nvme.TryGetProperty("percentage_used", out var percentUsed))
            {
                // Guardar como advertencia si es alto
                if (percentUsed.GetInt32() >= 80)
                {
                    report.Warnings.Add($"NVMe vida útil usada: {percentUsed.GetInt32()}%");
                }
            }

            if (nvme.TryGetProperty("available_spare", out var spare) &&
                nvme.TryGetProperty("available_spare_threshold", out var threshold))
            {
                var spareVal = spare.GetInt32();
                var threshVal = threshold.GetInt32();
                if (threshVal > 0 && spareVal <= threshVal)
                {
                    report.Warnings.Add($"NVMe espacio de repuesto bajo: {spareVal}% (umbral: {threshVal}%)");
                }
            }

            if (nvme.TryGetProperty("critical_warning", out var critical))
            {
                var warning = critical.GetString();
                if (!string.IsNullOrEmpty(warning) && warning != "0")
                {
                    report.Errors.Add($"NVMe critical_warning: {warning}");
                    report.RequiresBackupRecommendation = true;
                }
            }

            if (nvme.TryGetProperty("media_errors", out var mediaErrors) && mediaErrors.GetInt64() > 0)
            {
                report.Errors.Add($"NVMe media errors: {mediaErrors.GetInt64()}");
                report.RequiresBackupRecommendation = true;
            }
        }
    }

    /// <summary>
    /// Calcula el estado de salud general basado en atributos y advertencias.
    /// </summary>
    private static void CalculateOverallHealth(SmartDiskReport report)
    {
        // Si ya se marcó como crítico, mantener
        if (report.HealthStatus == SmartHealthStatus.Critical)
        {
            report.HealthSummary = "CRÍTICO: Backup inmediato recomendado";
            return;
        }

        // Si overall-health failed
        if (!report.OverallHealthPassed)
        {
            report.HealthStatus = SmartHealthStatus.Critical;
            report.HealthSummary = "Self-assessment de salud FAILED. Backup inmediato recomendado.";
            report.RequiresBackupRecommendation = true;
            return;
        }

        // Verificar atributos críticos
        foreach (var attr in report.ImportantAttributes)
        {
            if (attr.Severity == SmartSeverity.Critical)
            {
                report.HealthStatus = SmartHealthStatus.Critical;
                report.HealthSummary = $"Atributo crítico: {attr.Name} (ID {attr.Id})";
                report.RequiresBackupRecommendation = true;
                return;
            }
        }

        // Verificar errores
        if (report.Errors.Count > 0)
        {
            report.HealthStatus = SmartHealthStatus.Critical;
            report.HealthSummary = $"Errores detectados: {string.Join(", ", report.Errors)}";
            report.RequiresBackupRecommendation = true;
            return;
        }

        // Verificar advertencias
        if (report.Warnings.Count > 0)
        {
            report.HealthStatus = SmartHealthStatus.Warning;
            report.HealthSummary = $"Advertencias: {string.Join(", ", report.Warnings)}";
            return;
        }

        // Verificar atributos con warning
        foreach (var attr in report.ImportantAttributes)
        {
            if (attr.Severity == SmartSeverity.Warning)
            {
                report.HealthStatus = SmartHealthStatus.Warning;
                report.HealthSummary = $"Atributo a revisar: {attr.Name} (ID {attr.Id})";
                return;
            }
        }

        // Todo bien
        report.HealthStatus = SmartHealthStatus.Good;
        report.HealthSummary = "Salud general: Buena. Sin atributos críticos ni advertencias.";
    }

    /// <summary>
    /// Calcula la severidad de un atributo SMART.
    /// </summary>
    private static SmartSeverity CalculateAttributeSeverity(SmartAttribute attribute)
    {
        // Si el valor crudo supera el umbral, es crítico
        if (attribute.Threshold > 0 && attribute.RawValue > attribute.Threshold)
            return SmartSeverity.Critical;

        // Reglas específicas por ID
        return attribute.Id switch
        {
            // Sectores reasignados
            5 when attribute.RawValue > 0 => SmartSeverity.Warning,
            5 when attribute.RawValue > 10 => SmartSeverity.Critical,

            // Sectores pendientes
            197 when attribute.RawValue > 0 => SmartSeverity.Warning,
            197 when attribute.RawValue > 5 => SmartSeverity.Critical,

            // Offline uncorrectable
            198 when attribute.RawValue > 0 => SmartSeverity.Warning,
            198 when attribute.RawValue > 5 => SmartSeverity.Critical,

            // UDMA CRC errors
            199 when attribute.RawValue > 0 => SmartSeverity.Warning,
            199 when attribute.RawValue > 100 => SmartSeverity.Critical,

            // Temperatura (ID 194)
            194 when attribute.RawValue > 55 => SmartSeverity.Warning,
            194 when attribute.RawValue > 65 => SmartSeverity.Critical,

            // Wear leveling / SSD life
            231 when attribute.RawValue <= 10 => SmartSeverity.Warning,
            231 when attribute.RawValue <= 5 => SmartSeverity.Critical,

            // Media wearout
            233 when attribute.RawValue >= 90 => SmartSeverity.Warning,
            233 when attribute.RawValue >= 98 => SmartSeverity.Critical,

            _ => SmartSeverity.Info
        };
    }

    /// <summary>
    /// Obtiene la descripción de un atributo por su ID.
    /// </summary>
    private static string GetAttributeDescription(int id) => id switch
    {
        1 => "Tasa de errores de lectura cruda",
        3 => "Tiempo de arranque del disco",
        5 => "Sectores reasignados",
        9 => "Horas encendido",
        12 => "Ciclos de encendido/apagado",
        187 => "Errores no corregibles reportados",
        188 => "Timeouts de comandos",
        194 => "Temperatura actual",
        197 => "Sectores pendientes",
        198 => "Sectores offline no corregibles",
        199 => "Errores CRC UDMA",
        231 => "Vida útil SSD restante",
        233 => "Indicador de desgaste de medios",
        _ => "Atributo desconocido"
    };

    /// <summary>
    /// Obtiene el string de flags de un atributo.
    /// </summary>
    private static string GetFlagsString(JsonElement attr)
    {
        if (attr.TryGetProperty("flags", out var flags) && flags.TryGetProperty("string", out var str))
        {
            return str.GetString() ?? string.Empty;
        }
        return string.Empty;
    }
}
