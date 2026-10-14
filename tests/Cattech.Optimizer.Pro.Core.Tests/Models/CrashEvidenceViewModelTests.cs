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

    private sealed class FakeDumpCheck : IDumpCheckService
    {
        public int Calls { get; private set; }
        public string? ReceivedFile { get; private set; }
        public bool Confirmed { get; private set; }

        public Task<DumpCheckExecutionResult> VerifyDumpAsync(
            string dumpFileName, bool technicianConfirmed, CancellationToken cancellationToken = default)
        {
            Calls++;
            ReceivedFile = dumpFileName;
            Confirmed = technicianConfirmed;
            return Task.FromResult(new DumpCheckExecutionResult
            {
                Status = DumpCheckExecutionStatus.Completed,
                Message = "Comprobación completada.",
                Summary = new DumpCheckResult
                {
                    BugCheckCode = 0x124,
                    Readability = DumpCheckReadability.ProbablyReadable,
                    Message = "Comprobación completada."
                }
            });
        }
    }

    [Fact]
    public async Task VerifyDump_CannotExecuteOnStartupNorWithoutSelection()
    {
        var reader = new FakeDumpCheck();
        var confirmations = 0;
        var vm = new CrashEvidenceViewModel(
            new FakeDumpService(), new FakeEventService(), new FakeDecoder(),
            reader, _ => { confirmations++; return true; });

        Assert.False(vm.VerifySelectedDumpCommand.CanExecute(null));
        Assert.Equal(0, reader.Calls);
        Assert.Equal(0, confirmations);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.False(vm.VerifySelectedDumpCommand.CanExecute(null));
        Assert.Equal(0, reader.Calls);
        Assert.Equal(0, confirmations);
    }

    [Fact]
    public async Task VerifyDump_DeclinedConfirmation_NeverCallsService()
    {
        var reader = new FakeDumpCheck();
        var prompts = 0;
        var vm = new CrashEvidenceViewModel(
            new FakeDumpService(), new FakeEventService(), new FakeDecoder(),
            reader, _ => { prompts++; return false; });

        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedDump = Assert.Single(vm.Dumps);
        Assert.True(vm.VerifySelectedDumpCommand.CanExecute(null));

        await vm.VerifySelectedDumpCommand.ExecuteAsync(null);

        Assert.Equal(1, prompts);
        Assert.Equal(0, reader.Calls);
        Assert.Contains("cancelada", vm.DumpCheckStatusText);
    }

    [Fact]
    public async Task VerifyDump_Approved_UsesSelectedBasenameOnlyAndShowsSafeSummary()
    {
        var reader = new FakeDumpCheck();
        var prompts = 0;
        var vm = new CrashEvidenceViewModel(
            new FakeDumpService(), new FakeEventService(), new FakeDecoder(),
            reader, name => { prompts++; return name == "synthetic.dmp"; });

        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedDump = Assert.Single(vm.Dumps);

        await vm.VerifySelectedDumpCommand.ExecuteAsync(null);

        Assert.Equal(1, prompts);
        Assert.Equal(1, reader.Calls);
        Assert.True(reader.Confirmed);
        Assert.Equal("synthetic.dmp", reader.ReceivedFile);
        Assert.Contains("0x00000124", vm.DumpCheckStatusText);
        Assert.DoesNotContain("C:\\", vm.DumpCheckStatusText);
        Assert.False(vm.IsBusy);
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
