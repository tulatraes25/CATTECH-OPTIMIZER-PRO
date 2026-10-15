using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class DumpCheckToolLocatorTests
{
    [Fact]
    public void Locator_UsesTrustedSdkDirectoryOnly()
    {
        var available = Path.Combine(@"C:\Program Files (x86)", "Windows Kits",
            "10", "Debuggers", "x64", "dumpchk.exe");
        var locator = new DumpCheckToolLocator(
            () => @"C:\Program Files",
            () => @"C:\Program Files (x86)",
            path => path == available,
            isWindows: true,
            is64Bit: true);

        Assert.Equal(available, locator.Locate());
    }

    [Fact]
    public void Locator_MissingToolReturnsNullNotPathFallback()
    {
        var locator = new DumpCheckToolLocator(
            () => @"C:\Program Files",
            () => @"C:\Program Files (x86)",
            _ => false, isWindows: true, is64Bit: true);

        Assert.Null(locator.Locate());
    }

    [Fact]
    public void Locator_NonWindowsNeverProbes()
    {
        var called = false;
        var locator = new DumpCheckToolLocator(
            () => "any", () => "any",
            _ => { called = true; return true; },
            isWindows: false, is64Bit: true);

        Assert.Null(locator.Locate());
        Assert.False(called);
    }

    [Theory]
    [InlineData(@"..\secret.dmp")]
    [InlineData(@"C:\Windows\Minidump\a.dmp")]
    [InlineData(@"\\server\share\dump.dmp")]
    [InlineData(@"path/dump.dmp")]
    [InlineData("malware.exe")]
    [InlineData("")]
    public void Policy_RejectsPathInjection(string candidate)
    {
        Assert.False(DumpCheckPathPolicy.TryResolve(
            @"C:\Windows\Minidump", candidate, _ => true, out _));
    }

    [Fact]
    public void Policy_AcceptsExistingDumpBasename()
    {
        var expected = Path.Combine(@"C:\Windows\Minidump", "101326-01.dmp");
        var accepted = DumpCheckPathPolicy.TryResolve(
            @"C:\Windows\Minidump", "101326-01.dmp",
            value => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase),
            out var resolved);

        Assert.True(accepted);
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Policy_RejectsNonexistentDump()
    {
        Assert.False(DumpCheckPathPolicy.TryResolve(
            @"C:\Windows\Minidump", "not-found.dmp", _ => false, out _));
    }
}
