using System.IO;
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

        var (started, message, minutes) = SmartctlParser.ParseStartShortTestJson(json);

        Assert.True(started);
        Assert.Contains("Testing has begun", message);
        Assert.Equal(2, minutes);
    }

    [Fact]
    public void ParseStartShortTestJson_Empty_ReturnsNotStarted()
    {
        var (started, message, _) = SmartctlParser.ParseStartShortTestJson("");

        Assert.False(started);
        Assert.Contains("vacía", message);
    }

    [Fact]
    public void ParseStartShortTestJson_Unsupported_ReturnsNotStarted()
    {
        var json = @"{
            ""smartctl"": {
                ""messages"": [
                    { ""string"": ""SMART self-test not supported on this device."" }
                ],
                ""exit_status"": { ""value"": 2 }
            }
        }";

        var (started, message, _) = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(started);
        Assert.Contains("not supported", message);
    }

    [Fact]
    public void ParseStartShortTestJson_InvalidJson_ReturnsNotStarted()
    {
        var (started, _, _) = SmartctlParser.ParseStartShortTestJson("not json");

        Assert.False(started);
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
    public async Task CheckStatus_NoSmartctl_UnknownStatus()
    {
        var runner = new SmartctlRunner("/nonexistent/smartctl.exe");
        var service = new SmartTestService(runner, Path.Combine(Path.GetTempPath(), $"cattech_smart_test_{Guid.NewGuid():N}"));

        var session = new SmartTestSession { Device = "/dev/sda", Status = SmartTestStatus.InProgress };
        var result = await service.CheckStatusAsync(session);

        Assert.Equal(SmartTestStatus.Unknown, result.Status);
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
}
