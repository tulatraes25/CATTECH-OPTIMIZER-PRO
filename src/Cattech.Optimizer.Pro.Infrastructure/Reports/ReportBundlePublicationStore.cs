using System.Security.Cryptography;
using System.Text.Json;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// A new INTERNAL, client-free publication format. No service-case attribution
/// and no use by the current WPF HTML/PDF/index path. A matching manifest is
/// an integrity assertion, NOT evidence of customer or device provenance.
/// </summary>
internal enum ReportBundleInspectionStatus
{
    Current,
    Absent,
    Corrupt,
    Unsafe
}

internal sealed record ReportBundleInspection(
    ReportBundleInspectionStatus Status, Guid? BundleId = null, bool HasPdf = false);

internal sealed record ReportBundleManifest(
    int SchemaVersion, Guid BundleId, string Scope,
    string HtmlSha256, long HtmlBytes,
    string? PdfSha256, long? PdfBytes);

/// <summary>
/// C2-D4a: a directory rename is the one logical commit point for a
/// client-free HTML + optional PDF + checksum manifest bundle. Readers MUST
/// use InspectAsync and disregard *.pending directories and legacy artifacts.
/// Crash durability is not claimed and ordinary report history is unchanged.
/// </summary>
internal sealed class ReportBundlePublicationStore
{
    private const int HtmlLimit = 8 * 1024 * 1024;
    private const int PdfLimit = 64 * 1024 * 1024;
    private const int ManifestLimit = 4096;
    private const string Scope = "ClientFree";
    private const string HtmlFile = "report.html";
    private const string PdfFile = "report.pdf";
    private const string ManifestFile = "manifest.json";
    private readonly string _root;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        MaxDepth = 8
    };
    private static readonly HashSet<string> RequiredFields = new(
        ["schemaVersion", "bundleId", "scope", "htmlSha256", "htmlBytes",
            "pdfSha256", "pdfBytes"], StringComparer.Ordinal);

    internal ReportBundlePublicationStore(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        _root = Path.Combine(Path.GetFullPath(baseDirectory), "reports", "bundles");
    }

    /// <summary>
    /// No caller-provided filenames, case IDs, reviewer names or source paths.
    /// Incomplete pending directories are never published or auto-recovered.
    /// </summary>
    internal async Task<Guid> PublishClientFreeAsync(
        ReadOnlyMemory<byte> html, ReadOnlyMemory<byte>? pdf = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (html.IsEmpty || html.Length > HtmlLimit)
            throw new ArgumentOutOfRangeException(nameof(html));
        if (pdf is { } supplied && (supplied.IsEmpty || supplied.Length > PdfLimit))
            throw new ArgumentOutOfRangeException(nameof(pdf));

        // Private copies avoid mutation between hashing and the disk write.
        var htmlBytes = html.ToArray();
        var pdfBytes = pdf?.ToArray();
        var id = Guid.NewGuid();
        var pending = Path.Combine(_root, ".pending-" + id.ToString("N"));
        var published = Path.Combine(_root, "bundle-" + id.ToString("N"));
        if (!IsSafeDirectoryChain(_root))
            throw new IOException("Unsafe report publication ancestry.");
        Directory.CreateDirectory(_root);
        if (!IsSafeDirectoryChain(_root) || Directory.Exists(pending)
            || Directory.Exists(published))
            throw new IOException("Unsafe or occupied report publication directory.");

        Directory.CreateDirectory(pending);
        var committed = false;
        try
        {
            if (!IsSafeDirectoryChain(pending))
                throw new IOException("Unsafe private staging directory.");

            await WriteNewAsync(Path.Combine(pending, HtmlFile),
                htmlBytes, cancellationToken).ConfigureAwait(false);
            if (pdfBytes is not null)
                await WriteNewAsync(Path.Combine(pending, PdfFile),
                    pdfBytes, cancellationToken).ConfigureAwait(false);

            var manifest = new ReportBundleManifest(1, id, Scope,
                Digest(htmlBytes), htmlBytes.LongLength,
                pdfBytes is null ? null : Digest(pdfBytes),
                pdfBytes?.LongLength);
            var serialized = JsonSerializer.SerializeToUtf8Bytes(manifest, Options);
            await WriteNewAsync(Path.Combine(pending, ManifestFile),
                serialized, cancellationToken).ConfigureAwait(false);

            // Detect our own incomplete/stale staging before making it visible.
            var validated = await InspectDirectoryAsync(pending, id,
                cancellationToken).ConfigureAwait(false);
            if (validated.Status != ReportBundleInspectionStatus.Current)
                throw new InvalidDataException("Private report stage failed validation.");
            cancellationToken.ThrowIfCancellationRequested();

            // Pending and published are sibling directories on one volume.
            // This rename is the only visibility commit for bundle-aware readers.
            Directory.Move(pending, published);
            committed = true;
            return id;
        }
        finally
        {
            if (!committed)
                TryCleanupOwnPending(pending);
        }
    }

    /// <summary>
    /// Strict, non-creating and read-only. Does NOT interpret a legacy HTML,
    /// an orphaned pending directory, or a checksum as case authorization.
    /// </summary>
    internal async Task<ReportBundleInspection> InspectAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (id == Guid.Empty)
            return new(ReportBundleInspectionStatus.Absent);
        if (!IsSafeDirectoryChain(_root))
            return new(ReportBundleInspectionStatus.Unsafe);
        var published = Path.Combine(_root, "bundle-" + id.ToString("N"));
        if (!Directory.Exists(published))
            return new(ReportBundleInspectionStatus.Absent);
        return await InspectDirectoryAsync(published, id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ReportBundleInspection> InspectDirectoryAsync(
        string directory, Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSafeDirectoryChain(directory))
            return new(ReportBundleInspectionStatus.Unsafe);
        try
        {
            var manifestPath = Path.Combine(directory, ManifestFile);
            var json = await ReadBoundedAsync(manifestPath, ManifestLimit,
                cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(ReportBundleInspectionStatus.Corrupt);
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in document.RootElement.EnumerateObject())
            {
                if (!properties.Add(member.Name)
                    || !RequiredFields.Contains(member.Name))
                    return new(ReportBundleInspectionStatus.Corrupt);
            }
            if (!properties.SetEquals(RequiredFields))
                return new(ReportBundleInspectionStatus.Corrupt);
            var manifest = document.Deserialize<ReportBundleManifest>(Options);
            if (manifest is null || manifest.SchemaVersion != 1
                || manifest.BundleId != id
                || !string.Equals(manifest.Scope, Scope, StringComparison.Ordinal)
                || !IsSha256(manifest.HtmlSha256)
                || manifest.HtmlBytes is < 1 or > HtmlLimit)
                return new(ReportBundleInspectionStatus.Corrupt);
            var hasPdf = manifest.PdfSha256 is not null
                && manifest.PdfBytes is not null;
            if ((manifest.PdfSha256 is null) != (manifest.PdfBytes is null)
                || (hasPdf && (!IsSha256(manifest.PdfSha256!)
                    || manifest.PdfBytes is < 1 or > PdfLimit)))
                return new(ReportBundleInspectionStatus.Corrupt);

            var expected = hasPdf
                ? new HashSet<string>([HtmlFile, PdfFile, ManifestFile],
                    StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>([HtmlFile, ManifestFile],
                    StringComparer.OrdinalIgnoreCase);
            var entries = Directory.GetFileSystemEntries(directory);
            if (entries.Length != expected.Count
                || entries.Any(p => !expected.Remove(Path.GetFileName(p)))
                || expected.Count != 0)
                return new(ReportBundleInspectionStatus.Corrupt);

            var html = await ReadBoundedAsync(Path.Combine(directory, HtmlFile),
                HtmlLimit, cancellationToken).ConfigureAwait(false);
            if (html.LongLength != manifest.HtmlBytes
                || Digest(html) != manifest.HtmlSha256)
                return new(ReportBundleInspectionStatus.Corrupt);
            if (hasPdf)
            {
                var pdf = await ReadBoundedAsync(Path.Combine(directory, PdfFile),
                    PdfLimit, cancellationToken).ConfigureAwait(false);
                if (pdf.LongLength != manifest.PdfBytes
                    || Digest(pdf) != manifest.PdfSha256)
                    return new(ReportBundleInspectionStatus.Corrupt);
            }
            return new(ReportBundleInspectionStatus.Current, id, hasPdf);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new(ReportBundleInspectionStatus.Unsafe);
        }
        catch (IOException)
        {
            return new(ReportBundleInspectionStatus.Corrupt);
        }
        catch (JsonException)
        {
            return new(ReportBundleInspectionStatus.Corrupt);
        }
        catch (ArgumentException)
        {
            return new(ReportBundleInspectionStatus.Corrupt);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        string path, int maxLength, CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Reparse point in bundle.");
        await using var stream = new FileStream(path, FileMode.Open,
            FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        if (stream.Length < 1 || stream.Length > maxLength)
            throw new InvalidDataException("Invalid bundle artifact size.");
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken)
            .ConfigureAwait(false);
        return bytes;
    }

    private static async Task WriteNewAsync(
        string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSafeDirectoryChain(string path)
    {
        for (string? node = Path.GetFullPath(path); node is not null;
             node = Path.GetDirectoryName(node))
        {
            if (Directory.Exists(node)
                && (File.GetAttributes(node) & FileAttributes.ReparsePoint) != 0)
                return false;
            if (File.Exists(node) && !Directory.Exists(node))
                return false;
        }
        return true;
    }

    private static string Digest(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(c =>
            c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static void TryCleanupOwnPending(string directory)
    {
        // NEVER recurse into an unexpected directory or follow a reparse point.
        try
        {
            if (!Directory.Exists(directory) || !IsSafeDirectoryChain(directory))
                return;
            foreach (var name in new[] { HtmlFile, PdfFile, ManifestFile })
            {
                var file = Path.Combine(directory, name);
                if (File.Exists(file)
                    && (File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                    File.Delete(file);
            }
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (IOException) { /* Preserve the originating error. */ }
        catch (UnauthorizedAccessException) { }
    }
}
