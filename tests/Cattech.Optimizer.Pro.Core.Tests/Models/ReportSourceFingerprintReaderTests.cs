using System.Security.Cryptography;
using System.Text;
using Cattech.Optimizer.Pro.Infrastructure.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// All data is synthetic and disposable. No real diagnostics or Windows
/// services/repair tools are invoked by this C2-B1 read-only gate.
/// </summary>
public sealed class ReportSourceFingerprintReaderTests
{
    [Theory]
    [InlineData("Diagnostic", "diagnostics")]
    [InlineData("Startup", "startup-analysis")]
    [InlineData("SmartAnalysis", "smart-reports")]
    [InlineData("SmartSelfTest", "smart-tests")]
    public async Task KnownLocalSource_UsesSameBytesForRecordIdAndSha(
        string sourceKind, string folder)
    {
        var root = NewRoot();
        try
        {
            var json = "{\"id\":\"ABCDEF12\",\"computerName\":\"PRIVATE_SYNTHETIC_MARKER\"}";
            var relative = folder + "/synthetic.json";
            await WriteSourceAsync(root, relative, json);
            var reader = new ReportSourceFingerprintReader(root);
            var actual = await reader.InspectAsync(sourceKind, relative);
            Assert.True(actual.HasReadableSnapshot);
            Assert.Equal(SourceFingerprintFailure.None, actual.Failure);
            Assert.Equal("ABCDEF12", actual.Fingerprint!.SourceRecordId);
            Assert.Equal(relative, actual.Fingerprint.RelativePath);
            Assert.Equal(sourceKind, actual.Fingerprint.SourceKind);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(json))), actual.Fingerprint.Sha256);
            Assert.DoesNotContain("PRIVATE_SYNTHETIC_MARKER",
                actual.Fingerprint.ToString());
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Diagnostic", "smart-reports/foreign.json")]
    [InlineData("SMART", "smart-reports/foreign.json")]
    [InlineData("Startup", "startup-analysis/../diagnostics/foreign.json")]
    [InlineData("Diagnostic", "diagnostics/CON.json")]
    [InlineData("Diagnostic", "diagnostics/report.json\n")]
    public async Task UntrustedKindOrPath_IsRejectedWithoutWritingAnyFiles(
        string kind, string relative)
    {
        var root = NewRoot();
        try
        {
            var result = await new ReportSourceFingerprintReader(root)
                .InspectAsync(kind, relative);
            Assert.Equal(SourceFingerprintFailure.UnsafeScope, result.Failure);
            Assert.Null(result.Fingerprint);
            Assert.False(Directory.Exists(Path.Combine(root, "data")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("{\"id\":\"ABCD1234\",\"Id\":\"DEADBEEF\"}")]
    [InlineData("{\"id\":null}")]
    [InlineData("{\"id\":\"invalid/path\"}")]
    [InlineData("{\"id\":\"ABCD1234\"")]
    [InlineData("[]")]
    public async Task InvalidOrDuplicateSourceIdentity_IsNeverFingerprinted(string json)
    {
        var root = NewRoot();
        try
        {
            await WriteSourceAsync(root, "diagnostics/input.json", json);
            var result = await new ReportSourceFingerprintReader(root)
                .InspectAsync("Diagnostic", "diagnostics/input.json");
            Assert.Equal(SourceFingerprintFailure.InvalidPayload, result.Failure);
            Assert.Null(result.Fingerprint);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AlteredSourceBytes_GenerateDifferentFingerprintNotSilentReuse()
    {
        var root = NewRoot();
        try
        {
            const string relative = "smart-reports/synthetic.json";
            await WriteSourceAsync(root, relative,
                "{\"id\":\"DEADBEEF\",\"value\":1}");
            var reader = new ReportSourceFingerprintReader(root);
            var first = await reader.InspectAsync("SmartAnalysis", relative);
            Assert.True(first.HasReadableSnapshot);
            await WriteSourceAsync(root, relative,
                "{\"id\":\"DEADBEEF\",\"value\":2}");
            var second = await reader.InspectAsync("SmartAnalysis", relative);
            Assert.True(second.HasReadableSnapshot);
            Assert.Equal(first.Fingerprint!.SourceRecordId,
                second.Fingerprint!.SourceRecordId);
            Assert.NotEqual(first.Fingerprint.Sha256, second.Fingerprint.Sha256);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OversizedInputAndMissingFile_NeverCertifySnapshot()
    {
        var root = NewRoot();
        try
        {
            var reader = new ReportSourceFingerprintReader(root);
            var missing = await reader.InspectAsync("Diagnostic",
                "diagnostics/missing.json");
            Assert.Equal(SourceFingerprintFailure.Unavailable, missing.Failure);
            await WriteSourceAsync(root, "diagnostics/huge.json",
                "{\"id\":\"ABCDEF12\",\"x\":\"" + new string('a', 4 * 1024 * 1024)
                + "\"}");
            var huge = await reader.InspectAsync("Diagnostic",
                "diagnostics/huge.json");
            Assert.Equal(SourceFingerprintFailure.Oversized, huge.Failure);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task AnotherWindowsProcessHoldingExclusiveFile_DoesNotYieldProof()
    {
        var root = NewRoot();
        try
        {
            var path = await WriteSourceAsync(root, "diagnostics/locked.json",
                "{\"id\":\"ABCD1234\"}");
            await using var locked = new FileStream(path, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None);
            var result = await new ReportSourceFingerprintReader(root)
                .InspectAsync("Diagnostic", "diagnostics/locked.json");
            Assert.Equal(SourceFingerprintFailure.Unavailable, result.Failure);
            Assert.Null(result.Fingerprint);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cancellation_IsPropagatedAndDoesNotInventFingerprint()
    {
        var root = NewRoot();
        try
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new ReportSourceFingerprintReader(root).InspectAsync(
                    "Diagnostic", "diagnostics/a.json", cancelled.Token));
            Assert.False(Directory.Exists(Path.Combine(root, "data")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "cattech-c2b1-fingerprint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task<string> WriteSourceAsync(
        string root, string relative, string json)
    {
        var path = Path.Combine(root, "data",
            relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
