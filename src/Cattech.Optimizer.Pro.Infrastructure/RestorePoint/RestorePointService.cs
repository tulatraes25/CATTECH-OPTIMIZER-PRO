using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Infrastructure.RestorePoint;

/// <summary>
/// Resultado crudo de ejecutar un comando de PowerShell para puntos de restauración.
/// </summary>
internal sealed class RestorePointCommandResult
{
    /// <summary>Código de salida del proceso (-1 si hubo timeout).</summary>
    public int ExitCode { get; init; }

    /// <summary>Salida estándar completa.</summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>Salida de error completa.</summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>Si el comando superó el tiempo máximo de espera y fue terminado.</summary>
    public bool TimedOut { get; init; }
}

/// <summary>
/// Ejecutor de comandos de PowerShell usado por <see cref="RestorePointService"/>.
/// Se abstrae para poder probar el mapeo de estados y mensajes con un fake,
/// sin ejecutar operaciones reales sobre el sistema.
/// </summary>
internal interface IRestorePointCommandRunner
{
    Task<RestorePointCommandResult> RunAsync(
        string script,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Ejecutor real: lanza powershell.exe, captura salida y aplica timeout.
/// </summary>
internal sealed class PowerShellRestorePointCommandRunner : IRestorePointCommandRunner
{
    public async Task<RestorePointCommandResult> RunAsync(
        string script,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-Command");
        // Forzar UTF-8 para que los mensajes localizados se puedan clasificar.
        process.StartInfo.ArgumentList.Add("[Console]::OutputEncoding = [Text.Encoding]::UTF8; " + script);

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        var timedOut = false;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string standardOutput;
        string standardError;
        try
        {
            standardOutput = await outputTask;
        }
        catch (Exception)
        {
            standardOutput = string.Empty;
        }

        try
        {
            standardError = await errorTask;
        }
        catch (Exception)
        {
            standardError = string.Empty;
        }

        return new RestorePointCommandResult
        {
            ExitCode = timedOut || !process.HasExited ? -1 : process.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError,
            TimedOut = timedOut
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // El proceso ya terminó o no se pudo terminar: no es un error accionable.
        }
    }
}

/// <summary>
/// Clasificación de un fallo de PowerShell en un código y un mensaje para la UI.
/// </summary>
internal sealed record RestorePointFailure(string ErrorCode, string ErrorMessage);

/// <summary>
/// Mapea la salida de Checkpoint-Computer a los códigos de error conocidos.
/// Es una función pura para poder probarla con mensajes reales de Windows sin tocar el sistema.
/// </summary>
internal static class RestorePointErrorMapper
{
    internal static RestorePointFailure Classify(
        string? standardError, string? standardOutput, int exitCode)
    {
        var raw = string.Join(
            "\n",
            new[] { standardError, standardOutput }.Where(value => !string.IsNullOrWhiteSpace(value)));

        // Se normaliza (minúsculas y sin diacríticos) porque los mensajes de Windows
        // dependen del idioma instalado: "protección" y "proteccion" deben clasificar igual.
        var text = NormalizeForMatching(raw);

        if (HasAny(
                text,
                "frequency", "24 hours", "24 horas", "ultimas 24", "ultimos 24",
                "14 days", "14 dias", "already been created", "ya se ha creado",
                "ha creado uno recientemente", "intervalo de frecuencia",
                "restore point cannot be created"))
        {
            return new RestorePointFailure(
                RestorePointErrorCodes.FrequencyLimit,
                "Windows limitó la creación del punto de restauración. Por defecto permite un solo punto " +
                "cada 24 horas (14 días en versiones antiguas). Volvé a intentarlo más tarde o ajustá la " +
                "política SystemRestorePointCreationFrequency si el técnico lo considera necesario.");
        }

        if (HasAny(
                text,
                "access is denied", "access denied", "acceso denegado", "0x80070005",
                "requires elevation", "requires administrator", "run as administrator",
                "se requiere elevacion", "ejecutar como administrador", "no tiene privilegios"))
        {
            return new RestorePointFailure(
                RestorePointErrorCodes.AccessDenied,
                "Windows denegó el acceso al crear el punto de restauración. Iniciá la aplicación como " +
                "administrador y volvé a intentarlo.");
        }

        if (HasAny(
                text,
                "system restore is disabled", "system restore is not enabled",
                "system protection is disabled", "restore point creation is not enabled",
                "not enabled on this drive", "disabled on this drive",
                "proteccion del sistema esta deshabilitada", "proteccion del sistema no esta habilitada",
                "restauracion del sistema esta deshabilitada", "restauracion del sistema no esta habilitada",
                "restaurar sistema no esta habilitado", "no esta habilitada en esta unidad"))
        {
            return new RestorePointFailure(
                RestorePointErrorCodes.ProtectionDisabled,
                "Windows informó que la protección del sistema (Restaurar sistema) está deshabilitada o no " +
                "está habilitada para la unidad del sistema. Habilitá la protección de la unidad y volvé a " +
                "intentarlo: Windows no crea puntos sin protección activa.");
        }

        if (HasAny(
                text,
                "service cannot be started", "service is disabled", "0x80070422",
                "volume shadow copy", "vss", "rpc server is unavailable",
                "no se puede iniciar el servicio", "servicio de instantaneas",
                "servicio de instantaneas de volumen"))
        {
            return new RestorePointFailure(
                RestorePointErrorCodes.ServiceUnavailable,
                "El servicio de instantáneas (VSS/swprv) no está disponible o está deshabilitado. Revisá los " +
                "servicios 'Volume Shadow Copy' y 'Proveedor de instantáneas de software de Microsoft' " +
                "antes de reintentar.");
        }

        var detail = FirstLine(raw);
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"Windows no pudo crear el punto de restauración (código de salida {exitCode})."
            : $"Windows no pudo crear el punto de restauración (código de salida {exitCode}): {detail}";

        return new RestorePointFailure(RestorePointErrorCodes.PowerShellError, message);
    }

    private static string NormalizeForMatching(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static bool HasAny(string text, params string[] fragments)
        => fragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal));

    private static string FirstLine(string value)
    {
        var line = value
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(candidate => candidate.Trim())
            .FirstOrDefault(candidate => candidate.Length > 0);

        if (string.IsNullOrEmpty(line))
        {
            return string.Empty;
        }

        return line.Length <= 300 ? line : line[..300] + "...";
    }
}

/// <summary>
/// Scripts de PowerShell usados por el servicio. Se centralizan para poder inspeccionarlos en tests.
/// </summary>
internal static class RestorePointScripts
{
    /// <summary>
    /// Sonda real del sistema de restauración en Windows 10/11: estado de VSS/swprv,
    /// existencia de la clase WMI SystemRestore, puntos existentes y política DisableSR/DisableConfig.
    /// Ya no se consulta "srservice" (servicio inexistente en Windows 10/11).
    /// </summary>
    internal const string StatusProbe =
        "$ErrorActionPreference='SilentlyContinue';" +
        "$isAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent())." +
        "IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator);" +
        "$vss=Get-Service -Name 'VSS';" +
        "$swprv=Get-Service -Name 'swprv';" +
        "$srClass=Get-CimClass -Namespace 'root/default' -ClassName 'SystemRestore';" +
        "$points=@(Get-ComputerRestorePoint);" +
        "$current=Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore';" +
        "$policy=Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows NT\\SystemRestore';" +
        "[pscustomobject]@{" +
        "IsAdmin=[bool]$isAdmin;" +
        "VssFound=($null -ne $vss);" +
        "SwprvFound=($null -ne $swprv);" +
        "SystemRestoreClassFound=($null -ne $srClass);" +
        "RestorePointCount=[int]$points.Count;" +
        "DisableSrPolicy=[bool](($current.DisableSR -eq 1) -or ($current.DisableConfig -eq 1) -or " +
        "($policy.DisableSR -eq 1) -or ($policy.DisableConfig -eq 1))" +
        "} | ConvertTo-Json -Compress";

    /// <summary>
    /// Cuenta los puntos de restauración existentes. Best effort: si falla, el servicio no verifica.
    /// </summary>
    internal const string CountRestorePoints =
        "$ErrorActionPreference='SilentlyContinue';" +
        "$points=@(Get-ComputerRestorePoint);" +
        "[int]$points.Count";

    /// <summary>
    /// Comando real de creación. Escapa comillas simples para no permitir inyección de comandos
    /// a través del nombre del punto.
    /// </summary>
    internal static string BuildCheckpoint(string name)
        => "Checkpoint-Computer -Description '" + EscapeSingleQuoted(name) +
           "' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop";

    internal static string EscapeSingleQuoted(string value)
        => (value ?? string.Empty).Replace("'", "''");
}

/// <summary>
/// Implementación de IRestorePointService.
/// Crea puntos de restauración mediante Checkpoint-Computer (Windows 10/11) y clasifica
/// los errores reales que devuelve Windows. La protección del sistema se informa como
/// estado probable (advisory) y nunca se usa como puerta previa: el huevo y la gallina
/// de la versión anterior impedía crear el primer punto de una máquina limpia.
/// </summary>
public class RestorePointService : IRestorePointService
{
    private static readonly TimeSpan StatusProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RestorePointCountTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CreationTimeout = TimeSpan.FromMinutes(4);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions ProbeSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly IRestorePointCommandRunner _runner;
    private readonly string _baseDirectory;
    private readonly string _resultsDirectory;

    /// <summary>
    /// Crea el servicio con el ejecutor real de PowerShell.
    /// </summary>
    public RestorePointService()
        : this((string?)null)
    {
    }

    /// <summary>
    /// Crea el servicio con el ejecutor real de PowerShell y un directorio base para resultados.
    /// </summary>
    public RestorePointService(string? baseDirectory)
        : this(new PowerShellRestorePointCommandRunner(), baseDirectory)
    {
    }

    /// <summary>
    /// Crea el servicio con un ejecutor inyectable (usado por los tests con fakes).
    /// </summary>
    internal RestorePointService(IRestorePointCommandRunner runner, string? baseDirectory = null)
    {
        _runner = runner;
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _resultsDirectory = Path.Combine(_baseDirectory, "data", "restore-points");
    }

    /// <inheritdoc/>
    public async Task<RestorePointStatus> CheckStatusAsync()
    {
        var status = new RestorePointStatus
        {
            LastCheckedAt = DateTime.Now
        };

        RestoreSystemProbe? probe = null;
        try
        {
            probe = await ProbeSystemAsync();
        }
        catch (Exception)
        {
            probe = null;
        }

        // Si la sonda no se pudo ejecutar/parsear, se usa la detección nativa de elevación y
        // se deja la disponibilidad/protección en "desconocida": desconocido NO bloquea.
        status.IsAdministrator = probe?.IsAdmin ?? TryDetectAdministrator();

        if (probe is null)
        {
            status.AvailabilityState = RestoreAvailabilityState.Unknown;
            status.ProtectionState = RestoreProtectionState.Unknown;
            status.Warnings.Add(
                "No se pudo consultar el estado real de Restaurar sistema (VSS/WMI). " +
                "No se bloqueará el intento de creación.");
        }
        else
        {
            status.AvailabilityState =
                probe.VssFound || probe.SwprvFound || probe.SystemRestoreClassFound
                    ? RestoreAvailabilityState.LikelyAvailable
                    : RestoreAvailabilityState.NotAvailable;

            status.ProtectionState = probe.DisableSrPolicy
                ? RestoreProtectionState.LikelyDisabled
                : probe.RestorePointCount > 0
                    ? RestoreProtectionState.LikelyEnabled
                    : RestoreProtectionState.Unknown;
        }

        status.IsSystemRestoreAvailable =
            status.AvailabilityState == RestoreAvailabilityState.LikelyAvailable;
        status.IsProtectionEnabled =
            status.ProtectionState == RestoreProtectionState.LikelyEnabled;

        if (!status.IsAdministrator)
        {
            status.Warnings.Add(
                "La aplicación no está ejecutando como administrador: Windows rechazará Checkpoint-Computer.");
        }

        switch (status.AvailabilityState)
        {
            case RestoreAvailabilityState.NotAvailable:
                status.Warnings.Add(
                    "No se detectaron los servicios VSS/swprv ni la clase WMI SystemRestore " +
                    "(root/default). Es posible que esta edición de Windows no tenga Restaurar sistema.");
                break;
            case RestoreAvailabilityState.Unknown:
                status.Warnings.Add(
                    "No se pudo detectar el servicio de Restaurar sistema (VSS/swprv), " +
                    "pero no se bloqueará el intento.");
                break;
        }

        switch (status.ProtectionState)
        {
            case RestoreProtectionState.LikelyDisabled:
                status.Warnings.Add(
                    "La protección del sistema parece deshabilitada (política DisableSR/DisableConfig). " +
                    "Es solo informativo: se intentará crear el punto y Windows devolverá el error real si corresponde.");
                break;
            case RestoreProtectionState.Unknown:
                status.Warnings.Add(
                    "No se pudo confirmar la protección del sistema. No bloquea el intento: " +
                    "Windows informará el resultado real al crear el punto.");
                break;
        }

        if (status.CanAttemptCreate)
        {
            status.StatusMessage = status.ProtectionState == RestoreProtectionState.LikelyEnabled
                ? "Sistema listo para crear puntos de restauración."
                : "Se puede intentar crear un punto de restauración. La protección del sistema es informativa " +
                  "y Windows confirmará el resultado real.";
        }
        else
        {
            status.StatusMessage = $"No se puede intentar crear un punto: {status.AttemptBlockedReason}.";
        }

        return status;
    }

    /// <inheritdoc/>
    public async Task<RestorePointResult> CreateRestorePointAsync(string name)
    {
        var result = new RestorePointResult
        {
            RequestedAt = DateTime.Now,
            RestorePointName = name?.Trim() ?? string.Empty,
            TechnicianName = await GetTechnicianNameAsync()
        };

        try
        {
            if (string.IsNullOrWhiteSpace(result.RestorePointName))
            {
                result.Success = false;
                result.ErrorCode = RestorePointErrorCodes.InvalidName;
                result.ErrorMessage = "El nombre del punto de restauración no puede estar vacío.";
                result.FinishedAt = DateTime.Now;
                return result;
            }

            // No hay pre-consulta de protección: el único requisito previo es que Windows
            // pueda ejecutar Checkpoint-Computer. El resultado real se clasifica abajo.
            var previousCount = await TryGetRestorePointCountAsync();

            var creation = await _runner.RunAsync(
                RestorePointScripts.BuildCheckpoint(result.RestorePointName),
                CreationTimeout);

            result.MethodUsed = RestorePointMethod.PowerShellCheckpoint;
            result.StandardOutput = creation.StandardOutput;
            result.StandardError = creation.StandardError;

            if (creation.TimedOut)
            {
                result.Success = false;
                result.ErrorCode = RestorePointErrorCodes.Timeout;
                result.ErrorMessage =
                    "La creación del punto de restauración superó el tiempo máximo de espera. " +
                    "Verificá en 'Restaurar sistema' si el punto se creó antes de reintentar.";
            }
            else if (creation.ExitCode == 0)
            {
                result.Success = true;
                var currentCount = await TryGetRestorePointCountAsync();
                result.Verified = previousCount.HasValue && currentCount.HasValue &&
                                  currentCount.Value > previousCount.Value;
                result.ProtectionEnabled = true;

                if (result.Verified)
                {
                    result.Notes =
                        "Punto de restauración creado y verificado: se detectó un punto nuevo.";
                }
                else
                {
                    result.Notes =
                        "Checkpoint-Computer devolvió éxito. No se pudo verificar automáticamente el punto nuevo; " +
                        "confirmalo manualmente en 'Restaurar sistema'.";
                    result.Warnings.Add(
                        "Windows informó éxito, pero la verificación automática no detectó un punto nuevo. " +
                        "Confirmalo manualmente en 'Restaurar sistema'.");
                }
            }
            else
            {
                var failure = RestorePointErrorMapper.Classify(
                    creation.StandardError, creation.StandardOutput, creation.ExitCode);

                result.Success = false;
                result.ErrorCode = failure.ErrorCode;
                result.ErrorMessage = failure.ErrorMessage;
                result.RequiresAdministrator =
                    failure.ErrorCode == RestorePointErrorCodes.AccessDenied;
                result.ProtectionEnabled =
                    failure.ErrorCode == RestorePointErrorCodes.FrequencyLimit;
            }

            result.FinishedAt = DateTime.Now;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorCode = RestorePointErrorCodes.UnexpectedError;
            result.ErrorMessage =
                $"Error inesperado al crear el punto de restauración: {ex.Message}";
            result.FinishedAt = DateTime.Now;
            return result;
        }
    }

    /// <inheritdoc/>
    public string GenerateRestorePointName()
    {
        return $"CATTECH Optimizer Pro - Antes de mantenimiento - {DateTime.Now:yyyy-MM-dd HH:mm}";
    }

    /// <inheritdoc/>
    public async Task<string> SaveResultAsync(RestorePointResult result)
    {
        EnsureDirectoryExists(_resultsDirectory);

        var fileName = $"restore-point-result-{result.RequestedAt:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"restore-point-result-{result.RequestedAt:yyyyMMdd-HHmmss}-{result.Id}.json";
            filePath = Path.Combine(_resultsDirectory, fileName);
        }

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    /// <inheritdoc/>
    public async Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20)
    {
        var summaries = new List<RestorePointResultSummary>();

        if (!Directory.Exists(_resultsDirectory))
            return summaries;

        var files = Directory.GetFiles(_resultsDirectory, "restore-point-result-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var result = JsonSerializer.Deserialize<RestorePointResult>(json, SerializerOptions);

                if (result != null)
                {
                    summaries.Add(new RestorePointResultSummary
                    {
                        Id = result.Id,
                        RequestedAt = result.RequestedAt,
                        RestorePointName = result.RestorePointName,
                        Success = result.Success,
                        ErrorMessage = result.ErrorMessage,
                        FileName = Path.GetFileName(file)
                    });
                }
            }
            catch
            {
                // Un resultado corrupto no debe romper el historial.
            }
        }

        return summaries;
    }

    // --- Sondas internas ---

    private async Task<RestoreSystemProbe?> ProbeSystemAsync()
    {
        var run = await _runner.RunAsync(RestorePointScripts.StatusProbe, StatusProbeTimeout);

        if (run.TimedOut || run.ExitCode != 0)
        {
            return null;
        }

        var json = ExtractJsonObject(run.StandardOutput);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RestoreSystemProbe>(json, ProbeSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<int?> TryGetRestorePointCountAsync()
    {
        try
        {
            var run = await _runner.RunAsync(
                RestorePointScripts.CountRestorePoints, RestorePointCountTimeout);

            if (run.TimedOut || run.ExitCode != 0)
            {
                return null;
            }

            return int.TryParse(
                run.StandardOutput.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var count)
                ? count
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string output)
    {
        var trimmed = output.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private static bool TryDetectAdministrator()
    {
        try
        {
            return Environment.IsPrivilegedProcess;
        }
        catch (Exception)
        {
            // Si no se puede determinar, no se bloquea el intento.
            return true;
        }
    }

    private static async Task<string> GetTechnicianNameAsync()
    {
        return await Helpers.SettingsHelper.GetTechnicianNameAsync(new Data.JsonSettingsService());
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    /// <summary>
    /// DTO de la sonda del sistema de restauración.
    /// </summary>
    private sealed class RestoreSystemProbe
    {
        public bool IsAdmin { get; set; }
        public bool VssFound { get; set; }
        public bool SwprvFound { get; set; }
        public bool SystemRestoreClassFound { get; set; }
        public int RestorePointCount { get; set; }
        public bool DisableSrPolicy { get; set; }
    }
}
