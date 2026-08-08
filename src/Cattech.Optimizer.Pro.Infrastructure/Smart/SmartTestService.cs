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
    public async Task<SmartTestSession> StartShortTestAsync(SmartDiskDevice device)
    {
        var session = new SmartTestSession
        {
            Device = device.Name,
            ModelName = device.ModelName,
            SerialNumber = device.SerialNumber,
            TestType = SmartTestType.Short,
            Status = SmartTestStatus.Starting,
            RequestedAt = DateTime.Now
        };

        // Ejecutar smartctl -t short -j <device>
        var result = await _smartctlRunner.RunAsync($"-t short -j {device.Name}", TestStartTimeout);

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

            var testResult = new SmartTestResult
            {
                Device = device.Name,
                TestType = SmartTestType.Short,
                RawStatus = latest.TryGetProperty("status", out var status)
                    ? status.GetString() ?? string.Empty
                    : string.Empty
            };

            testResult.Status = SmartctlParser.MapStatusText(testResult.RawStatus);
            testResult.ResultMessage = SmartctlParser.StatusToMessage(testResult.Status);

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
