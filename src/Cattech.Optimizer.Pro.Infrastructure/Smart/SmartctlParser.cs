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
                report.NvmePercentageUsed = percentUsed.GetInt32();

                // Guardar como advertencia si es alto
                if (report.NvmePercentageUsed >= 80)
                {
                    report.Warnings.Add($"NVMe vida útil usada: {report.NvmePercentageUsed}%");
                }
            }

            if (nvme.TryGetProperty("available_spare", out var spare))
            {
                report.NvmeAvailableSpare = spare.GetInt32();
            }

            if (nvme.TryGetProperty("available_spare_threshold", out var spareThreshold))
            {
                var spareVal = report.NvmeAvailableSpare ?? -1;
                var threshVal = spareThreshold.GetInt32();
                if (threshVal > 0 && spareVal >= 0 && spareVal <= threshVal)
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

            if (nvme.TryGetProperty("media_errors", out var mediaErrors))
            {
                report.NvmeMediaErrors = mediaErrors.GetInt64();
                if (report.NvmeMediaErrors > 0)
                {
                    report.Errors.Add($"NVMe media errors: {report.NvmeMediaErrors}");
                    report.RequiresBackupRecommendation = true;
                }
            }

            if (nvme.TryGetProperty("unsafe_shutdowns", out var unsafeShutdowns))
            {
                report.NvmeUnsafeShutdowns = unsafeShutdowns.GetInt64();
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

    // =====================
    // Self-Test Parsing Methods
    // =====================

    /// <summary>
    /// Parsea la respuesta de smartctl -t short -j para determinar si el test se inició.
    /// smartctl retorna JSON con mensajes de ejecución.
    /// </summary>
    public static (bool Started, string Message, int? EstimatedMinutes) ParseStartShortTestJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (false, "Respuesta vacía de smartctl", null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verificar si hay mensaje de ejecución
            if (root.TryGetProperty("smartctl", out var smartctl) &&
                smartctl.TryGetProperty("messages", out var messages))
            {
                foreach (var message in messages.EnumerateArray())
                {
                    if (message.TryGetProperty("string", out var str))
                    {
                        var text = str.GetString() ?? string.Empty;

                        // Si dice "Testing has begun" o "Test will complete", el test se inició
                        if (text.Contains("Testing has begun", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("Test will complete", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("test has started", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, text, ExtractEstimatedMinutes(text));
                        }

                        // Mensaje de error
                        if (text.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("unsupported", StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, text, null);
                        }
                    }
                }
            }

            // Verificar exit status para detectar soporte
            if (root.TryGetProperty("smartctl", out var smartctl2) &&
                smartctl2.TryGetProperty("exit_status", out var exitStatus) &&
                exitStatus.TryGetProperty("value", out var exitValue))
            {
                // Exit status 0 o 1 = ejecutado; otros = error/soporte
                var exit = exitValue.GetInt32();
                if (exit != 0 && exit != 1)
                {
                    return (false, $"smartctl exit status: {exit}", null);
                }
            }

            return (false, "No se pudo confirmar el inicio del test", null);
        }
        catch (JsonException)
        {
            return (false, "JSON inválido", null);
        }
    }

    /// <summary>
    /// Parsea el self-test log JSON (smartctl -l selftest -j) para obtener estado/progreso.
    /// </summary>
    public static SmartTestSession ParseSelfTestLogJson(string json, SmartTestSession session)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            session.Status = SmartTestStatus.Unknown;
            session.ResultMessage = "Respuesta vacía de smartctl";
            session.LastCheckedAt = DateTime.Now;
            return session;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Buscar self-test log
            if (root.TryGetProperty("ata_smart_selective_self_test_log", out var selective))
            {
                ParseSelectiveLog(selective, session);
            }
            else if (root.TryGetProperty("ata_smart_self_test_log", out var selfTestLog) &&
                     selfTestLog.TryGetProperty("standard", out var standard) &&
                     standard.TryGetProperty("table", out var table) &&
                     table.ValueKind == JsonValueKind.Array && table.GetArrayLength() > 0)
            {
                ParseSelfTestTable(table, session);
            }
            // NVMe
            else if (root.TryGetProperty("nvme_self_test_list", out var nvmeList) &&
                     nvmeList.TryGetProperty("entries", out var entries) &&
                     entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
            {
                ParseNvmeSelfTest(entries, session);
            }
            else
            {
                session.Status = SmartTestStatus.Unknown;
                session.ResultMessage = "No se encontró log de self-test";
                session.LastCheckedAt = DateTime.Now;
            }
        }
        catch (JsonException)
        {
            session.Status = SmartTestStatus.Unknown;
            session.ResultMessage = "JSON inválido al consultar estado";
            session.LastCheckedAt = DateTime.Now;
        }

        return session;
    }

    private static void ParseSelectiveLog(JsonElement selective, SmartTestSession session)
    {
        if (selective.TryGetProperty("current", out var current) &&
            current.TryGetProperty("status", out var status))
        {
            var statusText = status.GetString() ?? string.Empty;
            session.Status = MapStatusText(statusText);
            session.ResultMessage = statusText;
            session.LastCheckedAt = DateTime.Now;
        }
        else
        {
            session.Status = SmartTestStatus.Unknown;
            session.ResultMessage = "No se pudo leer el estado del self-test";
            session.LastCheckedAt = DateTime.Now;
        }
    }

    private static void ParseSelfTestTable(JsonElement table, SmartTestSession session)
    {
        // El primer elemento del table es el test más reciente
        var latest = table.EnumerateArray().First();

        if (latest.TryGetProperty("status", out var status))
        {
            // smartctl JSON: status puede ser string o objeto { "string": "..." }
            string statusText;
            if (status.ValueKind == JsonValueKind.String)
                statusText = status.GetString() ?? string.Empty;
            else if (status.ValueKind == JsonValueKind.Object &&
                     status.TryGetProperty("string", out var statusString))
                statusText = statusString.GetString() ?? string.Empty;
            else
                statusText = string.Empty;

            session.Status = MapStatusText(statusText);
            session.ResultMessage = statusText;
        }

        if (latest.TryGetProperty("remaining", out var remaining))
        {
            // Puede ser string ("60%") o número (60)
            string remainingText;
            if (remaining.ValueKind == JsonValueKind.String)
                remainingText = remaining.GetString() ?? string.Empty;
            else if (remaining.ValueKind == JsonValueKind.Number)
                remainingText = remaining.GetRawText();
            else
                remainingText = string.Empty;

            if (int.TryParse(remainingText.TrimEnd('%'), out var remainingPercent))
            {
                session.ProgressPercent = Math.Clamp(100 - remainingPercent, 0, 100);
            }
        }

        if (latest.TryGetProperty("lifetime_hours", out var lifetime))
        {
            // No aplica a la sesión directamente
        }

        session.LastCheckedAt = DateTime.Now;

        // Si el test está en progreso, marcar InProgress
        if (session.Status == SmartTestStatus.Unknown &&
            session.ProgressPercent is > 0 and < 100)
        {
            session.Status = SmartTestStatus.InProgress;
        }
    }

    private static void ParseNvmeSelfTest(JsonElement entries, SmartTestSession session)
    {
        var latest = entries.EnumerateArray().First();

        if (latest.TryGetProperty("result", out var result))
        {
            var resultValue = result.GetInt32();
            session.Status = resultValue switch
            {
                0 => SmartTestStatus.CompletedWithoutError,
                1 => SmartTestStatus.CompletedWithError,
                2 => SmartTestStatus.Aborted,
                _ => SmartTestStatus.Unknown
            };
            session.ResultMessage = $"NVMe self-test result: {resultValue}";
        }

        session.LastCheckedAt = DateTime.Now;
    }

    /// <summary>
    /// Mapea texto de estado de smartctl a SmartTestStatus.
    /// </summary>
    public static SmartTestStatus MapStatusText(string statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
            return SmartTestStatus.Unknown;

        var lower = statusText.ToLowerInvariant();

        if (lower.Contains("completed without error") ||
            lower.Contains("completed, no errors") ||
            lower.Contains("no errors"))
            return SmartTestStatus.CompletedWithoutError;

        // "Completed with error", "Completed: read failure", "failed", "failure"
        if ((lower.StartsWith("completed") && (lower.Contains("error") || lower.Contains("failure"))) ||
            lower.Contains("failed"))
            return SmartTestStatus.CompletedWithError;

        if (lower.Contains("aborted"))
            return SmartTestStatus.Aborted;

        if (lower.Contains("interrupted"))
            return SmartTestStatus.Interrupted;

        if (lower.Contains("in progress") || lower.Contains("remaining"))
            return SmartTestStatus.InProgress;

        if (lower.Contains("unsupported") || lower.Contains("not supported"))
            return SmartTestStatus.Unsupported;

        if (lower.Contains("starting"))
            return SmartTestStatus.Starting;

        return SmartTestStatus.Unknown;
    }

    /// <summary>
    /// Extrae los minutos estimados de un mensaje como "Test will complete in 2 minutes".
    /// Retorna null si no puede determinarse.
    /// </summary>
    public static int? ExtractEstimatedMinutes(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        // Buscar patrón "in N minutes"
        var match = System.Text.RegularExpressions.Regex.Match(
            message, @"in\s+(\d+)\s+minutes?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var minutes))
            return minutes;

        // Buscar patrón "N minutes"
        match = System.Text.RegularExpressions.Regex.Match(
            message, @"(\d+)\s+minutes?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out minutes))
            return minutes;

        return null;
    }

    /// <summary>
    /// Convierte SmartTestStatus a mensaje legible en español.
    /// </summary>
    public static string StatusToMessage(SmartTestStatus status) => status switch
    {
        SmartTestStatus.NotStarted => "Test no iniciado",
        SmartTestStatus.Starting => "Test iniciándose",
        SmartTestStatus.InProgress => "Test en ejecución",
        SmartTestStatus.CompletedWithoutError => "Prueba completada sin errores reportados.",
        SmartTestStatus.CompletedWithError => "La prueba detectó errores. Revisar SMART y realizar backup.",
        SmartTestStatus.Aborted => "Prueba abortada.",
        SmartTestStatus.Interrupted => "Prueba interrumpida.",
        SmartTestStatus.Unsupported => "El dispositivo no soporta esta prueba.",
        SmartTestStatus.FailedToStart => "No se pudo iniciar la prueba.",
        SmartTestStatus.Unknown => "No se pudo determinar el resultado.",
        _ => "Estado desconocido"
    };
}
