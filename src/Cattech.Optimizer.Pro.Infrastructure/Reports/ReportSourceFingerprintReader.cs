using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// An independently captured, in-memory fingerprint of a persisted source.
/// This is NOT a customer/equipment association, signed provenance, or permission
/// to add the source to an attributed report. Never serialize it to public HTML.
/// </summary>
internal sealed record SourceFileFingerprint(
    string SourceKind, string SourceRecordId, string RelativePath, string Sha256);

internal enum SourceFingerprintFailure
{
    None,
    UnsafeScope,
    Unavailable,
    InvalidPayload,
    Oversized
}

internal sealed record SourceFingerprintInspection(
    SourceFingerprintFailure Failure, SourceFileFingerprint? Fingerprint)
{
    public bool HasReadableSnapshot =>
        Failure == SourceFingerprintFailure.None && Fingerprint is not null;
}

/// <summary>
/// Unique local source ID resolution ONLY. A match never proves technician
/// consent, machine identity or permission to associate a named client.
/// </summary>
internal enum SourceIdResolutionStatus
{
    FoundUnique,
    NotFound,
    Ambiguous,
    Unverifiable,
    UnsafeScope
}

internal sealed record SourceIdResolution(
    SourceIdResolutionStatus Status, SourceFileFingerprint? Fingerprint = null);

/// <summary>
/// SPEC #92 C2-B1: read ONLY the known CATTECH local source stores, on the SAME
/// exclusive-content Windows file handle for ID extraction and SHA-256.
/// No writes, sidecar approval, export authorization, or physical attestation.
/// A later C2-B2 journal and C2-D export must open/revalidate again at use time.
/// </summary>
internal sealed class ReportSourceFingerprintReader
{
    private const int MaxSourceBytes = 4 * 1024 * 1024;
    private readonly string _dataRoot;

    internal ReportSourceFingerprintReader(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _dataRoot = Path.GetFullPath(Path.Combine(baseDirectory, "data"));
    }

    /// <summary>
    /// Resolve a source ID to one approved local JSON file, refusing every
    /// ambiguous or unreadable history. Never choose by timestamp, serial,
    /// enumeration order or first matching short ID.
    /// A returned fingerprint is ephemeral and requires revalidation.
    /// </summary>
    internal async Task<SourceIdResolution> ResolveUniqueAsync(
        string sourceKind, string sourceRecordId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return new(SourceIdResolutionStatus.Unverifiable);

        var location = sourceKind switch
        {
            "Diagnostic" => ("diagnostics", "diagnostic-*.json"),
            "Startup" => ("startup-analysis", "startup-analysis-*.json"),
            "SmartAnalysis" => ("smart-reports", "smart-analysis-*.json"),
            "SmartSelfTest" => ("smart-tests", "smart-test-*.json"),
            _ => default
        };
        if (location.Item1 is null
            || !ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(sourceRecordId)
            || _dataRoot.StartsWith(@"\\", StringComparison.Ordinal))
            return new(SourceIdResolutionStatus.UnsafeScope);

        try
        {
            var folder = Path.Combine(_dataRoot, location.Item1);
            if (!Directory.Exists(folder))
                return new(SourceIdResolutionStatus.NotFound);
            if (!HasNoReparsePoints(folder))
                return new(SourceIdResolutionStatus.UnsafeScope);

            const int maxCandidates = 4096;
            var files = Directory.EnumerateFiles(folder, location.Item2,
                    SearchOption.TopDirectoryOnly)
                .Take(maxCandidates + 1).ToArray();
            if (files.Length > maxCandidates)
                return new(SourceIdResolutionStatus.Unverifiable);

            SourceFileFingerprint? selected = null;
            foreach (var file in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = location.Item1 + "/" + Path.GetFileName(file);
                var found = await InspectAsync(sourceKind, relative,
                    cancellationToken).ConfigureAwait(false);
                // A corrupt, inaccessible or redirected sibling may hide a
                // matching ID. Never silently skip it.
                if (!found.HasReadableSnapshot)
                    return new(SourceIdResolutionStatus.Unverifiable);
                if (!string.Equals(found.Fingerprint!.SourceRecordId,
                        sourceRecordId, StringComparison.Ordinal))
                    continue;
                if (selected is not null)
                    return new(SourceIdResolutionStatus.Ambiguous);
                selected = found.Fingerprint;
            }
            return selected is null
                ? new(SourceIdResolutionStatus.NotFound)
                : new(SourceIdResolutionStatus.FoundUnique, selected);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException)
        {
            return new(SourceIdResolutionStatus.Unverifiable);
        }
        catch (UnauthorizedAccessException)
        {
            return new(SourceIdResolutionStatus.Unverifiable);
        }
        catch (ArgumentException)
        {
            return new(SourceIdResolutionStatus.UnsafeScope);
        }
    }

    internal async Task<SourceFingerprintInspection> InspectAsync(
        string sourceKind, string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return new(SourceFingerprintFailure.Unavailable, null);

        var directory = sourceKind switch
        {
            "Diagnostic" => "diagnostics/",
            "Startup" => "startup-analysis/",
            "SmartAnalysis" => "smart-reports/",
            "SmartSelfTest" => "smart-tests/",
            _ => null
        };
        if (directory is null
            || !ServiceSourceBindingClaimPolicy.IsSafeRelativePath(relativePath)
            || !relativePath.StartsWith(directory, StringComparison.Ordinal)
            || !relativePath.EndsWith(".json", StringComparison.Ordinal))
            return new(SourceFingerprintFailure.UnsafeScope, null);

        try
        {
            // This first path check is lexical, not proof of actual file identity.
            // Reject network roots; the journal is intentionally local-only.
            if (_dataRoot.StartsWith(@"\\", StringComparison.Ordinal))
                return new(SourceFingerprintFailure.UnsafeScope, null);

            var root = _dataRoot.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(_dataRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return new(SourceFingerprintFailure.UnsafeScope, null);

            // Fail closed if any ancestor or the target is a known reparse point.
            // This pre-open check alone is NOT a TOCTOU guarantee.
            if (!HasNoReparsePoints(fullPath))
                return new(SourceFingerprintFailure.UnsafeScope, null);

            // Other writers cannot modify this file while the handle is open.
            // Both identity validation and digest use this exact open handle.
            await using var file = new FileStream(fullPath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
            if (!MatchesResolvedWindowsPath(file.SafeFileHandle, fullPath))
                return new(SourceFingerprintFailure.UnsafeScope, null);

            if (file.Length <= 0 || file.Length > MaxSourceBytes)
                return new(SourceFingerprintFailure.Oversized, null);

            var buffer = new byte[(int)file.Length];
            try
            {
                await file.ReadExactlyAsync(buffer.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
                using var document = JsonDocument.Parse(buffer.AsMemory());
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return new(SourceFingerprintFailure.InvalidPayload, null);

                // Never select the "last" duplicate ID. Old reports have
                // case-insensitive 8-char JSON IDs; require exactly one.
                var matches = 0;
                string? sourceId = null;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "id",
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    matches++;
                    if (property.Value.ValueKind != JsonValueKind.String)
                        return new(SourceFingerprintFailure.InvalidPayload, null);
                    sourceId = property.Value.GetString();
                }
                if (matches != 1
                    || !ServiceSourceBindingClaimPolicy.IsSafeSourceRecordId(sourceId))
                    return new(SourceFingerprintFailure.InvalidPayload, null);

                var hash = Convert.ToHexString(SHA256.HashData(buffer));
                return new(SourceFingerprintFailure.None,
                    new SourceFileFingerprint(sourceKind, sourceId!,
                        relativePath, hash));
            }
            finally
            {
                // Keep private source content out of long-lived buffers.
                Array.Clear(buffer, 0, buffer.Length);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return new(SourceFingerprintFailure.InvalidPayload, null);
        }
        catch (IOException)
        {
            return new(SourceFingerprintFailure.Unavailable, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new(SourceFingerprintFailure.Unavailable, null);
        }
        catch (ArgumentException)
        {
            return new(SourceFingerprintFailure.UnsafeScope, null);
        }
    }

    private static bool HasNoReparsePoints(string fullPath)
    {
        if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            return false;

        for (var directory = Path.GetDirectoryName(fullPath);
            directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                return false;
            if (string.Equals(directory, Path.GetPathRoot(directory),
                    StringComparison.OrdinalIgnoreCase))
                break;
        }
        return true;
    }

    private static bool MatchesResolvedWindowsPath(
        SafeFileHandle handle, string expectedPath)
    {
        // Kernel32 returns the real final handle target (not FileStream.Name).
        // Fail closed on an unexpected alias/junction/symlink; unknown path
        // representations are refused rather than guessed.
        var target = new StringBuilder(4096);
        var size = GetFinalPathNameByHandle(handle, target,
            (uint)target.Capacity, 0);
        if (size == 0 || size >= (uint)target.Capacity)
            return false;
        const string longPrefix = @"\\?\";
        var actual = target.ToString();
        if (!actual.StartsWith(longPrefix, StringComparison.Ordinal))
            return false;
        actual = actual[longPrefix.Length..];
        return !actual.StartsWith("UNC\\", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actual, expectedPath,
                StringComparison.OrdinalIgnoreCase);
    }

    // The mutable path buffer is small, bounded and only used on Windows.
#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW",
        CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle handle, StringBuilder filePath, uint capacity, uint flags);
#pragma warning restore SYSLIB1054
}
