using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DumpCheckServiceTests
{
    private sealed class FakeLease : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeGuard : IDumpCheckFileGuard
    {
        public bool Allow { get; set; } = true;
        public bool ThrowAccessDenied { get; set; }
        public int Calls { get; private set; }
        public FakeLease? Lease { get; private set; }

        public IDisposable? TryAcquire(string directory, string dumpPath)
        {
            Calls++;
            if (ThrowAccessDenied)
                throw new UnauthorizedAccessException("Synthetic denial.");
            if (!Allow)
                return null;
            Lease = new FakeLease();
            return Lease;
        }
    }

    private sealed class FakeRunner : IDumpCheckProcessRunner
    {
        public int Calls { get; private set; }
        public string? Tool { get; private set; }
        public string? Dump { get; private set; }
        public Action? OnRun { get; set; }

        public DumpCheckProcessResult Next { get; set; } = new()
        {
            Started = true,
            ExitCode = 0,
            Stdout = "BUGCHECK_CODE: 124\nFinished dump check"
        };

        public Task<DumpCheckProcessResult> RunAsync(
            string toolPath, string dumpPath, TimeSpan timeout, CancellationToken token)
        {
            OnRun?.Invoke();
            Calls++;
            Tool = toolPath;
            Dump = dumpPath;
            Assert.Equal(TimeSpan.FromSeconds(60), timeout);
            return Task.FromResult(Next);
        }
    }

    private static DumpCheckService Create(FakeRunner runner, bool hasTool = true, bool hasDump = true,
        FakeGuard? guard = null, Func<string, FileAttributes>? attributes = null)
    {
        var locator = new DumpCheckToolLocator(
            () => @"C:\Program Files",
            () => @"C:\Program Files (x86)",
            _ => hasTool, isWindows: true, is64Bit: true);

        return new DumpCheckService(
            locator, runner, @"C:\Windows\Minidump", _ => hasDump,
            attributes ?? (_ => FileAttributes.Normal), isWindows: true,
            fileGuard: guard ?? new FakeGuard());
    }

    [Fact]
    public async Task NotConfirmed_NeverStartsExternalProcess()
    {
        var fake = new FakeRunner();
        var result = await Create(fake).VerifyDumpAsync("synthetic.dmp", false);

        Assert.Equal(DumpCheckExecutionStatus.ConfirmationRequired, result.Status);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task PathTraversal_IsRejectedBeforeToolLaunch()
    {
        var fake = new FakeRunner();
        var result = await Create(fake).VerifyDumpAsync(@"..\secret.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.InvalidFileName, result.Status);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task MissingTool_IsGraceful()
    {
        var fake = new FakeRunner();
        var result = await Create(fake, hasTool: false).VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.ToolUnavailable, result.Status);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task MissingDump_IsGraceful()
    {
        var fake = new FakeRunner();
        var result = await Create(fake, hasDump: false).VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.DumpMissing, result.Status);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task ConfirmedValidDump_ParsesWithoutLeakingToolOutput()
    {
        var fake = new FakeRunner();
        var result = await Create(fake).VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(1, fake.Calls);
        Assert.Equal(DumpCheckExecutionStatus.Completed, result.Status);
        Assert.NotNull(result.Summary);
        Assert.Equal(0x124u, result.Summary.BugCheckCode);
        Assert.DoesNotContain("Windows\\Minidump", result.Message);
        Assert.EndsWith("synthetic.dmp", fake.Dump, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Timeout_IsReportedAndNotMisclassifiedAsCompleted()
    {
        var fake = new FakeRunner
        {
            Next = new DumpCheckProcessResult { Started = true, TimedOut = true }
        };
        var result = await Create(fake).VerifyDumpAsync("synthetic.dmp", true);
        Assert.Equal(DumpCheckExecutionStatus.TimedOut, result.Status);
    }

    [Fact]
    public void ProcessStartInfo_NeverUsesShellOrSymbolServer()
    {
        var start = DumpCheckProcessRunner.BuildStartInfo(
            @"C:\Windows Kits\10\Debuggers\x64\dumpchk.exe",
            @"C:\Windows\Minidump\dump with spaces.dmp");

        Assert.False(start.UseShellExecute);
        Assert.True(start.CreateNoWindow);
        Assert.True(start.RedirectStandardOutput);
        Assert.True(start.RedirectStandardError);
        Assert.Single(start.ArgumentList);
        Assert.Equal(@"C:\Windows\Minidump\dump with spaces.dmp", start.ArgumentList[0]);
        Assert.Equal("", start.Environment["_NT_SYMBOL_PATH"]);
        Assert.Equal("", start.Environment["_NT_ALT_SYMBOL_PATH"]);
    }
    [Fact]
    public async Task DirectoryReparsePoint_RejectsBeforeLaunching()
    {
        var runner = new FakeRunner();
        var result = await Create(runner, attributes: path =>
            path.EndsWith("Minidump", StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : FileAttributes.Normal).VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.InvalidFileName, result.Status);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task FileGuardDenial_DoesNotLaunchDumpChk()
    {
        var guard = new FakeGuard { Allow = false };
        var runner = new FakeRunner();
        var result = await Create(runner, guard: guard)
            .VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.InvalidFileName, result.Status);
        Assert.Equal(1, guard.Calls);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task FileGuardPermissionFailure_DoesNotLaunchDumpChk()
    {
        var guard = new FakeGuard { ThrowAccessDenied = true };
        var runner = new FakeRunner();
        var result = await Create(runner, guard: guard)
            .VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.AccessDenied, result.Status);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task FileLease_RemainsHeldUntilExternalRunnerFinishes()
    {
        var guard = new FakeGuard();
        var runner = new FakeRunner
        {
            OnRun = () => Assert.False(guard.Lease!.Disposed)
        };

        var result = await Create(runner, guard: guard)
            .VerifyDumpAsync("synthetic.dmp", true);

        Assert.Equal(DumpCheckExecutionStatus.Completed, result.Status);
        Assert.Equal(1, runner.Calls);
        Assert.True(guard.Lease!.Disposed);
    }

}
