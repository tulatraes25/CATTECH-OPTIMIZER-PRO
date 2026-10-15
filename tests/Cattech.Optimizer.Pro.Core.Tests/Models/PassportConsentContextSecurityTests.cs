using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Synthetic-only regression suite. No Windows clipboard, devices, clients,
/// files, or network involved. The external writer is an injected callback.
/// </summary>
public sealed class PassportConsentContextSecurityTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"));
    private static readonly DiagnosticSubjectId SubjectA =
        new(Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"));
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static TechnicalPassportArtifact MakePassport(
        DiagnosticContextId scope)
    {
        var observation = EvidenceRecord.Observed(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            scope, SubjectA, "synthetic-only", "case.health",
            IssuedAt.AddMinutes(-20), EvidenceValue.FromText("healthy"),
            "fictional-evidence", EvidencePrivacyClass.Equipment);
        return TechnicalPassportBuilder.Build(scope,
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            IssuedAt, new[] { observation },
            Array.Empty<DiagnosticAssessment>(),
            Array.Empty<RepairOutcomeAssessment>());
    }

    private static TechnicalPassportReviewViewModel MakeReview(
        TechnicalPassportArtifact passport,
        Action<string> writer, Func<DiagnosticContextId>? active = null) =>
        new(passport, writer, active ?? (() => passport.ContextId));

    [Fact]
    public void ArtifactCarriesPrivateCaseBindingWithoutChangingPublicPayload()
    {
        var passport = MakePassport(CaseA);

        Assert.Equal(CaseA, passport.ContextId);
        Assert.True(passport.HasMatchingDigest());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(passport.PayloadJson))), passport.Sha256Hex);
        Assert.DoesNotContain(CaseA.Value.ToString(), passport.PayloadJson,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(CaseA.Value.ToString(),
            JsonSerializer.Serialize(passport), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContextId", JsonSerializer.Serialize(passport));
    }

    [Fact]
    public void WpfReviewSurfaceCannotOmitActiveSelectionProvider()
    {
        // Reflection only: do not instantiate WPF or invoke Windows clipboard.
        var publicCtor = Assert.Single(
            typeof(TechnicalPassportReviewView).GetConstructors());
        var parameters = publicCtor.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(TechnicalPassportArtifact),
            parameters[0].ParameterType);
        Assert.Equal(typeof(DiagnosticSessionLease),
            parameters[1].ParameterType);
    }

    [Fact]
    public void ReviewRequiresExplicitActiveCaseProvider_NoInsecureOverload()
    {
        var passport = MakePassport(CaseA);

        Assert.Throws<ArgumentNullException>(() =>
            new TechnicalPassportReviewViewModel(passport, _ => { },
                (DiagnosticSessionLease)null!));
        Assert.DoesNotContain(
            typeof(TechnicalPassportReviewViewModel).GetConstructors(),
            ctor => ctor.GetParameters().Length == 2);
        Assert.DoesNotContain(
            typeof(TechnicalPassportReviewViewModel).GetConstructors(),
            ctor => ctor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(Func<DiagnosticContextId>)));
    }

    [Fact]
    public void OpeningWithWrongActiveCase_RevokesWithoutWriting()
    {
        var passport = MakePassport(CaseA);
        var selected = CaseB;
        var copies = 0;
        var review = MakeReview(passport, _ => copies++, () => selected);

        Assert.True(review.IsConsentRevoked);
        Assert.Empty(review.PreviewJson);
        Assert.Empty(review.PreviewSha256);
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));

        // Re-selecting the original case never resurrects a spent approval.
        selected = CaseA;
        review.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);
        Assert.True(review.IsConsentRevoked);
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void SwitchingEquipmentBeforeCopy_BlocksEvenWithoutUiNotification()
    {
        var selected = CaseA;
        var copies = 0;
        var review = MakeReview(MakePassport(CaseA),
            _ => copies++, () => selected);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        Assert.True(review.CopyApprovedPayloadCommand.CanExecute(null));

        selected = CaseB;
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(0, copies);
        Assert.True(review.IsConsentRevoked);
        selected = CaseA;
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void ExplicitNavigationRevocation_IsPermanentWhenSwitchingBack()
    {
        var selected = CaseA;
        var review = MakeReview(MakePassport(CaseA),
            _ => throw new Exception("No copy allowed"), () => selected);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        selected = CaseB;
        review.InvalidateForCaseSelectionChange();

        Assert.False(review.HasReviewedPreview);
        Assert.False(review.ConsentsToClipboard);
        Assert.True(review.IsConsentRevoked);
        Assert.Empty(review.PreviewJson);
        Assert.Empty(review.PreviewSha256);
        selected = CaseA;
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void ScopeProviderFailure_IsDeniedWithoutLeakingExceptionMessage()
    {
        var calls = 0;
        var review = MakeReview(MakePassport(CaseA),
            _ => calls++, () => throw new InvalidOperationException(
                "private machine context unavailable"));
        Assert.True(review.IsConsentRevoked);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(0, calls);
        Assert.DoesNotContain("private machine", review.StatusMessage);
    }

    [Fact]
    public void ContextSwitchInsideClipboardCallback_DoesNotClaimSafeCompletion()
    {
        var passport = MakePassport(CaseA);
        var selected = CaseA;
        var copies = new List<string>();
        var review = MakeReview(passport, value =>
        {
            copies.Add(value);
            selected = CaseB;
        }, () => selected);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Single(copies);
        Assert.Equal(passport.PayloadJson, copies[0]);
        Assert.Empty(review.PreviewJson);
        Assert.Empty(review.PreviewSha256);
        Assert.True(review.HasCopiedToClipboard);
        Assert.True(review.IsConsentRevoked);
        Assert.Contains("podría contener", review.StatusMessage);
        Assert.DoesNotContain("con autorización para el caso activo",
            review.StatusMessage);
        selected = CaseA;
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void NavigationReentrancyDuringWriter_RevokesEvenIfOriginalCaseReturns()
    {
        var selected = CaseA;
        TechnicalPassportReviewViewModel? review = null;
        var count = 0;
        review = MakeReview(MakePassport(CaseA), _ =>
        {
            count++;
            selected = CaseB;
            review!.InvalidateForCaseSelectionChange();
            selected = CaseA;
        }, () => selected);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(1, count);
        Assert.True(review.IsConsentRevoked);
        Assert.True(review.HasCopiedToClipboard);
        Assert.Contains("podría contener", review.StatusMessage);
    }

    [Fact]
    public void RecursiveClipboardCommand_CannotDoubleCopySameApproval()
    {
        TechnicalPassportReviewViewModel? review = null;
        var copied = 0;
        review = MakeReview(MakePassport(CaseA), _ =>
        {
            copied++;
            review!.CopyApprovedPayloadCommand.Execute(null);
        });
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(1, copied);
        Assert.True(review.HasCopiedToClipboard);
        Assert.False(review.CopyApprovedPayloadCommand.CanExecute(null));
    }

    [Fact]
    public void WriterFailureAfterPossibleSideEffect_NeverAllowsRetryWithOldConsent()
    {
        var copied = 0;
        var review = MakeReview(MakePassport(CaseA), _ =>
        {
            copied++;
            throw new System.Runtime.InteropServices.ExternalException(
                "driver failure after OS clipboard was changed");
        });
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        review.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(1, copied);
        Assert.False(review.HasCopiedToClipboard);
        Assert.True(review.IsConsentRevoked);
        Assert.Contains("Es posible que su contenido haya cambiado",
            review.StatusMessage);
        Assert.DoesNotContain("driver failure", review.StatusMessage);
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;
        review.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(1, copied);
    }

    [Fact]
    public void UnexpectedClipboardWriterException_RevokesAndSanitizesOutput()
    {
        var callCount = 0;
        var review = MakeReview(MakePassport(CaseA), _ =>
        {
            callCount++;
            throw new ApplicationException(
                "Sensitive internal callback with customer name");
        });
        review.HasReviewedPreview = true;
        review.ConsentsToClipboard = true;

        review.CopyApprovedPayloadCommand.Execute(null);

        Assert.Equal(1, callCount);
        Assert.True(review.IsConsentRevoked);
        Assert.Empty(review.PreviewJson);
        Assert.Empty(review.PreviewSha256);
        Assert.False(review.HasCopiedToClipboard);
        Assert.DoesNotContain("customer name", review.StatusMessage);
        Assert.Contains("No se pudo confirmar", review.StatusMessage);
        review.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void NewPassportDifferentCase_NeedsFreshExplicitConsent()
    {
        var selected = CaseA;
        var copies = 0;
        var original = MakeReview(MakePassport(CaseA),
            _ => copies++, () => selected);
        var newer = MakeReview(MakePassport(CaseB),
            _ => copies++, () => selected);
        original.HasReviewedPreview = true;
        original.ConsentsToClipboard = true;

        Assert.True(original.CopyApprovedPayloadCommand.CanExecute(null));
        Assert.False(newer.CopyApprovedPayloadCommand.CanExecute(null));
        selected = CaseB;
        original.InvalidateForCaseSelectionChange();
        newer.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(0, copies);
        Assert.True(newer.IsConsentRevoked);

        // Need to create a NEW review for CaseB after changing selection.
        var fresh = MakeReview(MakePassport(CaseB),
            _ => copies++, () => selected);
        fresh.HasReviewedPreview = true;
        fresh.ConsentsToClipboard = true;
        Assert.True(fresh.CopyApprovedPayloadCommand.CanExecute(null));
        fresh.CopyApprovedPayloadCommand.Execute(null);
        Assert.Equal(1, copies);
    }
}
