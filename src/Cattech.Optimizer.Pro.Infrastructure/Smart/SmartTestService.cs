using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Infrastructure.Smart;

/// <summary>
/// Implementación de ISmartTestService.
/// Ejecuta self-tests SMART cortos de forma segura.
/// El test ocurre internamente en el firmware del disco.
/// </summary>
public class SmartTestService : ISmartTestService
{
    private readonly ISmartctlRunner _smartctlRunner;
    private readonly string _testsDirectory;
    private static readonly TimeSpan TestStartTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StatusCheckTimeout = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SmartTestService(ISmartctlRunner smartctlRunner, string? baseDirectory = null)
    {
        _smartctlRunner = smartctlRunner;
        _testsDirectory = Path.Combine(
            baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory,
            "data",
            "smart-tests");
    }

    /// <inheritdoc/>
    public Task<SmartTestSession> StartShortTestAsync(SmartDiskDevice device)
        => StartTestAsync(device, SmartTestType.Short);

    /// <inheritdoc/>
    public Task<SmartTestSession> StartExtendedTestAsync(SmartDiskDevice device)
        => StartTestAsync(device, SmartTestType.Extended);

    /// <summary>
    /// Lógica común para iniciar un self-test SMART.
    /// El test ocurre internamente en el firmware del disco.
    /// </summary>
    private async Task<SmartTestSession> StartTestAsync(SmartDiskDevice device, SmartTestType testType)
    {
        var session = new SmartTestSession
        {
            Device = device.Name,
            ModelName = device.ModelName,
            SerialNumber = device.SerialNumber,
            TestType = testType,
            Status = SmartTestStatus.Starting,
            RequestedAt = DateTime.Now,
            SmartctlDeviceType = device.Type
        };

        // Preservar el transporte -d TYPE detectado al iniciar el test
        var arguments = SmartctlCommandBuilder.BuildStartTestArguments(device.Name, device.Type,
            testType == SmartTestType.Extended);

        // Ejecutar smartctl -t short|long -j [-d TYPE] <device>
        var result = await _smartctlRunner.RunAsync(arguments, TestStartTimeout);

        // Asignar SIEMPRE el exit code real del intento (inclusive fallos).
        session.SmartctlExitCode = result.ExitCode;

        // Fallo operativo de invocación (timeout, proceso no ejecutado, bits 0-2):
        // no se inicia la sesión como InProgress.
        if (result.TimedOut || result.HasInvocationFailure || result.HasSmartCommandFailure)
        {
            session.Status = SmartTestStatus.FailedToStart;
            session.Errors.Add(string.IsNullOrWhiteSpace(result.StandardError)
                ? "Fallo operativo al iniciar el test"
                : result.StandardError);
            session.ResultMessage = SmartTestStatus.FailedToStart.ToDisplayMessage();
            await SaveSessionAsync(session);
            return session;
        }

        // Parsear respuesta JSON de forma estructurada.
        // Los bits 3-7 (hallazgos de salud/log) no impiden el inicio del test.
        var parseResult = SmartctlParser.ParseStartShortTestJson(result.StandardOutput);

        // Aplicar estado resultante
        session.Status = parseResult.Status;
        session.ResultMessage = parseResult.Message;
        session.Errors.AddRange(parseResult.Errors);
        session.Warnings.AddRange(parseResult.Warnings);

        switch (parseResult.Status)
        {
            case SmartTestStatus.InProgress:
                // Test iniciado correctamente
                session.StartedAt = DateTime.Now;
                session.EstimatedDurationMinutes = parseResult.EstimatedDurationMinutes;
                session.EstimatedCompletionAt = parseResult.EstimatedDurationMinutes.HasValue
                    ? DateTime.Now.AddMinutes(parseResult.EstimatedDurationMinutes.Value)
                    : null;
                session.ResultMessage = parseResult.Message;
                session.LastCheckedAt = DateTime.Now;
                break;

            case SmartTestStatus.Unsupported:
                session.ResultMessage = SmartTestStatus.Unsupported.ToDisplayMessage();
                break;

            default:
                session.ResultMessage = SmartTestStatus.FailedToStart.ToDisplayMessage();
                break;
        }

        await SaveSessionAsync(session);
        return session;
    }

    /// <inheritdoc/>
    public async Task<SmartTestSession> CheckStatusAsync(SmartTestSession session)
    {
        // Consultar self-test log (solo lectura), preservando el transporte de la sesión
        var arguments = SmartctlCommandBuilder.BuildSelfTestLogArguments(session.Device, session.SmartctlDeviceType);
        var result = await _smartctlRunner.RunAsync(arguments, StatusCheckTimeout);

        // Error temporal de consulta: solo fallos operativos (timeout, proceso no
        // ejecutado, bits 0-2). Los bits 3-7 NO bloquean el parseo del log.
        if (result.TimedOut || result.HasInvocationFailure || result.HasSmartCommandFailure)
        {
            // NO marcar el test como finalizado: puede seguir ejecutándose en el disco.
            session.LastCheckSucceeded = false;
            session.LastCheckError = result.StandardError ?? "Timeout al consultar estado";
            session.LastCheckedAt = DateTime.Now;

            // Conservar StartedAt, EstimatedCompletionAt y warnings previos (no se tocan)
            await SaveSessionAsync(session);
            return session;
        }

        // Parsear estado (inclusive con exit 128: el log puede contener errores)
        SmartctlParser.ParseSelfTestLogJson(result.StandardOutput, session);
        session.LastCheckedAt = DateTime.Now;
        session.LastCheckSucceeded = true;
        session.LastCheckError = string.Empty;

        // Si el test terminó, marcar fecha de finalización
        if (session.Status == SmartTestStatus.CompletedWithoutError ||
            session.Status == SmartTestStatus.CompletedWithError ||
            session.Status == SmartTestStatus.Aborted ||
            session.Status == SmartTestStatus.Interrupted)
        {
            session.CompletedAt = DateTime.Now;
            session.ResultMessage = session.Status.ToDisplayMessage();
        }

        await SaveSessionAsync(session);
        return session;
    }

    /// <inheritdoc/>
    public async Task<SmartTestResult?> GetLatestResultAsync(SmartDiskDevice device)
    {
        var arguments = SmartctlCommandBuilder.BuildSelfTestLogArguments(device.Name, device.Type);
        var result = await _smartctlRunner.RunAsync(arguments, StatusCheckTimeout);

        // Solo fallos operativos bloquean; bits 3-7 (ej: 128 con log) no.
        if (result.TimedOut || result.HasInvocationFailure || result.HasSmartCommandFailure ||
            string.IsNullOrWhiteSpace(result.StandardOutput))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(result.StandardOutput);
            var root = doc.RootElement;

            // Buscar tabla de self-test
            if (!root.TryGetProperty("ata_smart_self_test_log", out var log) ||
                !log.TryGetProperty("standard", out var standard) ||
                !standard.TryGetProperty("table", out var table) ||
                table.ValueKind != JsonValueKind.Array || table.GetArrayLength() == 0)
            {
                return null;
            }

            var latest = table.EnumerateArray().First();

            // Determinar tipo de test desde el log si es posible
            var testType = SmartTestType.Short;
            if (latest.TryGetProperty("type", out var typeEl))
            {
                string typeText;
                if (typeEl.ValueKind == JsonValueKind.String)
                    typeText = typeEl.GetString() ?? string.Empty;
                else if (typeEl.ValueKind == JsonValueKind.Object &&
                         typeEl.TryGetProperty("string", out var typeString))
                    typeText = typeString.GetString() ?? string.Empty;
                else
                    typeText = string.Empty;

                if (typeText.Contains("Extended", StringComparison.OrdinalIgnoreCase))
                    testType = SmartTestType.Extended;
            }

            // Extraer status de forma robusta (string directo u objeto { string: ... })
            string rawStatus = string.Empty;
            if (latest.TryGetProperty("status", out var statusEl))
            {
                if (statusEl.ValueKind == JsonValueKind.String)
                    rawStatus = statusEl.GetString() ?? string.Empty;
                else if (statusEl.ValueKind == JsonValueKind.Object &&
                         statusEl.TryGetProperty("string", out var statusString))
                    rawStatus = statusString.GetString() ?? string.Empty;
            }

            var testResult = new SmartTestResult
            {
                Device = device.Name,
                TestType = testType,
                RawStatus = rawStatus
            };

            testResult.Status = SmartctlParser.MapStatusText(testResult.RawStatus);
            testResult.ResultMessage = testResult.Status.ToDisplayMessage();

            if (latest.TryGetProperty("lifetime_hours", out var lifetime))
            {
                testResult.LifetimeHours = lifetime.GetInt64();
            }

            if (latest.TryGetProperty("lba_of_first_error", out var lba) &&
                lba.ValueKind != JsonValueKind.Null)
            {
                testResult.LbaOfFirstError = lba.GetInt64();
            }

            return testResult;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> SaveSessionAsync(SmartTestSession session)
    {
        EnsureDirectoryExists();

        // Filename determinístico: la misma sesión siempre sobrescribe el mismo archivo
        var fileName = $"smart-test-{session.TestType.ToString().ToLowerInvariant()}-{session.RequestedAt:yyyyMMdd-HHmmss}-{session.Id}.json";
        var filePath = Path.Combine(_testsDirectory, fileName);

        var json = JsonSerializer.Serialize(session, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SmartTestSession>> ListSessionsAsync(int maxResults = 20)
    {
        var sessions = new List<SmartTestSession>();

        if (!Directory.Exists(_testsDirectory))
            return sessions;

        // Leer TODOS los archivos relevantes (incluye formato legacy sin Id)
        var files = Directory.GetFiles(_testsDirectory, "smart-test-*.json");
        var byId = new Dictionary<string, SmartTestSession>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var session = JsonSerializer.Deserialize<SmartTestSession>(json, SerializerOptions);

                if (session == null || string.IsNullOrEmpty(session.Id))
                    continue;

                // Si ya hay un snapshot para este Id, quedarse con el más reciente
                if (byId.TryGetValue(session.Id, out var existing))
                {
                    if (GetEffectiveDate(session) > GetEffectiveDate(existing))
                        byId[session.Id] = session;
                }
                else
                {
                    byId[session.Id] = session;
                }
            }
            catch
            {
                // Archivo corrupto: omitir sin romper el listado
            }
        }

        // Ordenar por fecha efectiva descendente (más reciente → más antiguo)
        sessions = byId.Values
            .OrderByDescending(GetEffectiveDate)
            .ToList();

        // Recién después aplicar maxResults
        if (maxResults > 0 && sessions.Count > maxResults)
            sessions = sessions.Take(maxResults).ToList();

        return sessions;
    }

    /// <summary>
    /// Fecha efectiva de una sesión para ordenar/desempate.
    /// Prioridad: LastCheckedAt → CompletedAt → StartedAt → RequestedAt.
    /// </summary>
    private static DateTime GetEffectiveDate(SmartTestSession session)
    {
        return session.LastCheckedAt
            ?? session.CompletedAt
            ?? session.StartedAt
            ?? session.RequestedAt;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_testsDirectory))
            Directory.CreateDirectory(_testsDirectory);
    }
}
