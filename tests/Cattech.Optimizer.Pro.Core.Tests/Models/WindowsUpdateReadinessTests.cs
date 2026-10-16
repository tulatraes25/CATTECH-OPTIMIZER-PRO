using Cattech.Optimizer.Pro.Core.Models.Repair;
using Cattech.Optimizer.Pro.Infrastructure.Repair;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class WindowsUpdateReadinessTests
{
    private sealed class FakeReader : IWindowsUpdateServiceReader
    {
        private readonly Func<IReadOnlyList<WindowsServiceRawState>> _load;
        public int Calls { get; private set; }

        public FakeReader(params WindowsServiceRawState[] entries)
            : this(() => entries)
        {
        }

        public FakeReader(Func<IReadOnlyList<WindowsServiceRawState>> load)
            => _load = load;

        public IReadOnlyList<WindowsServiceRawState> Read()
        {
            Calls++;
            return _load();
        }
    }

    private static WindowsServiceRawState S(string name, string state, string start)
        => new() { Name = name, State = state, StartMode = start };

    [Fact]
    public void ObservedFourServices_DoesNotClaimWindowsUpdateHealthy()
    {
        var fake = new FakeReader(
            S("wuauserv", "Stopped", "Manual"),
            S("BITS", "Running", "Auto"),
            S("cryptsvc", "Running", "Auto"),
            S("UsoSvc", "Stopped", "Manual"));
        var service = new WindowsUpdateReadinessService(fake);

        var result = service.Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.Observed, result.Status);
        Assert.Equal(4, result.Services.Count);
        Assert.Equal(WindowsServiceRunState.Stopped, result.Services[0].State);
        Assert.Contains("no confirma", result.Message);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public void EmptyServiceList_IsPartialNotHealthy()
    {
        var reader = new FakeReader();
        var result = new WindowsUpdateReadinessService(reader).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.Partial, result.Status);
        Assert.Equal(4, result.Services.Count);
        Assert.All(result.Services,
            x => Assert.Equal(WindowsServiceRunState.NotFound, x.State));
    }

    [Fact]
    public void MissingOneService_IsPartial()
    {
        var reader = new FakeReader(
            S("wuauserv", "Running", "Auto"),
            S("BITS", "Stopped", "Manual"),
            S("cryptsvc", "Running", "Auto"));
        var result = new WindowsUpdateReadinessService(reader).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.Partial, result.Status);
        Assert.Equal(WindowsServiceRunState.NotFound, result.Services[3].State);
    }

    [Fact]
    public void UnknownStateAndStartMode_AreNotMisrepresented()
    {
        var reader = new FakeReader(
            S("wuauserv", "Paused", "System"),
            S("BITS", "Running", "Auto"),
            S("cryptsvc", "Running", "Auto"),
            S("UsoSvc", "Stopped", "Manual"));
        var result = new WindowsUpdateReadinessService(reader).Inspect();

        Assert.Equal(WindowsServiceRunState.Other, result.Services[0].State);
        Assert.Equal("N/D", result.Services[0].StartMode);
    }

    [Fact]
    public void DeniedAccess_IsNotMisreportedAsNoUpdates()
    {
        var reader = new FakeReader(() => throw new UnauthorizedAccessException("Secret host"));
        var result = new WindowsUpdateReadinessService(reader).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.AccessDenied, result.Status);
        Assert.Empty(result.Services);
        Assert.DoesNotContain("Secret host", result.Message);
    }

    [Fact]
    public void ReadError_IsNeutral()
    {
        var reader = new FakeReader(() => throw new IOException("Sensitive path"));
        var result = new WindowsUpdateReadinessService(reader).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.ReadError, result.Status);
        Assert.Empty(result.Services);
        Assert.DoesNotContain("Sensitive", result.Message);
    }

    [Fact]
    public void NullProviderSnapshot_IsReadErrorWithoutLeakingContext()
    {
        var fake = new FakeReader(() => null!);

        var result = new WindowsUpdateReadinessService(fake).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.ReadError, result.Status);
        Assert.Empty(result.Services);
        Assert.Contains("inconsistente", result.Message);
    }

    [Fact]
    public void RepeatedServiceNamesWithConflictingStates_AreNotAcceptedAsObserved()
    {
        var fake = new FakeReader(
            S("wuauserv", "Running", "Auto"),
            S("WUAUSERV", "Stopped", "Disabled"),
            S("BITS", "Running", "Auto"),
            S("cryptsvc", "Running", "Auto"),
            S("UsoSvc", "Running", "Auto"));

        var result = new WindowsUpdateReadinessService(fake).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.ReadError, result.Status);
        Assert.Empty(result.Services);
        Assert.DoesNotContain("observó", result.Message);
    }

    [Fact]
    public void UnexpectedProviderService_DoesNotBecomeACompleteObservation()
    {
        var fake = new FakeReader(
            S("wuauserv", "Running", "Auto"),
            S("BITS", "Running", "Auto"),
            S("cryptsvc", "Running", "Auto"),
            S("UsoSvc", "Running", "Auto"),
            S(@"C:\Users\SyntheticPrivatePath", "Stopped", "Disabled"));

        var result = new WindowsUpdateReadinessService(fake).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.ReadError, result.Status);
        Assert.Empty(result.Services);
        Assert.DoesNotContain("SyntheticPrivatePath", result.Message);
    }

    [Fact]
    public void NullProviderRow_IsReadErrorRatherThanAThrownUiException()
    {
        var fake = new FakeReader(
            S("wuauserv", "Running", "Auto"), null!);

        var result = new WindowsUpdateReadinessService(fake).Inspect();

        Assert.Equal(WindowsUpdateReadinessStatus.ReadError, result.Status);
        Assert.Empty(result.Services);
    }

    [Fact]
    public void NonWindows_NeverQueriesProvider()
    {
        var reader = new FakeReader(S("wuauserv", "Running", "Auto"));
        var service = new WindowsUpdateReadinessService(reader, requireWindows: true);

        if (OperatingSystem.IsWindows())
            return;

        var result = service.Inspect();
        Assert.Equal(WindowsUpdateReadinessStatus.UnsupportedPlatform, result.Status);
        Assert.Equal(0, reader.Calls);
    }
}
