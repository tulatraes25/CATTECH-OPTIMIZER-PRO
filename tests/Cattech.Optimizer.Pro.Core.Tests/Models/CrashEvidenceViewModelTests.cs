using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class CrashEvidenceViewModelTests
{
    private sealed class FakeDumpService : ICrashDumpDiscoveryService
    {
        public int Calls { get; private set; }
        public CrashDumpDiscoveryResult Discover()
        {
            Calls++;
            return new CrashDumpDiscoveryResult
            {
                Status = CrashDumpDiscoveryStatus.Found,
                Dumps = new[] { new CrashDumpEntry
                {
                    FileName = "synthetic.dmp",
                    SizeBytes = 1024,
                    LastModifiedUtc = DateTimeOffset.UtcNow
                }},
                FilesObserved = 1
            };
        }
    }

    private sealed class FakeEventService : ICrashEventDiscoveryService
    {
        public int Calls { get; private set; }
        public CrashEventDiscoveryResult Discover()
        {
            Calls++;
            return new CrashEventDiscoveryResult
            {
                Status = CrashEventDiscoveryStatus.Found,
                Events = new[]
                {
                    new CrashEventEvidence
                    {
                        BugCheckCode = 0x124,
                        EventId = 1001,
                        Source = "Microsoft-Windows-WER-SystemErrorReporting"
                    }
                }
            };
        }
    }

    private sealed class FakeDecoder : IBugCheckDecoder
    {
        public BugCheckDecodeResult Decode(uint code)
            => new()
            {
                Code = code,
                IsKnown = true,
                Name = "WHEA_UNCORRECTABLE_ERROR",
                Description = "Error de hardware."
            };
    }

    [Fact]
    public async Task ViewModel_DoesNotScanUntilUserRequests()
    {
        var dumps = new FakeDumpService();
        var events = new FakeEventService();
        var vm = new CrashEvidenceViewModel(dumps, events, new FakeDecoder());

        Assert.Equal(0, dumps.Calls);
        Assert.Equal(0, events.Calls);
        Assert.Empty(vm.Dumps);
        Assert.Empty(vm.Events);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(1, dumps.Calls);
        Assert.Equal(1, events.Calls);
        Assert.Single(vm.Dumps);
        Assert.Single(vm.Events);
        Assert.Equal("0x00000124", vm.Events[0].Code);
        Assert.Equal("WHEA_UNCORRECTABLE_ERROR", vm.Events[0].Name);
        Assert.False(vm.IsBusy);
        Assert.Contains("No se modificó Windows", vm.StatusText);
    }
}
