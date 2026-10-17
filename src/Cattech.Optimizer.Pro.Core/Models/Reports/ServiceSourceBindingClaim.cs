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
    Guid SourceRecordId,
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
        new("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex RelativePathPattern =
        new("^[A-Za-z0-9_][A-Za-z0-9_./-]*$", RegexOptions.CultureInvariant);

    public static ServiceSourceBindingFailure Validate(
        ServiceSourceBindingClaim? claim,
        Guid currentCaseId, string expectedSourceKind,
        Guid expectedSourceRecordId, string verifiedSourceSha256,
        string verifiedRelativePath,
        IReadOnlyCollection<ServiceSourceBindingClaim>? allClaims = null)
    {
        if (claim is null || claim.SchemaVersion != SchemaVersion
            || claim.BindingId == Guid.Empty || claim.ServiceCaseId == Guid.Empty
            || claim.SourceRecordId == Guid.Empty || claim.ConfirmedAtUtc == default
            || string.IsNullOrWhiteSpace(claim.SourceKind))
            return ServiceSourceBindingFailure.InvalidClaim;

        if (claim.ServiceCaseId != currentCaseId || currentCaseId == Guid.Empty)
            return ServiceSourceBindingFailure.CaseMismatch;
        if (!string.Equals(claim.SourceKind, expectedSourceKind,
                StringComparison.Ordinal))
            return ServiceSourceBindingFailure.KindMismatch;
        if (claim.SourceRecordId != expectedSourceRecordId
            || expectedSourceRecordId == Guid.Empty)
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
        if (allClaims.Count(x => x is not null
                && (x.BindingId == claim.BindingId
                    || (x.ServiceCaseId == currentCaseId
                        && x.SourceKind == expectedSourceKind
                        && x.SourceRecordId == expectedSourceRecordId))) != 1)
            return ServiceSourceBindingFailure.DuplicateClaim;

        return ServiceSourceBindingFailure.None;
    }

    public static bool IsSafeHash(string? sha) =>
        sha is not null && Sha256Pattern.IsMatch(sha);

    public static bool IsSafeRelativePath(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 240
        && RelativePathPattern.IsMatch(value)
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Contains("/./", StringComparison.Ordinal)
        && !value.Contains("//", StringComparison.Ordinal)
        && !value.StartsWith("/", StringComparison.Ordinal)
        && !value.EndsWith("/", StringComparison.Ordinal)
        && !value.Contains(':');
}
