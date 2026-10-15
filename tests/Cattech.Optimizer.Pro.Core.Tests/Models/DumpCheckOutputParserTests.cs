using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DumpCheckOutputParserTests
{
    [Fact]
    public void SuccessfulKernelSummary_ParsesCodeAndFourParametersAsHex()
    {
        const string output = """
            Windows Kernel Mini Dump
            BUGCHECK_CODE:  1e
            BUGCHECK_P1: ffffffffc0000420
            BUGCHECK_P2: fffff8004dbab02a
            BUGCHECK_P3: 0  BUGCHECK_P4: fffff8003a6d5f20
            MODULE_NAME: ntoskrnl
            IMAGE_NAME: ntoskrnl.exe
            Finished dump check
            """;

        var parsed = DumpCheckOutputParser.Parse(output);

        Assert.Equal(DumpCheckReadability.ProbablyReadable, parsed.Readability);
        Assert.Equal(0x1Eu, parsed.BugCheckCode);
        Assert.Equal("0x0000001E", parsed.BugCheckHexCode);
        Assert.Equal(4, parsed.Parameters.Count);
        Assert.Equal(0xFFFFFFFFC0000420ul, parsed.Parameters[1]);
        Assert.Equal(0ul, parsed.Parameters[3]);
        Assert.DoesNotContain("ntoskrnl", parsed.Message);
    }

    [Fact]
    public void BugCheckCodeWithoutPrefix_IsHexNotDecimal()
    {
        var value = DumpCheckOutputParser.Parse("BUGCHECK_CODE: 10\nFinished dump check");
        Assert.Equal(0x10u, value.BugCheckCode);
    }

    [Fact]
    public void InvalidFormat_DoesNotClaimValidEvenWithSuccessMarker()
    {
        var value = DumpCheckOutputParser.Parse(
            "Could not match Dump File signature\nFinished dump check");

        Assert.Equal(DumpCheckReadability.InvalidFormat, value.Readability);
        Assert.Null(value.BugCheckCode);
    }

    [Fact]
    public void MissingSuccessMarker_IsIndeterminate()
    {
        var value = DumpCheckOutputParser.Parse("BUGCHECK_CODE: 124");
        Assert.Equal(DumpCheckReadability.Indeterminate, value.Readability);
        Assert.Null(value.BugCheckCode);
    }

    [Fact]
    public void NoOutput_IsIndeterminate()
    {
        Assert.Equal(DumpCheckReadability.Indeterminate, DumpCheckOutputParser.Parse(null).Readability);
    }

    [Fact]
    public void LargeOutput_IsSafelyRejected()
    {
        var value = DumpCheckOutputParser.Parse(new string('X', 256_001));
        Assert.Equal(DumpCheckReadability.Indeterminate, value.Readability);
    }

    [Fact]
    public void RawPrivatePathsAreNeverIncludedInResult()
    {
        var raw = @"BUGCHECK_CODE: 124
UserProfile: C:\Users\PrivateUser
MODULE_NAME: SomeModule
Finished dump check";
        var value = DumpCheckOutputParser.Parse(raw);

        Assert.Equal(DumpCheckReadability.ProbablyReadable, value.Readability);
        Assert.DoesNotContain("PrivateUser", value.Message);
        Assert.DoesNotContain("SomeModule", value.Message);
        Assert.NotNull(value.BugCheckCode);
    }
}
