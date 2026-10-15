using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class TechnicalPassportReviewViewModelTests
{
    private static TechnicalPassportArtifact NewPassport(string observation = "healthy")
    {
        var issued = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
        var evidence = EvidenceRecord.Observed(
            Guid.Parse("10101010-1010-4010-8010-101010101010"),
            "synthetic", "windows.health", issued.AddMinutes(-10),
            EvidenceValue.FromText(observation), "synthetic-test-only",
            EvidencePrivacyClass.Equipment);

        return TechnicalPassportBuilder.Build(
            Guid.Parse("20202020-2020-4020-8020-202020202020"), issued,
            new[] { evidence }, Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());
    }

    [Fact]
    public void OpeningPreview_NeverCopiesAutomatically_AndStartsUnconsented()
    {
        int copies = 0;
        var passport = NewPassport();
        var vm = new TechnicalPassportReviewViewModel(passport, _ => copies++);

        Assert.Equal(passport.PayloadJson, vm.PreviewJson);
        Assert.Equal(passport.Sha256Hex, vm.PreviewSha256);
        Assert.Contains("firma digital", vm.PrivacyNotice);
        Assert.False(vm.HasReviewedPreview);
        Assert.False(vm.ConsentsToClipboard);
        Assert.False(vm.HasCopiedToClipboard);
        Assert.False(vm.CopyApprovedPayloadCommand.CanExecute(null));
        vm.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);
    }

    [Fact]
    public void AcknowledgmentAndConsent_AreBothRequiredForTheSamePreview()
    {
        int copies = 0;
        var vm = new TechnicalPassportReviewViewModel(NewPassport(), _ => copies++);

        vm.HasReviewedPreview = true;
        Assert.False(vm.CopyApprovedPayloadCommand.CanExecute(null));
        vm.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);

        vm.HasReviewedPreview = false;
        vm.ConsentsToClipboard = true;
        Assert.False(vm.CopyApprovedPayloadCommand.CanExecute(null));
        vm.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);

        vm.HasReviewedPreview = true;
        Assert.True(vm.CopyApprovedPayloadCommand.CanExecute(null));
        vm.ConsentsToClipboard = false;
        Assert.False(vm.CopyApprovedPayloadCommand.CanExecute(null));
        Assert.Equal(0, copies);
    }

    [Fact]
    public void ApprovedCopy_WritesExactFrozenBytesOnce_ThenInvalidatesConsent()
    {
        var copied = new List<string>();
        var passport = NewPassport();
        var vm = new TechnicalPassportReviewViewModel(passport, copied.Add);
        vm.HasReviewedPreview = true;
        vm.ConsentsToClipboard = true;

        Assert.True(vm.CopyApprovedPayloadCommand.CanExecute(null));
        vm.CopyApprovedPayloadCommand.Execute(null);

        Assert.Single(copied);
        Assert.Equal(passport.PayloadJson, copied[0]);
        Assert.True(vm.HasCopiedToClipboard);
        Assert.False(vm.HasReviewedPreview);
        Assert.False(vm.ConsentsToClipboard);
        Assert.False(vm.CopyApprovedPayloadCommand.CanExecute(null));
        Assert.Contains("No se envió", vm.StatusMessage);

        // Even a re-check of both boxes cannot copy the same stale snapshot.
        vm.HasReviewedPreview = true;
        vm.ConsentsToClipboard = true;
        vm.CopyApprovedPayloadCommand.Execute(null);
        Assert.Single(copied);
    }

    [Fact]
    public void ClipboardFailure_DoesNotClaimSuccessOrConsumeConsent()
    {
        int calls = 0;
        var vm = new TechnicalPassportReviewViewModel(NewPassport(), _ =>
        {
            calls++;
            if (calls == 1)
                throw new InvalidOperationException("synthetic clipboard unavailable");
        });
        vm.HasReviewedPreview = true;
        vm.ConsentsToClipboard = true;

        vm.CopyApprovedPayloadCommand.Execute(null);
        Assert.False(vm.HasCopiedToClipboard);
        Assert.True(vm.CopyApprovedPayloadCommand.CanExecute(null));
        Assert.Contains("No se pudo", vm.StatusMessage);
        Assert.DoesNotContain("synthetic clipboard", vm.StatusMessage);

        vm.CopyApprovedPayloadCommand.Execute(null);
        Assert.True(vm.HasCopiedToClipboard);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void NewSnapshot_NeedsFreshReviewAndConsent()
    {
        int copies = 0;
        var a = new TechnicalPassportReviewViewModel(NewPassport("healthy"),
            _ => copies++);
        var b = new TechnicalPassportReviewViewModel(NewPassport("unhealthy"),
            _ => copies++);

        a.HasReviewedPreview = true;
        a.ConsentsToClipboard = true;
        Assert.False(b.CopyApprovedPayloadCommand.CanExecute(null));
        b.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);

        a.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(1, copies);
        Assert.False(b.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void MissingClipboardWriter_IsRejectedAtConstruction()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TechnicalPassportReviewViewModel(NewPassport(), null!));
    }
}
