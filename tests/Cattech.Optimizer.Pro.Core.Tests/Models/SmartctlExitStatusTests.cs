using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SmartctlExitStatusTests
{
    // =====================
    // Fake runner (nunca ejecuta smartctl real)
    // =====================

    private sealed class FakeSmartctlRunner : ISmartctlRunner
    {
        public List<string> RunArguments { get; } = new();
        public SmartctlCommandResult? AnalyzeResponse { get; set; }
        public Dictionary<string, SmartctlCommandResult> AnalyzeResponseByDevice { get; } = new();
        public SmartctlCommandResult? StartResponse { get; set; }
        public SmartctlCommandResult? StatusResponse { get; set; }
        public SmartctlAvailability Availability { get; set; } = new()
        {
            IsAvailable = true,
            SmartctlPath = "C:\\mock\\smartctl.exe",
            Version = "smartctl 7.4",
            SupportsJson = true
        };
        public List<SmartDiskDevice> ScanDevices { get; set; } = new();

        public Task<SmartctlAvailability> CheckAvailabilityAsync() => Task.FromResult(Availability);

        public Task<SmartctlCommandResult> RunAsync(string arguments, TimeSpan timeout)
        {
            RunArguments.Add(arguments);

            if (arguments.Contains("-a -j", StringComparison.OrdinalIgnoreCase))
            {
                var deviceName = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();
                if (AnalyzeResponseByDevice.TryGetValue(deviceName, out var perDevice))
                {
                    return Task.FromResult(perDevice);
                }

                return Task.FromResult(AnalyzeResponse ?? new SmartctlCommandResult());
            }

            if (arguments.Contains("-t short -j", StringComparison.OrdinalIgnoreCase) ||
                arguments.Contains("-t long -j", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(StartResponse ?? new SmartctlCommandResult());

            if (arguments.Contains("-l selftest -j", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(StatusResponse ?? new SmartctlCommandResult());

            return Task.FromResult(new SmartctlCommandResult());
        }

        public Task<IReadOnlyList<SmartDiskDevice>> ListDevicesAsync()
            => Task.FromResult<IReadOnlyList<SmartDiskDevice>>(ScanDevices);
    }

    // =====================
    // Helpers
    // =====================

    private static SmartDiskDevice Device(string name = "/dev/sda", string type = "sat",
        string approximate = "HDD")
    {
        return new SmartDiskDevice
        {
            Name = name,
            InfoName = name,
            Type = type,
            ApproximateDiskType = approximate,
            Protocol = "SATA"
        };
    }

    private static string ValidSmartJson() => @"{
        ""smart_status"": { ""passed"": true },
        ""ata_smart_attributes"": { ""table"": [] },
        ""temperature"": { ""current"": 40 }
    }";

    private static string SelfTestLogJson(string statusText) => @"{
        ""ata_smart_self_test_log"": {
            ""standard"": {
                ""table"": [
                    {
                        ""type"": { ""string"": ""Short offline"" },
                        ""status"": { ""string"": """ + statusText + @""" },
                        ""remaining"": ""0%"",
                        ""lifetime_hours"": 12345
                    }
                ]
            }
        }
    }";

    private static string StartJson(string message = "Testing has begun.") => @"{
        ""smartctl"": {
            ""messages"": [ { ""string"": """ + message + @""" } ],
            ""exit_status"": 0
        }
    }";

    private static async Task<string> RunAnalyzeAndGetFirstArg(SmartDiskDevice device)
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = 0, StandardOutput = ValidSmartJson() }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        await service.AnalyzeDiskAsync(device);

        return runner.RunArguments.First(a => a.Contains("-a -j", StringComparison.OrdinalIgnoreCase));
    }

    // =====================
    // A. Bitmask (S.1)
    // =====================

    [Fact]
    public void ExitCode0_FlagsNone()
    {
        var result = new SmartctlCommandResult { ExitCode = 0 };

        Assert.Equal(SmartctlExitFlags.None, result.ExitFlags);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(1, SmartctlExitFlags.CommandLineOrInternalError)]
    [InlineData(2, SmartctlExitFlags.DeviceOpenOrIdentityFailed)]
    [InlineData(4, SmartctlExitFlags.SmartCommandOrChecksumError)]
    [InlineData(8, SmartctlExitFlags.SmartStatusFailed)]
    [InlineData(16, SmartctlExitFlags.PrefailAttributeThreshold)]
    [InlineData(32, SmartctlExitFlags.PastOrUsageAttributeFailure)]
    [InlineData(64, SmartctlExitFlags.ErrorLogContainsErrors)]
    [InlineData(128, SmartctlExitFlags.SelfTestLogContainsErrors)]
    public void ExitCode_MapsToSingleBit(int exitCode, SmartctlExitFlags expected)
    {
        var result = new SmartctlCommandResult { ExitCode = exitCode };

        Assert.Equal(expected, result.ExitFlags);
    }

    [Fact]
    public void ExitCode3_CombinesBits1And2()
    {
        var result = new SmartctlCommandResult { ExitCode = 3 };

        Assert.True(result.ExitFlags.HasFlag(SmartctlExitFlags.CommandLineOrInternalError));
        Assert.True(result.ExitFlags.HasFlag(SmartctlExitFlags.DeviceOpenOrIdentityFailed));
        Assert.Equal((SmartctlExitFlags)3, result.ExitFlags);
    }

    [Fact]
    public void ExitCode192_CombinesBits64And128()
    {
        var result = new SmartctlCommandResult { ExitCode = 192 };

        Assert.True(result.ExitFlags.HasFlag(SmartctlExitFlags.ErrorLogContainsErrors));
        Assert.True(result.ExitFlags.HasFlag(SmartctlExitFlags.SelfTestLogContainsErrors));
        Assert.Equal((SmartctlExitFlags)192, result.ExitFlags);
    }

    [Fact]
    public void ExitCodeNegative_NotInterpretedAsFlags()
    {
        // -1 no es un bitmask smartctl válido (proceso no ejecutado)
        var result = new SmartctlCommandResult { ExitCode = -1 };

        Assert.Equal(SmartctlExitFlags.None, result.ExitFlags);
        Assert.True(result.HasInvocationFailure);
    }

    [Fact]
    public void TimedOut_HasInvocationFailure()
    {
        var result = new SmartctlCommandResult { TimedOut = true, ExitCode = -1 };

        Assert.True(result.HasInvocationFailure);
    }

    [Fact]
    public void Exit1_HasInvocationFailure()
    {
        var result = new SmartctlCommandResult { ExitCode = 1 };

        Assert.True(result.HasInvocationFailure);
    }

    [Fact]
    public void Exit2_HasInvocationFailure()
    {
        var result = new SmartctlCommandResult { ExitCode = 2 };

        Assert.True(result.HasInvocationFailure);
    }

    [Fact]
    public void Exit4_HasSmartCommandFailure_NotInvocation()
    {
        var result = new SmartctlCommandResult { ExitCode = 4 };

        Assert.True(result.HasSmartCommandFailure);
        Assert.False(result.HasInvocationFailure);
    }

    [Fact]
    public void Exit8_NotInvocationFailure()
    {
        var result = new SmartctlCommandResult { ExitCode = 8 };

        Assert.False(result.HasInvocationFailure);
        Assert.False(result.HasSmartCommandFailure);
        Assert.True(result.HasHealthOrLogFindings);
    }

    [Fact]
    public void Exit128_NotInvocationFailure()
    {
        var result = new SmartctlCommandResult { ExitCode = 128 };

        Assert.False(result.HasInvocationFailure);
        Assert.False(result.HasSmartCommandFailure);
        Assert.True(result.HasHealthOrLogFindings);
    }

    // =====================
    // B. JSON exit_status (S.1)
    // =====================

    [Theory]
    [InlineData("0", 0)]
    [InlineData("8", 8)]
    [InlineData("128", 128)]
    [InlineData("192", 192)]
    public void TryGetSmartctlExitStatus_Numeric(string value, int expected)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            $@"{{ ""smartctl"": {{ ""exit_status"": {value} }} }}");

        var status = SmartctlParser.TryGetSmartctlExitStatus(doc.RootElement);

        Assert.Equal(expected, status);
    }

    [Fact]
    public void TryGetSmartctlExitStatus_LegacyObjectValue_Tolerated()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            @"{ ""smartctl"": { ""exit_status"": { ""value"": 8 } } }");

        var status = SmartctlParser.TryGetSmartctlExitStatus(doc.RootElement);

        Assert.Equal(8, status);
    }

    [Fact]
    public void TryGetSmartctlExitStatus_Missing_ReturnsNull()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(@"{ ""smartctl"": { } }");

        Assert.Null(SmartctlParser.TryGetSmartctlExitStatus(doc.RootElement));
    }

    [Fact]
    public void TryGetSmartctlExitStatus_InvalidFormat_ReturnsNull()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(
            @"{ ""smartctl"": { ""exit_status"": ""ocho"" } }");

        Assert.Null(SmartctlParser.TryGetSmartctlExitStatus(doc.RootElement));
    }

    [Fact]
    public void ParseStartShortTestJson_Exit3_InterpretedAsBits()
    {
        // 3 = bits 1|2 combinados, no un "código 3" especial
        var json = @"{
            ""smartctl"": {
                ""messages"": [],
                ""exit_status"": 3
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.False(result.Started);
        Assert.Equal(SmartTestStatus.FailedToStart, result.Status);
        Assert.Equal(3, result.SmartctlExitStatus);
    }

    [Fact]
    public void ParseStartShortTestJson_Exit8_DoesNotFailStart()
    {
        // Bit 3 (hallazgo de salud) no impide iniciar el test
        var json = @"{
            ""smartctl"": {
                ""messages"": [ { ""string"": ""Testing has begun."" } ],
                ""exit_status"": 8
            }
        }";

        var result = SmartctlParser.ParseStartShortTestJson(json);

        Assert.True(result.Started);
        Assert.Equal(SmartTestStatus.InProgress, result.Status);
    }

    // =====================
    // C. Transporte -d TYPE (S.1)
    // =====================

    [Theory]
    [InlineData("sat")]
    [InlineData("nvme")]
    [InlineData("sntjmicron")]
    public async Task Analyze_IncludesDeviceType(string type)
    {
        var arguments = await RunAnalyzeAndGetFirstArg(Device(type: type));

        Assert.Contains($"-d {type}", arguments);
        Assert.Contains("/dev/sda", arguments);
    }

    [Fact]
    public async Task Analyze_EmptyType_OmitsDashD()
    {
        var arguments = await RunAnalyzeAndGetFirstArg(Device(type: string.Empty));

        Assert.DoesNotContain("-d", arguments);
    }

    [Fact]
    public async Task Analyze_ApproximateDiskType_NotUsedAsTransport()
    {
        // USB/SSD son clasificaciones visuales CATTECH, no argumentos -d válidos
        var arguments = await RunAnalyzeAndGetFirstArg(Device(type: "scsi", approximate: "USB"));

        Assert.Contains("-d scsi", arguments);
        Assert.DoesNotContain("-d usb", arguments);
        Assert.DoesNotContain("-d USB", arguments);
    }

    [Fact]
    public async Task Analyze_SsdApproximate_NotUsedAsTransport()
    {
        var arguments = await RunAnalyzeAndGetFirstArg(Device(type: "nvme", approximate: "SSD"));

        Assert.Contains("-d nvme", arguments);
        Assert.DoesNotContain("-d ssd", arguments);
        Assert.DoesNotContain("-d SSD", arguments);
    }

    [Fact]
    public async Task StartShortTest_PreservesDeviceType()
    {
        var runner = new FakeSmartctlRunner
        {
            StartResponse = new SmartctlCommandResult { ExitCode = 0, StandardOutput = StartJson() }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);

        await service.StartShortTestAsync(Device(type: "sat"));

        var arguments = runner.RunArguments.First(a => a.Contains("-t short -j", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("-d sat", arguments);
    }

    [Fact]
    public async Task StartExtendedTest_PreservesDeviceType()
    {
        var runner = new FakeSmartctlRunner
        {
            StartResponse = new SmartctlCommandResult { ExitCode = 0, StandardOutput = StartJson() }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);

        await service.StartExtendedTestAsync(Device(type: "sat"));

        var arguments = runner.RunArguments.First(a => a.Contains("-t long -j", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("-d sat", arguments);
    }

    [Fact]
    public async Task CheckStatus_PreservesSessionDeviceType()
    {
        var runner = new FakeSmartctlRunner
        {
            StatusResponse = new SmartctlCommandResult
            {
                ExitCode = 0,
                StandardOutput = SelfTestLogJson("Self-test routine in progress")
            }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            SmartctlDeviceType = "sntjmicron",
            Status = SmartTestStatus.InProgress
        };

        await service.CheckStatusAsync(session);

        var arguments = runner.RunArguments.First(a => a.Contains("-l selftest -j", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("-d sntjmicron", arguments);
    }

    [Fact]
    public async Task GetLatestResult_PreservesDeviceType()
    {
        var runner = new FakeSmartctlRunner
        {
            StatusResponse = new SmartctlCommandResult
            {
                ExitCode = 0,
                StandardOutput = SelfTestLogJson("Completed without error")
            }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);

        await service.GetLatestResultAsync(Device(type: "sat"));

        var arguments = runner.RunArguments.First(a => a.Contains("-l selftest -j", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("-d sat", arguments);
    }

    [Fact]
    public void Report_PersistsSmartctlDeviceType()
    {
        var report = SmartctlParser.ParseSmartJson(ValidSmartJson(), Device(type: "sat"), "smartctl 7.4");

        Assert.Equal("sat", report.SmartctlDeviceType);
    }

    [Fact]
    public async Task Session_PersistsSmartctlDeviceType()
    {
        var runner = new FakeSmartctlRunner
        {
            StartResponse = new SmartctlCommandResult { ExitCode = 0, StandardOutput = StartJson() }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);

        var session = await service.StartShortTestAsync(Device(type: "sntjmicron"));

        Assert.Equal("sntjmicron", session.SmartctlDeviceType);
    }

    [Fact]
    public void Report_LegacyWithoutType_DefaultsEmpty()
    {
        var report = SmartctlParser.ParseSmartJson(ValidSmartJson(), Device(type: string.Empty), "smartctl 7.4");

        Assert.Equal(string.Empty, report.SmartctlDeviceType);
    }

    [Fact]
    public void Session_LegacyWithoutType_DefaultsEmpty()
    {
        var session = new SmartTestSession();

        Assert.Equal(string.Empty, session.SmartctlDeviceType);
    }

    [Fact]
    public async Task ViewModelFallback_UsesSmartctlDeviceTypeRaw()
    {
        var runner = new FakeSmartctlRunner
        {
            StartResponse = new SmartctlCommandResult { ExitCode = 0, StandardOutput = StartJson() }
        };
        var testService = new SmartTestService(runner, AppContext.BaseDirectory);
        var diskService = new SmartDiskService(runner, AppContext.BaseDirectory);
        var vm = new SmartDiskViewModel(runner, diskService, testService);

        var report = new SmartDiskReport
        {
            Device = "/dev/sda",
            DeviceName = "/dev/sda",
            DeviceType = "USB", // clasificación visual: NO debe usarse como -d
            SmartctlDeviceType = "sat"
        };
        vm.Reports.Add(report);
        vm.SelectedReport = report;

        await vm.StartShortTestCommand.ExecuteAsync(null);

        var arguments = runner.RunArguments.First(a => a.Contains("-t short -j", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("-d sat", arguments);
        Assert.DoesNotContain("-d usb", arguments);
    }

    // =====================
    // D. Resultados no cero (S.1)
    // =====================

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    public async Task Analyze_HealthBitsWithValidJson_Parsed(int exitCode)
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = exitCode, StandardOutput = ValidSmartJson() }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(Device(type: "sat"));

        Assert.True(report.IsAnalysisSuccessful);
        Assert.NotEqual(SmartHealthStatus.NotAvailable, report.HealthStatus);
    }

    [Fact]
    public async Task CheckStatus_Exit128_WithErrorLog_ParsesCompletedWithError()
    {
        var runner = new FakeSmartctlRunner
        {
            StatusResponse = new SmartctlCommandResult
            {
                ExitCode = 128,
                StandardOutput = SelfTestLogJson("Completed: read failure")
            }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);
        var session = new SmartTestSession
        {
            Device = "/dev/sda",
            SmartctlDeviceType = "sat",
            Status = SmartTestStatus.InProgress
        };

        await service.CheckStatusAsync(session);

        Assert.True(session.LastCheckSucceeded);
        Assert.Equal(SmartTestStatus.CompletedWithError, session.Status);
    }

    [Fact]
    public async Task GetLatestResult_Exit128_WithValidLog_ReturnsResult()
    {
        var runner = new FakeSmartctlRunner
        {
            StatusResponse = new SmartctlCommandResult
            {
                ExitCode = 128,
                StandardOutput = SelfTestLogJson("Completed: read failure")
            }
        };
        var service = new SmartTestService(runner, AppContext.BaseDirectory);

        var result = await service.GetLatestResultAsync(Device(type: "sat"));

        Assert.NotNull(result);
        Assert.Equal(SmartTestStatus.CompletedWithError, result!.Status);
    }

    [Fact]
    public async Task Analyze_Bit2_WithPartialJson_UnknownInconclusive()
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = 4, StandardOutput = ValidSmartJson() }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(Device(type: "sat"));

        Assert.False(report.IsAnalysisSuccessful);
        Assert.Equal(SmartHealthStatus.Unknown, report.HealthStatus);
        Assert.NotEqual(SmartHealthStatus.Good, report.HealthStatus);
        Assert.NotEqual(SmartHealthStatus.Critical, report.HealthStatus);
        Assert.Contains("parcial", report.HealthSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Analyze_Bit2_WithoutJson_NotAvailable()
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = 4, StandardOutput = string.Empty }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(Device(type: "sat"));

        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
        Assert.False(report.IsAnalysisSuccessful);
    }

    [Fact]
    public async Task Analyze_Bit0_WithoutJson_NotAvailable()
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = 1, StandardOutput = string.Empty }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(Device(type: "sat"));

        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
        Assert.False(report.IsAnalysisSuccessful);
    }

    [Fact]
    public async Task Analyze_Bit1_WithoutJson_NotAvailable()
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = 2, StandardOutput = string.Empty }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(Device(type: "sat"));

        Assert.Equal(SmartHealthStatus.NotAvailable, report.HealthStatus);
        Assert.False(report.IsAnalysisSuccessful);
    }

    [Fact]
    public async Task Analyze_InaccessibleDisk_DoesNotAbortOthers()
    {
        var runner = new FakeSmartctlRunner
        {
            ScanDevices =
            [
                Device("/dev/sda", "sat"),
                Device("/dev/nvme0n1", "nvme")
            ],
            AnalyzeResponse = new SmartctlCommandResult
            {
                ExitCode = 2,
                StandardOutput = string.Empty,
                StandardError = "open failed"
            }
        };
        runner.AnalyzeResponseByDevice["/dev/nvme0n1"] = new SmartctlCommandResult
        {
            ExitCode = 0,
            StandardOutput = ValidSmartJson()
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var result = await service.AnalyzeAllDisksAsync();

        Assert.Equal(2, result.Reports.Count);
        var first = result.Reports.First(r => r.Device == "/dev/sda");
        Assert.Equal(SmartHealthStatus.NotAvailable, first.HealthStatus);
        var second = result.Reports.First(r => r.Device == "/dev/nvme0n1");
        Assert.True(second.IsAnalysisSuccessful);
        Assert.NotEqual(SmartHealthStatus.NotAvailable, second.HealthStatus);
        Assert.All(runner.RunArguments.Where(a => a.Contains("/dev/sda")), a =>
            Assert.Contains("-d sat", a));
        Assert.All(runner.RunArguments.Where(a => a.Contains("/dev/nvme0n1")), a =>
            Assert.Contains("-d nvme", a));
    }

    [Fact]
    public async Task Analyze_UsesInjectedRunner_NoRealSmartctl()
    {
        var runner = new FakeSmartctlRunner
        {
            AnalyzeResponse = new SmartctlCommandResult { ExitCode = 0, StandardOutput = ValidSmartJson() }
        };
        var service = new SmartDiskService(runner, AppContext.BaseDirectory);

        var report = await service.AnalyzeDiskAsync(Device(type: "sat"));

        Assert.True(report.IsAnalysisSuccessful);
        Assert.NotEmpty(runner.RunArguments);
    }
}
