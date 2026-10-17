using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class ServiceSourceBindingClaimPolicyTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid Record = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
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
            Check(Good() with { SourceRecordId = Binding }));
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
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("diagnostics/../../private.json")]
    [InlineData("/absolute.json")]
    [InlineData("C:/private.json")]
    [InlineData("diagnostics//file.json")]
    [InlineData("diagnostics/./file.json")]
    [InlineData("diagnostics/file.json\nGITHUB_OUTPUT=x")]
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
