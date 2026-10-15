using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// In-memory adversarial selection tests. No real hardware, clipboard or I/O.
/// </summary>
public sealed class DiagnosticSessionCoordinatorTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("11111111-aaaa-4111-8111-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("22222222-bbbb-4222-8222-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId Subject =
        new(Guid.Parse("33333333-cccc-4333-8333-cccccccccccc"));
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 15, 22, 0, 0, TimeSpan.Zero);

    private static TechnicalPassportArtifact Passport(DiagnosticContextId scope)
    {
        var evidence = EvidenceRecord.Observed(
            Guid.Parse("44444444-dddd-4444-8444-dddddddddddd"),
            scope, Subject, "synthetic", "disk.health",
            IssuedAt.AddMinutes(-10), EvidenceValue.FromText("healthy"),
            "fictional-fixture", EvidencePrivacyClass.Equipment);
        return TechnicalPassportBuilder.Build(scope,
            Guid.Parse("55555555-eeee-4555-8555-eeeeeeeeeeee"),
            IssuedAt, new[] { evidence },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());
    }

    [Fact]
    public void UnselectedCoordinator_DeniesAllCaseLeases()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        Assert.Null(coordinator.CaptureLeaseFor(CaseA));
        Assert.Null(coordinator.CaptureLeaseFor(default));
        Assert.Throws<ArgumentException>(() => coordinator.SelectCase(default));
        Assert.Null(coordinator.CaptureLeaseFor(CaseA));
    }

    [Fact]
    public void NewSession_MintsOpaqueUniqueLogicalCaseAndWorkingLease()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        var a = coordinator.StartNewCase();
        var leaseA = Assert.IsType<DiagnosticSessionLease>(
            coordinator.CaptureLeaseFor(a));

        Assert.True(a.IsValid);
        Assert.True(leaseA.IsCurrent);
        Assert.Equal(a, leaseA.ContextId);
        var b = coordinator.StartNewCase();
        Assert.NotEqual(a, b);
        Assert.False(leaseA.IsCurrent);
        Assert.Null(coordinator.CaptureLeaseFor(a));
        Assert.True(coordinator.CaptureLeaseFor(b)?.IsCurrent);
    }

    [Fact]
    public void ReselectingSameCase_AlwaysInvalidatesOldEpochLease()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var oldLease = Assert.IsType<DiagnosticSessionLease>(
            coordinator.CaptureLeaseFor(CaseA));

        coordinator.SelectCase(CaseA);
        var renewed = Assert.IsType<DiagnosticSessionLease>(
            coordinator.CaptureLeaseFor(CaseA));

        Assert.False(oldLease.IsCurrent);
        Assert.True(renewed.IsCurrent);
        Assert.Equal(oldLease.ContextId, renewed.ContextId);
    }

    [Fact]
    public void AtoBtoA_RejectsAllOldLeasesEvenIfGuidIsIdentical()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var first = coordinator.CaptureLeaseFor(CaseA)!;
        coordinator.SelectCase(CaseB);
        var second = coordinator.CaptureLeaseFor(CaseB)!;
        coordinator.SelectCase(CaseA);

        Assert.False(first.IsCurrent);
        Assert.False(second.IsCurrent);
        Assert.True(coordinator.CaptureLeaseFor(CaseA)!.IsCurrent);
    }

    [Fact]
    public void ClearingSelectionAndClearingAgain_RevokesEveryOldLease()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var old = coordinator.CaptureLeaseFor(CaseA)!;
        coordinator.ClearSelection();
        coordinator.ClearSelection();

        Assert.False(old.IsCurrent);
        Assert.Null(coordinator.CaptureLeaseFor(CaseA));
    }

    [Fact]
    public void SelectionChange_NotifiesAllListenersEvenWhenOneThrows()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        var count = 0;
        coordinator.SelectionChanged += (_, _) =>
            throw new InvalidOperationException("Synthetic client data");
        coordinator.SelectionChanged += (_, _) => count++;
        coordinator.SelectCase(CaseA);
        coordinator.SelectCase(CaseA);
        coordinator.ClearSelection();

        Assert.Equal(3, count);
        Assert.Null(coordinator.CaptureLeaseFor(CaseA));
    }

    [Fact]
    public void LeaseIsOpaqueInJson_AndCannotBeConstructedPublicly()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var lease = coordinator.CaptureLeaseFor(CaseA)!;
        Assert.Empty(typeof(DiagnosticSessionLease).GetConstructors());

        var output = JsonSerializer.Serialize(lease)
            + JsonSerializer.Serialize(coordinator);
        Assert.DoesNotContain(CaseA.Value.ToString(), output,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContextId", output);
        Assert.DoesNotContain("Epoch", output);
    }

    [Fact]
    public void ExistingCaseReview_MustBeBoundToTheCorrectLease()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var leaseA = coordinator.CaptureLeaseFor(CaseA)!;
        var copies = 0;
        var review = new TechnicalPassportReviewViewModel(
            Passport(CaseB), _ => copies++, leaseA);

        Assert.True(review.IsConsentRevoked);
        Assert.Empty(review.PreviewJson);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        review.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);
    }

    [Fact]
    public void SelectionChange_AutomaticallyClearsReviewWithoutManualRevocation()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var lease = coordinator.CaptureLeaseFor(CaseA)!;
        using var review = new TechnicalPassportReviewViewModel(
            Passport(CaseA), _ => throw new Exception("Never copy"), lease);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        Assert.True(review.CopyApprovedPayloadCommand.CanExecute(null));

        coordinator.SelectCase(CaseB);

        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
        Assert.True(review.IsConsentRevoked);
        Assert.Empty(review.PreviewJson);
        Assert.Empty(review.PreviewSha256);
        Assert.False(review.HasReviewedPreview);
        Assert.False(review.ConsentsToClipboard);
    }

    [Fact]
    public void SameCaseReopened_AutomaticallyRevokesReviewOnEpochChange()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var lease = coordinator.CaptureLeaseFor(CaseA)!;
        using var review = new TechnicalPassportReviewViewModel(
            Passport(CaseA), _ => { }, lease);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        coordinator.SelectCase(CaseA);

        Assert.False(lease.IsCurrent);
        Assert.True(review.IsConsentRevoked);
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
        Assert.Empty(review.PreviewJson);
    }

    [Fact]
    public void ClipboardCallbackSwitchesAwayAndBack_StillFailsClosed()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var lease = coordinator.CaptureLeaseFor(CaseA)!;
        var copies = 0;
        using var review = new TechnicalPassportReviewViewModel(
            Passport(CaseA), _ =>
            {
                copies++;
                coordinator.SelectCase(CaseB);
                coordinator.SelectCase(CaseA);
            }, lease);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(1, copies);
        Assert.False(lease.IsCurrent);
        Assert.True(review.IsConsentRevoked);
        Assert.True(review.HasCopiedToClipboard);
        Assert.Empty(review.PreviewJson);
        Assert.Contains("podría contener", review.StatusMessage);
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void DisposingReview_RevokesAuthorizationAndUnsubscribes()
    {
        var coordinator = new DiagnosticSessionCoordinator();
        coordinator.SelectCase(CaseA);
        var lease = coordinator.CaptureLeaseFor(CaseA)!;
        var calls = 0;
        var review = new TechnicalPassportReviewViewModel(
            Passport(CaseA), _ => calls++, lease);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        review.Dispose();
        review.Dispose();
        coordinator.SelectCase(CaseB);
        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(0, calls);
        Assert.True(review.IsConsentRevoked);
        Assert.Empty(review.PreviewJson);
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void OnlyLeaseBasedConstructorsArePublicInReviewSurfaces()
    {
        var vm = Assert.Single(
            typeof(TechnicalPassportReviewViewModel).GetConstructors());
        var ctor = Assert.Single(
            typeof(TechnicalPassportReviewView).GetConstructors());

        Assert.Equal(typeof(DiagnosticSessionLease),
            vm.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(DiagnosticSessionLease),
            ctor.GetParameters()[1].ParameterType);
    }
}
