using System.Text.RegularExpressions;

namespace Cattech.Optimizer.Pro.Core.Models.Reports;

/// <summary>
/// A technician-reviewed association claim. This untrusted DTO is NEVER proof
/// of a machine's physical identity or of the content of an on-disk file.
/// Future Infrastructure C2-B must provide independently verified identity,
/// digest, storage safety and immutable local audit before it can be used.
/// </summary>
public sealed record ServiceSourceBindingClaim(
    int SchemaVersion,
    Guid BindingId,
    Guid ServiceCaseId,
    string SourceKind,
    string SourceRecordId,
    string SourceSha256,
    string RelativeSourcePath,
    DateTimeOffset ConfirmedAtUtc,
    string ConfirmationMethod);

public enum ServiceSourceBindingFailure
{
    None,
    InvalidClaim,
    CaseMismatch,
    KindMismatch,
    RecordMismatch,
    DigestMismatch,
    PathMismatch,
    UntrustedConfirmation,
    DuplicateClaim,
    InvalidSourceDigest
}

/// <summary>
/// Pure, side-effect-free syntactic/semantic preflight for future audited
/// associations. Passing it is NECESSARY, not SUFFICIENT, for export.
/// </summary>
public static class ServiceSourceBindingClaimPolicy
{
    public const int SchemaVersion = 1;
    public const string ExplicitReview = "ExplicitTechnicianReview";

    private static readonly Regex Sha256Pattern =
        new(@"\A[a-fA-F0-9]{64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex RelativePathPattern =
        new(@"\A[A-Za-z0-9_][A-Za-z0-9_./-]*\z", RegexOptions.CultureInvariant);
    private static readonly Regex SourceIdPattern =
        new(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);

    public static ServiceSourceBindingFailure Validate(
        ServiceSourceBindingClaim? claim,
        Guid currentCaseId, string expectedSourceKind,
        string expectedSourceRecordId, string verifiedSourceSha256,
        string verifiedRelativePath,
        IReadOnlyCollection<ServiceSourceBindingClaim>? allClaims = null)
    {
        if (claim is null || claim.SchemaVersion != SchemaVersion
            || claim.BindingId == Guid.Empty || claim.ServiceCaseId == Guid.Empty
            || !IsSafeSourceRecordId(claim.SourceRecordId)
            || claim.ConfirmedAtUtc == default
            || string.IsNullOrWhiteSpace(claim.SourceKind))
            return ServiceSourceBindingFailure.InvalidClaim;

        if (claim.ServiceCaseId != currentCaseId || currentCaseId == Guid.Empty)
            return ServiceSourceBindingFailure.CaseMismatch;
        if (!string.Equals(claim.SourceKind, expectedSourceKind,
                StringComparison.Ordinal))
            return ServiceSourceBindingFailure.KindMismatch;
        if (!IsSafeSourceRecordId(expectedSourceRecordId)
            || !string.Equals(claim.SourceRecordId, expectedSourceRecordId,
                StringComparison.Ordinal))
            return ServiceSourceBindingFailure.RecordMismatch;

        if (!IsSafeHash(claim.SourceSha256)
            || !IsSafeHash(verifiedSourceSha256))
            return ServiceSourceBindingFailure.InvalidSourceDigest;
        if (!string.Equals(claim.SourceSha256, verifiedSourceSha256,
                StringComparison.OrdinalIgnoreCase))
            return ServiceSourceBindingFailure.DigestMismatch;

        if (!IsSafeRelativePath(claim.RelativeSourcePath)
            || !IsSafeRelativePath(verifiedRelativePath)
            || !string.Equals(claim.RelativeSourcePath, verifiedRelativePath,
                StringComparison.Ordinal))
            return ServiceSourceBindingFailure.PathMismatch;

        if (!string.Equals(claim.ConfirmationMethod, ExplicitReview,
                StringComparison.Ordinal))
            return ServiceSourceBindingFailure.UntrustedConfirmation;

        // Unknown/missing inventory must not mean verified uniqueness.
        if (allClaims is null)
            return ServiceSourceBindingFailure.InvalidClaim;
        // The exact proposed claim itself must occur exactly once.
        // A DIFFERENT claim with the same binding ID, record identity or
        // canonical source path is ambiguous, including across two cases.
        // This is only a pure preflight over a caller-supplied inventory;
        // the future C2-B store must independently audit every persisted item.
        var exactCount = 0;
        foreach (var other in allClaims)
        {
            if (other is null)
                return ServiceSourceBindingFailure.InvalidClaim;
            var sameBinding = other.BindingId == claim.BindingId;
            var sameSource = string.Equals(other.SourceKind, claim.SourceKind,
                    StringComparison.Ordinal)
                && string.Equals(other.SourceRecordId, claim.SourceRecordId,
                    StringComparison.Ordinal);
            var samePath = string.Equals(other.RelativeSourcePath,
                claim.RelativeSourcePath, StringComparison.OrdinalIgnoreCase);
            if (!sameBinding && !sameSource && !samePath)
                continue;
            if (other != claim)
                return ServiceSourceBindingFailure.DuplicateClaim;
            exactCount++;
        }
        return exactCount == 1
            ? ServiceSourceBindingFailure.None
            : ServiceSourceBindingFailure.DuplicateClaim;
    }

    public static bool IsSafeHash(string? sha) =>
        sha is not null && Sha256Pattern.IsMatch(sha);

    // Historical source IDs can be 8-character strings; their actual JSON
    // identifiers must be checked at use time. Never invent a GUID from a
    // timestamp, serial number or guessed filename.
    public static bool IsSafeSourceRecordId(string? value) =>
        value is not null && SourceIdPattern.IsMatch(value);

    public static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 240
            || !RelativePathPattern.IsMatch(value)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains("//", StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.EndsWith("/", StringComparison.Ordinal)
            || value.Contains(':'))
            return false;

        // Windows treats these reserved basenames and trailing dots specially,
        // even with extensions. A path passing this syntactic guard STILL
        // requires rooted, handle-based provenance checks in Infrastructure.
        foreach (var segment in value.Split('/'))
        {
            if (segment.Length == 0 || segment.StartsWith('.', StringComparison.Ordinal)
                || segment.EndsWith('.', StringComparison.Ordinal))
                return false;
            var basename = segment.Split('.')[0].ToUpperInvariant();
            if (basename is "CON" or "PRN" or "AUX" or "NUL"
                || (basename.Length == 4
                    && (basename.StartsWith("COM", StringComparison.Ordinal)
                        || basename.StartsWith("LPT", StringComparison.Ordinal))
                    && basename[3] >= '1' && basename[3] <= '9'))
                return false;
        }
        return true;
    }
}
