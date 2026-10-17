using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ServiceSourceBindingClaimPolicyTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private const string Record = "ABCDEF12";
    private static readonly Guid Binding = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private const string Hash = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
    private const string Relative = "diagnostics/record-1.json";

    private static ServiceSourceBindingClaim Good() => new(
        1, Binding, CaseA, "Diagnostic", Record, Hash, Relative,
        new DateTimeOffset(2026, 10, 17, 17, 0, 0, TimeSpan.Zero),
        ServiceSourceBindingClaimPolicy.ExplicitReview);

    private static ServiceSourceBindingFailure Check(ServiceSourceBindingClaim claim,
        IReadOnlyCollection<ServiceSourceBindingClaim>? inventory = null) =>
        ServiceSourceBindingClaimPolicy.Validate(claim, CaseA, "Diagnostic", Record,
            Hash, Relative, inventory ?? new[] { claim });

    [Fact]
    public void ExplicitReviewedClaimWithExactSourceDigest_IsOnlySyntaxApproved()
    {
        Assert.Equal(ServiceSourceBindingFailure.None, Check(Good()));
    }

    [Fact]
    public void AnotherCaseOrRecordCannotInheritReviewedAssociation()
    {
        Assert.Equal(ServiceSourceBindingFailure.CaseMismatch,
            Check(Good() with { ServiceCaseId = CaseB }));
        Assert.Equal(ServiceSourceBindingFailure.RecordMismatch,
            Check(Good() with { SourceRecordId = Binding.ToString("N") }));
        Assert.Equal(ServiceSourceBindingFailure.KindMismatch,
            Check(Good() with { SourceKind = "SMART" }));
    }

    [Fact]
    public void ContentReplacementAndMissingHashesAreRejected()
    {
        Assert.Equal(ServiceSourceBindingFailure.DigestMismatch,
            Check(Good() with { SourceSha256 = new string('f', 64) }));
        Assert.Equal(ServiceSourceBindingFailure.InvalidSourceDigest,
            Check(Good() with { SourceSha256 = "not a hash" }));
        Assert.Equal(ServiceSourceBindingFailure.InvalidSourceDigest,
            Check(Good() with { SourceSha256 = Hash + "\n" }));
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("diagnostics/../../private.json")]
    [InlineData("/absolute.json")]
    [InlineData("C:/private.json")]
    [InlineData("diagnostics//file.json")]
    [InlineData("diagnostics/./file.json")]
    [InlineData("diagnostics/file.json\nGITHUB_OUTPUT=x")]
    [InlineData("diagnostics/file.json\n")]
    [InlineData("diagnostics/CON.json")]
    [InlineData("diagnostics/nul.json")]
    [InlineData("diagnostics/COM1.json")]
    [InlineData("diagnostics/.hidden.json")]
    [InlineData("diagnostics/file.")]
    public void UnsafeSourceRelativePathsNeverPassPreflight(string path)
    {
        Assert.Equal(ServiceSourceBindingFailure.PathMismatch,
            Check(Good() with { RelativeSourcePath = path }));
    }

    [Fact]
    public void DuplicateOrMissingAuditInventoryFailsClosed()
    {
        var c = Good();
        var duplicate = c with { BindingId = Guid.NewGuid() };
        Assert.Equal(ServiceSourceBindingFailure.DuplicateClaim,
            Check(c, new[] { c, duplicate }));
        Assert.Equal(ServiceSourceBindingFailure.DuplicateClaim,
            Check(c, Array.Empty<ServiceSourceBindingClaim>()));
        Assert.Equal(ServiceSourceBindingFailure.InvalidClaim,
            ServiceSourceBindingClaimPolicy.Validate(c, CaseA, "Diagnostic",
                Record, Hash, Relative, null));
    }

    [Fact]
    public void MissingExactClaimOrReusedSourceAcrossCustomersFailsClosed()
    {
        var good = Good();
        var foreignCase = good with
        {
            BindingId = Guid.NewGuid(),
            ServiceCaseId = CaseB
        };
        Assert.Equal(ServiceSourceBindingFailure.DuplicateClaim,
            Check(good, new[] { good, foreignCase }));
        // One foreign claim is NOT equivalent to the missing exact claim.
        Assert.Equal(ServiceSourceBindingFailure.DuplicateClaim,
            Check(good, new[] { foreignCase }));

        var sameSourcePath = good with
        {
            BindingId = Guid.NewGuid(),
            SourceRecordId = "DEADBEEF",
            ServiceCaseId = CaseB
        };
        Assert.Equal(ServiceSourceBindingFailure.DuplicateClaim,
            Check(good, new[] { good, sameSourcePath }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad/id")]
    [InlineData("diagnostic\n")]
    [InlineData("ABCD.1234")]
    public void InvalidHistoricalSourceIdCannotBeTrusted(string id)
    {
        Assert.Equal(ServiceSourceBindingFailure.InvalidClaim,
            Check(Good() with { SourceRecordId = id }));
    }

    [Fact]
    public void ValidLegacyEightCharacterSourceIdIsNotTreatedAsMissingGuid()
    {
        Assert.Equal(ServiceSourceBindingFailure.None, Check(Good()));
    }

    [Fact]
    public void UnreviewedLegacyAndVersionSkewCannotBeTrusted()
    {
        Assert.Equal(ServiceSourceBindingFailure.UntrustedConfirmation,
            Check(Good() with { ConfirmationMethod = "AutomaticBySerial" }));
        Assert.Equal(ServiceSourceBindingFailure.InvalidClaim,
            Check(Good() with { SchemaVersion = 99 }));
        Assert.Equal(ServiceSourceBindingFailure.InvalidClaim,
            Check(Good() with { BindingId = Guid.Empty }));
    }
}
