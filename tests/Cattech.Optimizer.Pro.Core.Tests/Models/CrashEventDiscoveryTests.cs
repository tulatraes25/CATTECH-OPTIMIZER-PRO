using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class CrashEventDiscoveryTests
{
    private sealed class FakeEnumerator : ICrashEventXmlEnumerator
    {
        private readonly Func<IEnumerable<string>> _fetch;
        public FakeEnumerator(params string[] xml) : this(() => xml) { }
        public FakeEnumerator(Func<IEnumerable<string>> fetch) => _fetch = fetch;
        public IEnumerable<string> Enumerate(int maxEvents) => _fetch();
    }

    private static string Wer(string code, string source = "Microsoft-Windows-WER-SystemErrorReporting")
        => $"""
           <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
             <System>
               <Provider Name="{source}"/>
               <EventID>1001</EventID>
               <TimeCreated SystemTime="2026-10-13T15:30:00.000Z"/>
             </System>
             <EventData>
               <Data Name="param1">{code} (0x1, 0x2)</Data>
               <Data Name="param2">C:\Users\PRIVATE\MEMORY.DMP</Data>
             </EventData>
           </Event>
           """;

    private static string Power(string code)
        => $"""
           <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
             <System>
               <Provider Name="Microsoft-Windows-Kernel-Power"/>
               <EventID>41</EventID>
               <TimeCreated SystemTime="2026-10-13T16:30:00Z"/>
             </System>
             <EventData><Data Name="BugcheckCode">{code}</Data></EventData>
           </Event>
           """;

    [Fact]
    public void ParseWer1001_DecodesBugCheckAndStripsSensitiveData()
    {
        Assert.True(CrashEventXmlParser.TryParse(Wer("0x00000124"), out var info));
        Assert.NotNull(info);
        Assert.Equal(1001, info.EventId);
        Assert.Equal(0x124u, info.BugCheckCode);
        Assert.Equal("0x00000124", info.HexCode);
        Assert.Equal(TimeSpan.Zero, info.TimeCreatedUtc!.Value.Offset);
        Assert.DoesNotContain("PRIVATE", info.Source);
        Assert.DoesNotContain("C:\\", info.Source);
    }

    [Fact]
    public void ParseKernelPower41_AcceptsNonzeroDecimalBugcheck()
    {
        Assert.True(CrashEventXmlParser.TryParse(Power("292"), out var info));
        Assert.NotNull(info);
        Assert.Equal(41, info.EventId);
        Assert.Equal(0x124u, info.BugCheckCode);
    }

    [Fact]
    public void ParseKernelPower41_ZeroBugCheckIsNotABsod()
    {
        Assert.False(CrashEventXmlParser.TryParse(Power("0"), out _));
    }

    [Fact]
    public void Parse1001FromOtherProvider_IsNotClassifiedAsBugCheck()
    {
        Assert.False(CrashEventXmlParser.TryParse(Wer("0x124", "Unrelated-Provider"), out _));
    }

    [Fact]
    public void XmlWithDtd_IsRejected()
    {
        const string xml = "<!DOCTYPE Event [<!ENTITY leak SYSTEM 'file:///C:/secret'>]><Event>&leak;</Event>";
        Assert.False(CrashEventXmlParser.TryParse(xml, out _));
    }

    [Fact]
    public void MalformedXml_IsIgnoredWithoutCrash()
    {
        Assert.False(CrashEventXmlParser.TryParse("<Event><System>", out _));
    }

    [Fact]
    public void NoEvents_DoesNotImplyNeverHadCrashes()
    {
        var result = new CrashEventDiscoveryService(new FakeEnumerator()).Discover();
        Assert.Equal(CrashEventDiscoveryStatus.NoEvents, result.Status);
        Assert.Contains("No implica", result.Message);
    }

    [Fact]
    public void EventsAreBoundedAndDoNotPersistXml()
    {
        var service = new CrashEventDiscoveryService(
            new FakeEnumerator(Wer("0x0000009F"), Power("159"), Wer("0x00000124")),
            maxEvents: 2);
        var result = service.Discover();

        Assert.Equal(CrashEventDiscoveryStatus.Found, result.Status);
        Assert.Equal(2, result.Events.Count);
        Assert.True(result.IsTruncated);
        Assert.Equal(2, result.EventsScanned);
        Assert.All(result.Events, entry => Assert.DoesNotContain("PRIVATE", entry.Source));
    }

    [Fact]
    public void DeniedEventLog_IsNotReportedAsNoEvents()
    {
        var result = new CrashEventDiscoveryService(
            new FakeEnumerator(() => throw new UnauthorizedAccessException())).Discover();
        Assert.Equal(CrashEventDiscoveryStatus.AccessDenied, result.Status);
    }

    [Theory]
    [InlineData("0x0000000A", 10u)]
    [InlineData("10", 10u)]
    public void ParseCodePrefix_AcceptsKnownFormats(string value, uint expected)
    {
        Assert.True(CrashEventXmlParser.TryParseCodePrefix(value, out var code));
        Assert.Equal(expected, code);
    }
}
