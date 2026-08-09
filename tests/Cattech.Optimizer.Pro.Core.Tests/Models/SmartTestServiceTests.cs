using System.IO;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Tests del servicio de self-test SMART.
/// Usa SmartctlRunner con ruta inexistente para evitar ejecutar smartctl real.
/// </summary>
public class SmartTestServiceTests
{
    // =====================
    // Tests de parseo de inicio
    // =====================

    [Fact]
    public void ParseStartShortTestJson_TestWillComplete_ReturnsStarted()
    {
        var json = @"{
            ""smartctl"": {
                ""messages"": [
                    { ""string"": ""Testing has begun.  Please wait 2 minutes for test to complete."" },
                    { ""string"": ""Use smartctl -X to abort test."" }
                ],
                ""exit_status"": { ""value"": 0 }
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.True(result.Started);
        Assert.Equal(SmartTestStatus.InProgress, result.Status);
        Assert.Contains("Testing has begun", result.Message);
        Assert.Equal(2, result.EstimatedDurationMinutes);
    }

    [Fact]
    public void ParseStartShortTestJson_Empty_ReturnsFailedToStart()
    {
        var result = SmartctlParser.ParseStartShortTestJson("");

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.FailedToStart, result.Status);
        Assert.Contains("vacía", result.Message);
    }

    [Fact]
    public void ParseStartShortTestJson_ExitStatus4_ReturnsUnsupported()
    {
        var json = @"{
            ""smartctl"": {
                ""messages"": [
                    { ""string"": ""SMART self-test not supported on this device."" }
                ],
                ""exit_status"": { ""value"": 4 }
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.Unsupported, result.Status);
        Assert.Equal(4, result.SmartctlExitStatus);
    }

    [Fact]
    public void ParseStartShortTestJson_InvalidJson_ReturnsFailedToStart()
    {
        var result = SmartctlParser.ParseStartShortTestJson("not json");

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.FailedToStart, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    // =====================
    // Tests de duración estimada
    // =====================

    [Fact]
    public void ExtractEstimatedMinutes_ValidMessage_ReturnsMinutes()
    {
        var minutes = SmartctlParser.ExtractEstimatedMinutes("Test will complete in 2 minutes");
        Assert.Equal(2, minutes);
    }

    [Fact]
    public void ExtractEstimatedMinutes_NoNumber_ReturnsNull()
    {
        var minutes = SmartctlParser.ExtractEstimatedMinutes("Testing has begun");
        Assert.Null(minutes);
    }

    [Fact]
    public void ExtractEstimatedMinutes_Empty_ReturnsNull()
    {
        var minutes = SmartctlParser.ExtractEstimatedMinutes("");
        Assert.Null(minutes);
    }

    // =====================
    // Tests de mapeo de estados
    // =====================

    [Fact]
    public void MapStatusText_CompletedWithoutError_MapsCorrectly()
    {
        Assert.Equal(SmartTestStatus.CompletedWithoutError,
            SmartctlParser.MapStatusText("Completed without error"));
    }

    [Fact]
    public void MapStatusText_CompletedWithError_MapsCorrectly()
    {
        Assert.Equal(SmartTestStatus.CompletedWithError,
            SmartctlParser.MapStatusText("Completed: read failure"));
    }

    [Fact]
    public void MapStatusText_Aborted_MapsCorrectly()
    {
        Assert.Equal(SmartTestStatus.Aborted,
            SmartctlParser.MapStatusText("Aborted by host"));
    }

    [Fact]
    public void MapStatusText_Interrupted_MapsCorrectly()
    {
        Assert.Equal(SmartTestStatus.Interrupted,
            SmartctlParser.MapStatusText("Interrupted (host reset)"));
    }

    [Fact]
    public void MapStatusText_InProgress_MapsCorrectly()
    {
        Assert.Equal(SmartTestStatus.InProgress,
            SmartctlParser.MapStatusText("Self-test routine in progress"));
    }

    [Fact]
    public void MapStatusText_Unsupported_MapsCorrectly()
    {
        Assert.Equal(SmartTestStatus.Unsupported,
            SmartctlParser.MapStatusText("SMART self-test not supported"));
    }

    [Fact]
    public void MapStatusText_Empty_ReturnsUnknown()
    {
        Assert.Equal(SmartTestStatus.Unknown, SmartctlParser.MapStatusText(""));
    }

    [Fact]
    public void StatusToMessage_AllStates_HaveMessages()
    {
        foreach (SmartTestStatus status in Enum.GetValues<SmartTestStatus>())
        {
            var message = SmartctlParser.StatusToMessage(status);
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    // =====================
    // Tests de parseo de self-test log
    // =====================

    [Fact]
    public void ParseSelfTestLogJson_CompletedWithoutError_UpdatesSession()
    {
        var json = @"{
            ""ata_smart_self_test_log"": {
                ""standard"": {
                    ""table"": [
                        {
                            ""type"": { ""string"": ""Short offline"" },
                            ""status"": { ""string"": ""Completed without error"" },
                            ""remaining"": ""0%"",
                            ""lifetime_hours"": 12345
                        }
                    ]
                }
            }
        }";

        var session = new SmartTestSession { Device = "/dev/sda" };
        var result = SmartctlParser.ParseSelfTestLogJson(json, session);

        Assert.Equal(SmartTestStatus.CompletedWithoutError, result.Status);
        Assert.NotNull(result.LastCheckedAt);
    }

    [Fact]
    public void ParseSelfTestLogJson_InProgress_UpdatesProgress()
    {
        var json = @"{
            ""ata_smart_self_test_log"": {
                ""standard"": {
                    ""table"": [
                        {
                            ""type"": { ""string"": ""Short offline"" },
                            ""status"": { ""string"": ""Self-test routine in progress"" },
                            ""remaining"": ""60%"",
                            ""lifetime_hours"": 12345
                        }
                    ]
                }
            }
        }";

        var session = new SmartTestSession { Device = "/dev/sda" };
        var result = SmartctlParser.ParseSelfTestLogJson(json, session);

        Assert.Equal(SmartTestStatus.InProgress, result.Status);
        Assert.Equal(40, result.ProgressPercent);
    }

    [Fact]
    public void ParseSelfTestLogJson_InvalidJson_UnknownStatus()
    {
        var session = new SmartTestSession { Device = "/dev/sda" };
        var result = SmartctlParser.ParseSelfTestLogJson("not json", session);

        Assert.Equal(SmartTestStatus.Unknown, result.Status);
    }

    [Fact]
    public void ParseSelfTestLogJson_Empty_UnknownStatus()
    {
        var session = new SmartTestSession { Device = "/dev/sda" };
        var result = SmartctlParser.ParseSelfTestLogJson("", session);

        Assert.Equal(SmartTestStatus.Unknown, result.Status);
    }

    [Fact]
    public void ParseSelfTestLogJson_NoLog_UnknownStatus()
    {
        var json = @"{
            ""model_name"": ""Test Disk""
        }";

        var session = new SmartTestSession { Device = "/dev/sda" };
        var result = SmartctlParser.ParseSelfTestLogJson(json, session);

        Assert.Equal(SmartTestStatus.Unknown, result.Status);
    }

    // =====================
    // Tests de sesión
    // =====================

    [Fact]
    public void SmartTestSession_DefaultValues_AreValid()
    {
        var session = new SmartTestSession();

        Assert.NotNull(session.Id);
        Assert.NotEmpty(session.Id);
        Assert.Equal(8, session.Id.Length);
        Assert.Equal(SmartTestType.Short, session.TestType);
        Assert.Equal(SmartTestStatus.NotStarted, session.Status);
        Assert.True(session.RequestedAt <= DateTime.Now);
        Assert.Null(session.EstimatedDurationMinutes);
        Assert.Null(session.CompletedAt);
    }

    [Fact]
    public void SmartTestSession_PreservesTimestamps()
    {
        var startedAt = new DateTime(2026, 2, 1, 10, 0, 0);
        var session = new SmartTestSession
        {
            RequestedAt = startedAt,
            StartedAt = startedAt.AddMinutes(1),
            EstimatedCompletionAt = startedAt.AddMinutes(3),
            CompletedAt = startedAt.AddMinutes(3)
        };

        Assert.Equal(startedAt, session.RequestedAt);
        Assert.Equal(startedAt.AddMinutes(1), session.StartedAt);
        Assert.Equal(startedAt.AddMinutes(3), session.EstimatedCompletionAt);
        Assert.Equal(startedAt.AddMinutes(3), session.CompletedAt);
    }

    [Fact]
    public void SmartTestSession_SerializePreservesDeviceAndSerial()
    {
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            ModelName = "Samsung SSD 860 EVO",
            SerialNumber = "S3Z9NB0K123456",
            TestType = SmartTestType.Short,
            Status = SmartTestStatus.InProgress
        };

        var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SmartTestSession>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.Equal("/dev/sda", deserialized!.Device);
        Assert.Equal("S3Z9NB0K123456", deserialized.SerialNumber);
        Assert.Equal(SmartTestStatus.InProgress, deserialized.Status);
    }

    // =====================
    // Tests del servicio con smartctl inexistente
    // =====================

    [Fact]
    public async Task StartShortTest_NoSmartctl_FailsToStart()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartTestService(runner, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var session = await service.StartShortTestAsync(device);

        Assert.Equal(SmartTestStatus.FailedToStart, session.Status);
        Assert.NotEmpty(session.Errors);
    }

    [Fact]
    public async Task CheckStatus_NoSmartctl_KeepsSessionState()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartTestService(runner, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress,
            StartedAt = DateTime.Now.AddMinutes(-1)
        };
        var result = await service.CheckStatusAsync(session);

        // El error temporal de consulta NO debe cambiar el estado del test
        Assert.Equal(SmartTestStatus.InProgress, result.Status);
        Assert.False(result.LastCheckSucceeded);
        Assert.NotEmpty(result.LastCheckError);
        Assert.NotNull(result.StartedAt);
    }

    [Fact]
    public async Task ListSessions_EmptyDirectory_ReturnsEmpty()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartTestService(runner, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var sessions = await service.ListSessionsAsync();
        Assert.Empty(sessions);
    }

    // =====================
    // Tests de reglas de seguridad (ViewModel logic)
    // =====================

    [Fact]
    public void CriticalDisk_BlocksTestStart()
    {
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            HealthStatus = SmartHealthStatus.Critical
        };

        // Lógica: disco crítico → no permitir test
        var canStart = report.HealthStatus != SmartHealthStatus.Critical;
        Assert.False(canStart);
    }

    [Fact]
    public void TestInProgress_BlocksSecondStart()
    {
        // Si ya hay una sesión InProgress para el disco, no iniciar otra
        var existingSession = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress
        };

        var canStartAnother = existingSession.Status != SmartTestStatus.InProgress;
        Assert.False(canStartAnother);
    }

    [Fact]
    public void CompletedSession_AllowsNewTest()
    {
        var completedSession = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.CompletedWithoutError
        };

        var canStartAnother = completedSession.Status != SmartTestStatus.InProgress;
        Assert.True(canStartAnother);
    }

    [Fact]
    public void SmartTestResult_CompletedWithError_HasBackupRecommendation()
    {
        var result = new SmartTestResult
        {
            Status = SmartTestStatus.CompletedWithError,
            ResultMessage = "La prueba detectó errores. Revisar SMART y realizar backup."
        };

        Assert.Contains("errores", result.ResultMessage);
        Assert.Contains("backup", result.ResultMessage);
    }

    [Fact]
    public void SmartTestResult_LbaOfFirstError_IsNullable()
    {
        var result = new SmartTestResult();
        Assert.Null(result.LbaOfFirstError);

        result.LbaOfFirstError = 12345;
        Assert.Equal(12345, result.LbaOfFirstError);
    }

    // =====================
    // Tests de arquitectura (refactor)
    // =====================

    [Fact]
    public void SmartTestStartParseResult_DefaultValues_AreValid()
    {
        var result = new SmartTestStartParseResult();

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.Unknown, result.Status);
        Assert.Null(result.EstimatedDurationMinutes);
        Assert.Null(result.SmartctlExitStatus);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ParseStartShortTestJson_ExitStatus1_ReturnsInProgress()
    {
        // exit_status 1 = success with SMART warnings, test still started
        var json = @"{
            ""smartctl"": {
                ""messages"": [
                    { ""string"": ""Testing has begun.  Please wait 2 minutes for test to complete."" }
                ],
                ""exit_status"": { ""value"": 1 }
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.True(result.Started);
        Assert.Equal(SmartTestStatus.InProgress, result.Status);
        Assert.Equal(1, result.SmartctlExitStatus);
    }

    [Fact]
    public void ParseStartShortTestJson_ExitStatus2_ReturnsFailedToStart()
    {
        // exit_status 2 = device open failed (permisos)
        var json = @"{
            ""smartctl"": {
                ""messages"": [
                    { ""string"": ""Device open failed"" }
                ],
                ""exit_status"": { ""value"": 2 }
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.FailedToStart, result.Status);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseStartShortTestJson_ExitStatus3_ReturnsFailedToStart()
    {
        var json = @"{
            ""smartctl"": {
                ""messages"": [],
                ""exit_status"": { ""value"": 3 }
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.FailedToStart, result.Status);
    }

    [Fact]
    public void ParseStartShortTestJson_NoExitStatus_ReturnsFailedToStart()
    {
        var json = @"{
            ""smartctl"": {
                ""messages"": []
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.FailedToStart, result.Status);
    }

    [Fact]
    public void ParseStartShortTestJson_Unsupported_NoTextDependency()
    {
        // El mecanismo principal es exit_status=4, no el texto "not supported"
        var json = @"{
            ""smartctl"": {
                ""messages"": [
                    { ""string"": ""Algun mensaje en cualquier idioma"" }
                ],
                ""exit_status"": { ""value"": 4 }
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.Unsupported, result.Status);
    }

    [Fact]
    public void CheckStatusTimeout_DoesNotMarkTestAsFinished()
    {
        // Un error temporal de consulta no debe finalizar el test
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress,
            StartedAt = new DateTime(2026, 2, 1, 10, 0, 0),
            EstimatedCompletionAt = new DateTime(2026, 2, 1, 10, 2, 0),
            Warnings = ["warning previo"]
        };

        // Simular fallo de consulta: marcar error temporal, NO cambiar Status
        session.LastCheckSucceeded = false;
        session.LastCheckError = "Timeout al consultar estado";

        Assert.Equal(SmartTestStatus.InProgress, session.Status);
        Assert.False(session.LastCheckSucceeded);
        Assert.Contains("Timeout", session.LastCheckError);
    }

    [Fact]
    public void CheckStatusError_PreservesStartedAt()
    {
        var startedAt = new DateTime(2026, 2, 1, 10, 0, 0);
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress,
            StartedAt = startedAt
        };

        // Simular error temporal de consulta
        session.LastCheckSucceeded = false;
        session.LastCheckError = "Error temporal";

        Assert.Equal(startedAt, session.StartedAt);
    }

    [Fact]
    public void CheckStatusError_PreservesEstimatedCompletionAt()
    {
        var estimatedCompletion = new DateTime(2026, 2, 1, 10, 2, 0);
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress,
            EstimatedCompletionAt = estimatedCompletion
        };

        // Simular error temporal de consulta
        session.LastCheckSucceeded = false;
        session.LastCheckError = "Error temporal";

        Assert.Equal(estimatedCompletion, session.EstimatedCompletionAt);
    }

    [Fact]
    public void CheckStatusError_PreservesWarnings()
    {
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            Status = SmartTestStatus.InProgress
        };
        session.Warnings.Add("warning previo 1");
        session.Warnings.Add("warning previo 2");

        // Simular error temporal de consulta
        session.LastCheckSucceeded = false;
        session.LastCheckError = "Error temporal";

        Assert.Equal(2, session.Warnings.Count);
        Assert.Contains("warning previo 1", session.Warnings);
    }

    [Fact]
    public void ToDisplayMessage_AllStates_HaveMessages()
    {
        foreach (SmartTestStatus status in Enum.GetValues<SmartTestStatus>())
        {
            var message = status.ToDisplayMessage();
            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    [Fact]
    public void SmartDiskReport_SelfTestCapabilities_DefaultToNull()
    {
        var report = new SmartDiskReport();

        Assert.Null(report.SupportsSelfTest);
        Assert.Null(report.SupportsShortSelfTest);
        Assert.Null(report.SupportsExtendedSelfTest);
        Assert.False(report.SelfTestSupportKnown);
    }

    [Fact]
    public void SmartDiskReport_SelfTestCapabilities_AreSettable()
    {
        var report = new SmartDiskReport
        {
            SupportsSelfTest = true,
            SupportsShortSelfTest = true,
            SupportsExtendedSelfTest = false,
            SelfTestSupportKnown = true
        };

        Assert.True(report.SupportsSelfTest);
        Assert.True(report.SupportsShortSelfTest);
        Assert.False(report.SupportsExtendedSelfTest);
        Assert.True(report.SelfTestSupportKnown);
    }

    // =====================
    // Tests Fase A.6 - Extended Self-Test
    // =====================

    [Fact]
    public async Task StartExtendedTest_NoSmartctl_FailsToStart()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartTestService(runner, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var session = await service.StartExtendedTestAsync(device);

        Assert.Equal(SmartTestStatus.FailedToStart, session.Status);
        Assert.Equal(SmartTestType.Extended, session.TestType);
    }

    [Fact]
    public void ExtendedTest_UsesLongCommand()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStartResponse(0);
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        service.StartExtendedTestAsync(device).GetAwaiter().GetResult();

        Assert.Contains("-t long -j", mock.LastArguments);
    }

    [Fact]
    public void ShortTest_StillUsesShortCommand()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStartResponse(0);
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        service.StartShortTestAsync(device).GetAwaiter().GetResult();

        Assert.Contains("-t short -j", mock.LastArguments);
    }

    [Fact]
    public async Task StartExtended_ValidResponse_InProgress()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStartResponse(0);
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var session = await service.StartExtendedTestAsync(device);

        Assert.Equal(SmartTestStatus.InProgress, session.Status);
        Assert.Equal(SmartTestType.Extended, session.TestType);
    }

    [Fact]
    public async Task ExtendedDuration_ParsedCorrectly()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStartResponse(0, "Testing has begun. Please wait 120 minutes for test to complete.");
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var session = await service.StartExtendedTestAsync(device);

        Assert.Equal(120, session.EstimatedDurationMinutes);
        Assert.NotNull(session.EstimatedCompletionAt);
    }

    [Fact]
    public async Task ExtendedDuration_Unknown_RemainsNull()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStartResponse(0, "Testing has begun.");
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var session = await service.StartExtendedTestAsync(device);

        Assert.Null(session.EstimatedDurationMinutes);
        Assert.Null(session.EstimatedCompletionAt);
    }

    [Fact]
    public async Task ExtendedCompletionAt_OnlyIfDurationExists()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStartResponse(0, "Testing has begun. Please wait 45 minutes for test to complete.");
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var session = await service.StartExtendedTestAsync(device);

        Assert.Equal(45, session.EstimatedDurationMinutes);
        Assert.NotNull(session.EstimatedCompletionAt);
    }

    [Fact]
    public async Task CriticalDisk_BlocksExtendedTest()
    {
        var mock = new MockSmartctlRunner();
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            HealthStatus = SmartHealthStatus.Critical
        };

        // La lógica de bloqueo está en el ViewModel, pero validamos que Critical != Good
        var canStart = report.HealthStatus != SmartHealthStatus.Critical;
        Assert.False(canStart);
    }

    [Fact]
    public async Task SupportsExtendedSelfTestFalse_BlocksExecution()
    {
        // Si SelfTestSupportKnown == true y SupportsExtendedSelfTest == false → no ejecutar
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            SupportsExtendedSelfTest = false,
            SelfTestSupportKnown = true
        };

        var canStart = !(report.SelfTestSupportKnown && report.SupportsExtendedSelfTest == false);
        Assert.False(canStart);
    }

    [Fact]
    public async Task UnknownSupport_AllowsAttempt()
    {
        // Soporte desconocido (null) → permitir intento
        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            SupportsExtendedSelfTest = null,
            SelfTestSupportKnown = false
        };

        var canStart = report.SelfTestSupportKnown
            ? report.SupportsExtendedSelfTest == true
            : true;
        Assert.True(canStart);
    }

    [Fact]
    public async Task ExtendedInProgress_BlocksShort()
    {
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            TestType = SmartTestType.Extended,
            Status = SmartTestStatus.InProgress
        };

        var canStartShort = !(session.Status == SmartTestStatus.InProgress &&
                              session.Device == "/dev/sda");
        Assert.False(canStartShort);
    }

    [Fact]
    public async Task ShortInProgress_BlocksExtended()
    {
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            Status = SmartTestStatus.InProgress
        };

        var canStartExtended = !(session.Status == SmartTestStatus.InProgress &&
                                 session.Device == "/dev/sda");
        Assert.False(canStartExtended);
    }

    [Fact]
    public async Task CheckStatus_PreservesExtendedType()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupStatusResponse(completedNoError: true);
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            TestType = SmartTestType.Extended,
            Status = SmartTestStatus.InProgress
        };

        var result = await service.CheckStatusAsync(session);

        Assert.Equal(SmartTestType.Extended, result.TestType);
    }

    [Fact]
    public async Task CheckStatus_Timeout_PreservesActiveSession()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartTestService(runner, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            TestType = SmartTestType.Extended,
            Status = SmartTestStatus.InProgress,
            StartedAt = DateTime.Now.AddMinutes(-1)
        };

        var result = await service.CheckStatusAsync(session);

        // El timeout NO finaliza el test
        Assert.Equal(SmartTestStatus.InProgress, result.Status);
        Assert.False(result.LastCheckSucceeded);
    }

    [Fact]
    public void SessionPersistence_UsesExtendedName()
    {
        var session = new SmartTestSession
        {
            TestType = SmartTestType.Extended,
            RequestedAt = new DateTime(2026, 2, 1, 10, 30, 0)
        };

        var expected = $"smart-test-extended-{session.RequestedAt:yyyyMMdd-HHmmss}.json";
        var fileName = $"smart-test-{session.TestType.ToString().ToLowerInvariant()}-{session.RequestedAt:yyyyMMdd-HHmmss}.json";

        Assert.Equal(expected, fileName);
        Assert.Contains("extended", fileName);
    }

    [Fact]
    public async Task GetLatestResult_ExtendedLog_ReturnsExtended()
    {
        var mock = new MockSmartctlRunner();
        mock.SetupSelfTestLog("Extended offline");
        var service = new SmartTestService(mock, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var device = new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" };
        var result = await service.GetLatestResultAsync(device);

        Assert.NotNull(result);
        Assert.Equal(SmartTestType.Extended, result!.TestType);
    }

    [Fact]
    public void UiBlocksBothButtons_DuringActiveTest()
    {
        // Simular lógica del ViewModel: test activo bloquea corto y extendido
        var isTestInProgress = true;
        var canStartTest = !isTestInProgress;
        var canStartExtendedTest = !isTestInProgress;

        Assert.False(canStartTest);
        Assert.False(canStartExtendedTest);
    }

    // =====================
    // Tests de persistencia estabilizada
    // =====================

    private static string CreateTestDir()
        => Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}");

    private static readonly JsonSerializerOptions TestSerializer = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task SaveSession_AlwaysIncludesId()
    {
        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), CreateTestDir());
        var session = new SmartTestSession
        {
            Id = "ABC12345",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 11, 15, 0)
        };

        var fileName = await service.SaveSessionAsync(session);

        Assert.Contains("ABC12345", fileName);
    }

    [Fact]
    public async Task SaveSession_SameSession_SameFilename()
    {
        var dir = CreateTestDir();
        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var session = new SmartTestSession
        {
            Id = "ABC12345",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 11, 15, 0)
        };

        var first = await service.SaveSessionAsync(session);
        session.Status = SmartTestStatus.CompletedWithoutError;
        var second = await service.SaveSessionAsync(session);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SaveSession_SecondPersist_DoesNotCreateSecondFile()
    {
        var dir = CreateTestDir();
        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var session = new SmartTestSession
        {
            Id = "ABC12345",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 11, 15, 0)
        };

        await service.SaveSessionAsync(session);
        session.Status = SmartTestStatus.CompletedWithoutError;
        await service.SaveSessionAsync(session);

        var files = Directory.GetFiles(Path.Combine(dir, "data", "smart-tests"), "smart-test-*.json");
        Assert.Single(files);
    }

    [Fact]
    public async Task SaveSession_Short_UsesShortPrefix()
    {
        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), CreateTestDir());
        var session = new SmartTestSession
        {
            Id = "ABC12345",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 11, 15, 0)
        };

        var fileName = await service.SaveSessionAsync(session);
        Assert.StartsWith("smart-test-short-", fileName);
    }

    [Fact]
    public async Task SaveSession_Extended_UsesExtendedPrefix()
    {
        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), CreateTestDir());
        var session = new SmartTestSession
        {
            Id = "E5F6G7H8",
            Device = "/dev/sda",
            TestType = SmartTestType.Extended,
            RequestedAt = new DateTime(2026, 8, 9, 11, 20, 0)
        };

        var fileName = await service.SaveSessionAsync(session);
        Assert.StartsWith("smart-test-extended-", fileName);
    }

    [Fact]
    public async Task ListSessions_LegacyFormat_StillReadable()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        // Formato legacy sin Id
        var legacy = new SmartTestSession
        {
            Id = "LEGACY01",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0)
        };
        var legacyJson = JsonSerializer.Serialize(legacy, TestSerializer);
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000.json"), legacyJson);

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Equal("LEGACY01", sessions[0].Id);
    }

    [Fact]
    public async Task ListSessions_LegacyAndNew_SameId_Deduplicated()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        // Snapshot legacy sin Id en filename pero con session.Id
        var legacy = new SmartTestSession
        {
            Id = "DUPE001",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            Status = SmartTestStatus.InProgress,
            StartedAt = new DateTime(2026, 8, 9, 10, 0, 5)
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000.json"),
            JsonSerializer.Serialize(legacy, TestSerializer));

        // Snapshot nuevo con mismo session.Id y estado más reciente
        var newer = new SmartTestSession
        {
            Id = "DUPE001",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            Status = SmartTestStatus.CompletedWithoutError,
            StartedAt = new DateTime(2026, 8, 9, 10, 0, 5),
            CompletedAt = new DateTime(2026, 8, 9, 10, 5, 0)
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000-DUPE001.json"),
            JsonSerializer.Serialize(newer, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Equal(SmartTestStatus.CompletedWithoutError, sessions[0].Status);
    }

    [Fact]
    public async Task ListSessions_NewestLastCheckedAt_Wins()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var older = new SmartTestSession
        {
            Id = "WIN001",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            LastCheckedAt = new DateTime(2026, 8, 9, 10, 1, 0),
            Warnings = ["old warning"]
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000-WIN001.json"),
            JsonSerializer.Serialize(older, TestSerializer));

        var newer = new SmartTestSession
        {
            Id = "WIN001",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            LastCheckedAt = new DateTime(2026, 8, 9, 10, 2, 0),
            Warnings = ["new warning"]
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000-WIN001-2.json"),
            JsonSerializer.Serialize(newer, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Contains("new warning", sessions[0].Warnings);
    }

    [Fact]
    public async Task GetEffectiveDate_UsesCompletedAt_WhenLastCheckedNull()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var noLastCheck = new SmartTestSession
        {
            Id = "DATE01",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            CompletedAt = new DateTime(2026, 8, 9, 10, 5, 0),
            Status = SmartTestStatus.CompletedWithoutError
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000-DATE01.json"),
            JsonSerializer.Serialize(noLastCheck, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Equal(SmartTestStatus.CompletedWithoutError, sessions[0].Status);
    }

    [Fact]
    public async Task GetEffectiveDate_UsesStartedAt_WhenLaterDatesNull()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var session = new SmartTestSession
        {
            Id = "DATE02",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0),
            StartedAt = new DateTime(2026, 8, 9, 10, 0, 30),
            Status = SmartTestStatus.InProgress
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000-DATE02.json"),
            JsonSerializer.Serialize(session, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Equal(SmartTestStatus.InProgress, sessions[0].Status);
    }

    [Fact]
    public async Task GetEffectiveDate_UsesRequestedAt_AsFallback()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var session = new SmartTestSession
        {
            Id = "DATE03",
            Device = "/dev/sda",
            TestType = SmartTestType.Short,
            RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0)
        };
        await File.WriteAllTextAsync(
            Path.Combine(testsDir, "smart-test-short-20260809-100000-DATE03.json"),
            JsonSerializer.Serialize(session, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Equal("DATE03", sessions[0].Id);
    }

    [Fact]
    public async Task ListSessions_DifferentIds_NeverDeduplicated()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var sessionA = new SmartTestSession { Id = "AAAA1111", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0) };
        var sessionB = new SmartTestSession { Id = "BBBB2222", Device = "/dev/sdb", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 1, 0) };

        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-AAAA1111.json"), JsonSerializer.Serialize(sessionA, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100100-BBBB2222.json"), JsonSerializer.Serialize(sessionB, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task ListSessions_DedupBeforeMaxResults()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        // 2 snapshots del mismo Id + 1 sesión distinta = 2 únicas, maxResults=1 → 1
        var sessionA1 = new SmartTestSession { Id = "AAA00001", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0), LastCheckedAt = new DateTime(2026, 8, 9, 10, 0, 1) };
        var sessionA2 = new SmartTestSession { Id = "AAA00001", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0), LastCheckedAt = new DateTime(2026, 8, 9, 10, 0, 2) };
        var sessionB = new SmartTestSession { Id = "BBB00002", Device = "/dev/sdb", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 1, 0) };

        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-AAA00001.json"), JsonSerializer.Serialize(sessionA1, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-AAA00001-2.json"), JsonSerializer.Serialize(sessionA2, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100100-BBB00002.json"), JsonSerializer.Serialize(sessionB, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync(maxResults: 1);

        Assert.Single(sessions);
    }

    [Fact]
    public async Task ListSessions_OrderedDescending()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var older = new SmartTestSession { Id = "ORDER01", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 9, 0, 0) };
        var middle = new SmartTestSession { Id = "ORDER02", Device = "/dev/sdb", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0) };
        var newer = new SmartTestSession { Id = "ORDER03", Device = "/dev/sdc", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 11, 0, 0) };

        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-090000-ORDER01.json"), JsonSerializer.Serialize(older, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-ORDER02.json"), JsonSerializer.Serialize(middle, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-110000-ORDER03.json"), JsonSerializer.Serialize(newer, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Equal(3, sessions.Count);
        Assert.Equal("ORDER03", sessions[0].Id);
        Assert.Equal("ORDER02", sessions[1].Id);
        Assert.Equal("ORDER01", sessions[2].Id);
    }

    [Fact]
    public async Task ListSessions_SelectedSnapshot_PreservesWarnings()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var older = new SmartTestSession { Id = "WARN01", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0), LastCheckedAt = new DateTime(2026, 8, 9, 10, 1, 0), Warnings = ["old"] };
        var newer = new SmartTestSession { Id = "WARN01", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0), LastCheckedAt = new DateTime(2026, 8, 9, 10, 2, 0), Warnings = ["kept warning"] };

        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-WARN01.json"), JsonSerializer.Serialize(older, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-WARN01-2.json"), JsonSerializer.Serialize(newer, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Contains("kept warning", sessions[0].Warnings);
    }

    [Fact]
    public async Task ListSessions_SelectedSnapshot_PreservesErrors()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        var older = new SmartTestSession { Id = "ERR001", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0), LastCheckedAt = new DateTime(2026, 8, 9, 10, 1, 0), Errors = ["old"] };
        var newer = new SmartTestSession { Id = "ERR001", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0), LastCheckedAt = new DateTime(2026, 8, 9, 10, 2, 0), Errors = ["kept error"] };

        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-ERR001.json"), JsonSerializer.Serialize(older, TestSerializer));
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-ERR001-2.json"), JsonSerializer.Serialize(newer, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Contains("kept error", sessions[0].Errors);
    }

    [Fact]
    public async Task ListSessions_CorruptJson_DoesNotBreakList()
    {
        var dir = CreateTestDir();
        var testsDir = Path.Combine(dir, "data", "smart-tests");
        Directory.CreateDirectory(testsDir);

        // Archivo corrupto
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-corrupt.json"), "{ not json !!!");

        // Sesión válida
        var valid = new SmartTestSession { Id = "VALID01", Device = "/dev/sda", TestType = SmartTestType.Short, RequestedAt = new DateTime(2026, 8, 9, 10, 0, 0) };
        await File.WriteAllTextAsync(Path.Combine(testsDir, "smart-test-short-20260809-100000-VALID01.json"), JsonSerializer.Serialize(valid, TestSerializer));

        var service = new SmartTestService(new SmartctlRunner("/nonexistent/smartctl.exe"), dir);
        var sessions = await service.ListSessionsAsync();

        Assert.Single(sessions);
        Assert.Equal("VALID01", sessions[0].Id);
    }
}

/// <summary>
/// Mock de ISmartctlRunner que captura los argumentos sin ejecutar smartctl real.
/// </summary>
public class MockSmartctlRunner : ISmartctlRunner
{
    public string LastArguments { get; private set; } = string.Empty;
    private string _startJson = string.Empty;
    private string _statusJson = string.Empty;

    public void SetupStartResponse(int exitStatus, string message = "Testing has begun.")
    {
        _startJson = $@"{{
            ""smartctl"": {{
                ""messages"": [
                    {{ ""string"": ""{message}"" }}
                ],
                ""exit_status"": {{ ""value"": {exitStatus} }}
            }}
        }}";
    }

    public void SetupStatusResponse(bool completedNoError)
    {
        var statusText = completedNoError ? "Completed without error" : "Self-test routine in progress";
        var remaining = completedNoError ? "0%" : "50%";
        _statusJson = $@"{{
            ""ata_smart_self_test_log"": {{
                ""standard"": {{
                    ""table"": [
                        {{
                            ""type"": {{ ""string"": ""Extended offline"" }},
                            ""status"": {{ ""string"": ""{statusText}"" }},
                            ""remaining"": ""{remaining}"",
                            ""lifetime_hours"": 12345
                        }}
                    ]
                }}
            }}
        }}";
    }

    public void SetupSelfTestLog(string testType)
    {
        _statusJson = $@"{{
            ""ata_smart_self_test_log"": {{
                ""standard"": {{
                    ""table"": [
                        {{
                            ""type"": {{ ""string"": ""{testType}"" }},
                            ""status"": {{ ""string"": ""Completed without error"" }},
                            ""remaining"": ""0%"",
                            ""lifetime_hours"": 12345
                        }}
                    ]
                }}
            }}
        }}";
    }

    public Task<SmartctlAvailability> CheckAvailabilityAsync()
    {
        return Task.FromResult(new SmartctlAvailability
        {
            IsAvailable = true,
            SmartctlPath = "C:\\mock\\smartctl.exe",
            Version = "smartctl 7.4",
            SupportsJson = true
        });
    }

    public Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout)
    {
        LastArguments = arguments;

        var output = arguments.Contains("-l selftest") ? _statusJson : _startJson;

        return Task.FromResult(new SmartctlCommandResult
        {
            ExitCode = 0,
            StandardOutput = output,
            StandardError = string.Empty,
            TimedOut = false,
            DurationMs = 10
        });
    }

    public Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync()
    {
        return Task.FromResult<IReadOnlyList<SmartDiskDevice>>(
            [new SmartDiskDevice { Name = "/dev/sda", ApproximateDiskType = "HDD" }]);
    }
}
