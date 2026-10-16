using Cattech.Optimizer.Pro.Core.Models.Repair;
using Cattech.Optimizer.Pro.Infrastructure.Repair;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SystemRepairPreflightTests
{
    private sealed class FakeProbe : ISystemRepairEnvironmentProbe
    {
        public bool IsSupportedWindows { get; set; } = true;
        public bool IsElevatedAdministrator { get; set; } = true;
        public string SystemDirectory { get; set; } = @"C:\Windows\System32";
        public bool ToolExists { get; set; } = true;
        public bool DirectoryIsReparse { get; set; }
        public bool FileIsReparse { get; set; }
        public bool ThrowAccessDenied { get; set; }
        public int FileChecks { get; private set; }

        public bool FileExists(string path)
        {
            FileChecks++;
            return ToolExists;
        }

        public FileAttributes GetAttributes(string path)
        {
            if (ThrowAccessDenied)
                throw new UnauthorizedAccessException("Synthetic access denial.");

            if (path.Equals(SystemDirectory, StringComparison.OrdinalIgnoreCase))
                return FileAttributes.Directory
                    | (DirectoryIsReparse ? FileAttributes.ReparsePoint : 0);

            return FileIsReparse ? FileAttributes.ReparsePoint : FileAttributes.Normal;
        }
    }

    private static SystemRepairPreflightService Create(FakeProbe probe)
        => new(new SystemRepairPlanService(), probe);

    [Theory]
    [InlineData(SystemRepairOperation.DismCheckHealth, "dism.exe")]
    [InlineData(SystemRepairOperation.DismScanHealth, "dism.exe")]
    [InlineData(SystemRepairOperation.SfcVerifyOnly, "sfc.exe")]
    [InlineData(SystemRepairOperation.DismRestoreHealth, "dism.exe")]
    [InlineData(SystemRepairOperation.SfcScanNow, "sfc.exe")]
    public void SupportedPlans_RequireReviewAndNeverAuthorizeExecution(
        SystemRepairOperation operation, string toolName)
    {
        var probe = new FakeProbe();
        var result = Create(probe).Inspect(operation);

        Assert.Equal(SystemRepairPreflightStatus.ReviewRequired, result.Status);
        Assert.Equal(toolName, result.ExecutableName);
        Assert.Equal(Path.Combine(probe.SystemDirectory, toolName), result.TrustedToolPath);
        Assert.True(result.RequiresExplicitConfirmation);
        Assert.False(result.ExecutionAuthorized);
        Assert.Equal(1, probe.FileChecks);
    }

    [Fact]
    public void UnsupportedWindows_NeverChecksTools()
    {
        var probe = new FakeProbe { IsSupportedWindows = false };
        var result = Create(probe).Inspect(SystemRepairOperation.SfcScanNow);

        Assert.Equal(SystemRepairPreflightStatus.UnsupportedPlatform, result.Status);
        Assert.False(result.ExecutionAuthorized);
        Assert.Equal(0, probe.FileChecks);
    }

    [Fact]
    public void NonAdmin_NeverChecksTools()
    {
        var probe = new FakeProbe { IsElevatedAdministrator = false };
        var result = Create(probe).Inspect(SystemRepairOperation.DismScanHealth);

        Assert.Equal(SystemRepairPreflightStatus.AdministratorRequired, result.Status);
        Assert.False(result.ExecutionAuthorized);
        Assert.Equal(0, probe.FileChecks);
    }

    [Fact]
    public void MissingTool_DoesNotAuthorizeExecution()
    {
        var result = Create(new FakeProbe { ToolExists = false })
            .Inspect(SystemRepairOperation.DismCheckHealth);

        Assert.Equal(SystemRepairPreflightStatus.TrustedToolMissing, result.Status);
        Assert.False(result.ExecutionAuthorized);
    }

    [Fact]
    public void ReparseSystemDirectory_IsRejectedBeforeFileProbe()
    {
        var probe = new FakeProbe { DirectoryIsReparse = true };
        var result = Create(probe).Inspect(SystemRepairOperation.SfcVerifyOnly);

        Assert.Equal(SystemRepairPreflightStatus.UntrustedToolPath, result.Status);
        Assert.Equal(0, probe.FileChecks);
        Assert.False(result.ExecutionAuthorized);
    }

    [Fact]
    public void ReparseExecutable_IsRejected()
    {
        var result = Create(new FakeProbe { FileIsReparse = true })
            .Inspect(SystemRepairOperation.SfcScanNow);

        Assert.Equal(SystemRepairPreflightStatus.UntrustedToolPath, result.Status);
        Assert.False(result.ExecutionAuthorized);
    }

    [Fact]
    public void PrivilegeOrFileProbeFailure_IsConservative()
    {
        var result = Create(new FakeProbe { ThrowAccessDenied = true })
            .Inspect(SystemRepairOperation.DismRestoreHealth);

        Assert.Equal(SystemRepairPreflightStatus.ProbeUnavailable, result.Status);
        Assert.False(result.ExecutionAuthorized);
        Assert.Equal(string.Empty, result.TrustedToolPath);
    }
}
