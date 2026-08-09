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
            RequestedAt = DateTime.Now
        };

        // Mapear comando según tipo de test
        var testCommand = testType switch
        {
            SmartTestType.Short => "-t short -j",
            SmartTestType.Extended => "-t long -j",
            _ => throw new ArgumentOutOfRangeException(nameof(testType))
        };

        // Ejecutar smartctl -t short|long -j <device>
        var result = await _smartctlRunner.RunAsync($"{testCommand} {device.Name}", TestStartTimeout);

        if (result.TimedOut)
        {
            session.Status = SmartTestStatus.FailedToStart;
            session.Errors.Add("Timeout al iniciar el test");
            session.ResultMessage = SmartTestStatus.FailedToStart.ToDisplayMessage();
            await SaveSessionAsync(session);
            return session;
        }

        if (!result.IsSuccess)
        {
            session.Status = SmartTestStatus.FailedToStart;
            session.Errors.Add(result.StandardError);
            session.ResultMessage = SmartTestStatus.FailedToStart.ToDisplayMessage();
            await SaveSessionAsync(session);
            return session;
        }

        // Parsear respuesta JSON de forma estructurada
        var parseResult = SmartctlParser.ParseStartShortTestJson(result.StandardOutput);

        // Aplicar estado resultante
        session.Status = parseResult.Status;
        session.ResultMessage = parseResult.Message;
        session.SmartctlExitCode = result.ExitCode;
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
        // Consultar self-test log (solo lectura)
        var result = await _smartctlRunner.RunAsync($"-l selftest -j {session.Device}", StatusCheckTimeout);

        if (result.TimedOut || !result.IsSuccess)
        {
            // Error temporal de consulta: NO marcar el test como finalizado.
            // El test puede seguir ejecutándose internamente en el disco.
            session.LastCheckSucceeded = false;
            session.LastCheckError = result.StandardError ?? "Timeout al consultar estado";
            session.LastCheckedAt = DateTime.Now;

            // Conservar StartedAt, EstimatedCompletionAt y warnings previos (no se tocan)
            await SaveSessionAsync(session);
            return session;
        }

        // Parsear estado
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
        var result = await _smartctlRunner.RunAsync($"-l selftest -j {device.Name}", StatusCheckTimeout);

        if (result.TimedOut || !result.IsSuccess || string.IsNullOrWhiteSpace(result.StandardOutput))
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

        var fileName = $"smart-test-{session.TestType.ToString().ToLowerInvariant()}-{session.RequestedAt:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_testsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"smart-test-{session.TestType.ToString().ToLowerInvariant()}-{session.RequestedAt:yyyyMMdd-HHmmss}-{session.Id}.json";
            filePath = Path.Combine(_testsDirectory, fileName);
        }

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

        var files = Directory.GetFiles(_testsDirectory, "smart-test-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var session = JsonSerializer.Deserialize<SmartTestSession>(json, SerializerOptions);
                if (session != null)
                    sessions.Add(session);
            }
            catch { }
        }

        return sessions;
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_testsDirectory))
            Directory.CreateDirectory(_testsDirectory);
    }
}
