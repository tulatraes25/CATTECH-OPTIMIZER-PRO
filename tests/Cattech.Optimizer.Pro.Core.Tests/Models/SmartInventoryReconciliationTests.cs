using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Evidence;
using Cattech.Optimizer.Pro.Core.Models.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public sealed class SmartInventoryReconciliationTests
{
    private static readonly DiagnosticContextId CaseA =
        new(Guid.Parse("d1111111-1111-4111-8111-111111111111"));
    private static readonly DiagnosticContextId CaseB =
        new(Guid.Parse("d2222222-2222-4222-8222-222222222222"));
    private static readonly DiagnosticSubjectId DiskA =
        new(Guid.Parse("a1111111-1111-4111-8111-111111111111"));
    private static readonly DiagnosticSubjectId DiskB =
        new(Guid.Parse("b2222222-2222-4222-8222-222222222222"));
    private static readonly DateTimeOffset PriorTime =
        new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CurrentTime = PriorTime.AddHours(1);
    private static readonly DateTimeOffset EvaluationTime = CurrentTime.AddMinutes(10);

    private static SmartDiskReport Disk(
        string serial = "PRIVATE-SERIAL-001",
        string model = "EXAMPLE NVME 1",
        long size = 512_000_000_000,
        string path = "/dev/nvme0",
        string transport = "nvme") => new()
        {
            Device = path,
            ModelName = model,
            SerialNumber = serial,
            CapacityBytes = size,
            SmartctlDeviceType = transport,
            Protocol = "NVMe",
            IsAnalysisSuccessful = true,
            HealthStatus = SmartHealthStatus.Good
        };

    private static SmartInventorySnapshot Snapshot(
        DiagnosticContextId context, DateTimeOffset when,
        params SmartDiskReport[] reports) =>
        SmartInventorySnapshot.Capture(context, when, reports);

    [Fact]
    public void SameDrive_WithRenumberedPath_IsOnlyAnUnapprovedCandidate()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(path: "/dev/nvme0"));
        var current = Snapshot(CaseA, CurrentTime, Disk(path: "/dev/nvme7"));

        var match = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.CandidateRequiresReview, match.Status);
        Assert.Equal(DiskA, match.PreviousSubjectId);
        Assert.Equal(0, match.CurrentIndex);
    }

    [Fact]
    public void ReorderedInventory_UsesIdentityNotEnumerationPosition()
    {
        var a = Disk(serial: "PRIVATE-AAA", path: "/dev/sda");
        var b = Disk(serial: "PRIVATE-BBB", path: "/dev/sdb");
        var before = Snapshot(CaseA, PriorTime, a, b);
        var after = Snapshot(CaseA, CurrentTime,
            Disk(serial: "PRIVATE-BBB", path: "/dev/sda"),
            Disk(serial: "PRIVATE-AAA", path: "/dev/sdb"));

        var result = SmartInventorySnapshot.Preview(
            before, after, new[] { DiskA, DiskB }, EvaluationTime);

        Assert.Equal(DiskB, result[0].PreviousSubjectId);
        Assert.Equal(DiskA, result[1].PreviousSubjectId);
        Assert.All(result, match =>
            Assert.Equal(SmartInventoryMatchStatus.CandidateRequiresReview,
                match.Status));
    }

    [Fact]
    public void ReplacedDriveOnSamePath_IsNewNotRepaired()
    {
        var before = Snapshot(CaseA, PriorTime, Disk(serial: "ORIGINAL"));
        var after = Snapshot(CaseA, CurrentTime, Disk(serial: "REPLACEMENT"));

        var match = Assert.Single(SmartInventorySnapshot.Preview(
            before, after, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.NewDevice, match.Status);
        Assert.Null(match.PreviousSubjectId);
    }

    [Fact]
    public void DuplicateCurrentSerial_RefusesBothMatches()
    {
        var before = Snapshot(CaseA, PriorTime, Disk(serial: "SAME-SERIAL"));
        var after = Snapshot(CaseA, CurrentTime,
            Disk(serial: "SAME-SERIAL", path: "/dev/sda"),
            Disk(serial: "SAME-SERIAL", path: "/dev/sdb", model: "DIFFERENT MODEL"));

        var matches = SmartInventorySnapshot.Preview(
            before, after, new[] { DiskA }, EvaluationTime);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, match =>
        {
            Assert.Equal(SmartInventoryMatchStatus.AmbiguousSerial, match.Status);
            Assert.Null(match.PreviousSubjectId);
        });
    }

    [Fact]
    public void DuplicatePriorSerial_BlocksRebindingEvenIfOneCandidateMatches()
    {
        var before = Snapshot(CaseA, PriorTime,
            Disk(serial: "DUPLICATED", path: "/dev/sda"),
            Disk(serial: "DUPLICATED", path: "/dev/sdb"));
        var after = Snapshot(CaseA, CurrentTime, Disk(serial: "DUPLICATED"));

        var match = Assert.Single(SmartInventorySnapshot.Preview(
            before, after, new[] { DiskA, DiskB }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.AmbiguousSerial, match.Status);
        Assert.Null(match.PreviousSubjectId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    [InlineData("n/d")]
    [InlineData("N/A")]
    [InlineData("000000000000")]
    public void MissingOrPlaceholderSerial_CannotBeConfirmed(string serial)
    {
        var before = Snapshot(CaseA, PriorTime, Disk(serial: serial));
        var after = Snapshot(CaseA, CurrentTime, Disk(serial: serial));

        var match = Assert.Single(SmartInventorySnapshot.Preview(
            before, after, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, match.Status);
        Assert.Null(match.PreviousSubjectId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("0000000000000000")]
    [InlineData("default string")]
    [InlineData("TO BE FILLED BY OEM")]
    [InlineData("SYSTEM SERIAL NUMBER")]
    [InlineData("UNKNOWN SERIAL")]
    [InlineData("NOT APPLICABLE")]
    public void FirmwarePlaceholderSerial_RequiresIndependentVerification(string serial)
    {
        var prior = Snapshot(CaseA, PriorTime, Disk(serial: serial));
        var now = Snapshot(CaseA, CurrentTime, Disk(serial: serial));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            prior, now, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
        Assert.Null(result.PreviousSubjectId);
    }

    [Fact]
    public void RealisticZeroPrefixedSerial_IsNotMistakenForAllZeroPlaceholder()
    {
        var prior = Snapshot(CaseA, PriorTime, Disk(serial: "0000A123"));
        var now = Snapshot(CaseA, CurrentTime, Disk(serial: "0000A123"));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            prior, now, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.CandidateRequiresReview, result.Status);
        Assert.Equal(DiskA, result.PreviousSubjectId);
    }

    [Theory]
    [InlineData("SN\nABC")]
    [InlineData("SN\rABC")]
    [InlineData("SN\tABC")]
    [InlineData("SN\u200BABC")]
    [InlineData("SN\u202EABC")]
    [InlineData("SN\u2066ABC")]
    [InlineData("FFFFFFFFFFFF")]
    [InlineData("XXXXXXXXXXXX")]
    public void UntrustedSerialControlOrRepeatedMarker_NeverSuggestsContinuity(string serial)
    {
        var prior = Snapshot(CaseA, PriorTime, Disk(serial: serial));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: serial));

        var match = Assert.Single(SmartInventorySnapshot.Preview(
            prior, current, new[] { DiskA }, EvaluationTime));
        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, match.Status);
        Assert.Null(match.PreviousSubjectId);
    }

    [Fact]
    public void FormatCharactersInModelOrTransport_FailClosed()
    {
        var before = Snapshot(CaseA, PriorTime, Disk());
        var invalidModel = Snapshot(CaseA, CurrentTime,
            Disk(model: "EXAMPLE\u202E NVME"));
        var invalidTransport = Snapshot(CaseA, CurrentTime,
            Disk(transport: "NVME\u200B"));

        foreach (var current in new[] { invalidModel, invalidTransport })
        {
            var match = Assert.Single(SmartInventorySnapshot.Preview(
                before, current, new[] { DiskA }, EvaluationTime));
            Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, match.Status);
            Assert.Null(match.PreviousSubjectId);
        }
    }

    [Fact]
    public void FailedAnalysis_ZeroCapacity_OrMissingDevice_DoNotClaimIdentity()
    {
        var unavailable = Disk();
        unavailable.IsAnalysisSuccessful = false;
        var invalidSize = Disk();
        invalidSize.CapacityBytes = 0;
        var noDevice = Disk();
        noDevice.Device = " ";

        var old = Snapshot(CaseA, PriorTime, Disk());
        var current = Snapshot(CaseA, CurrentTime,
            unavailable, invalidSize, noDevice);
        var result = SmartInventorySnapshot.Preview(
            old, current, new[] { DiskA }, EvaluationTime);

        Assert.All(result, match =>
            Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity,
                match.Status));
    }

    [Fact]
    public void SameSerialWithChangedCapacityModelOrTransport_RequiresInvestigation()
    {
        var prior = Snapshot(CaseA, PriorTime, Disk());
        // Evaluate each changed identity independently: a repeated serial
        // with mismatched hardware attributes must never produce a binding.
        foreach (var candidate in new[]
        {
            Disk(size: 1_024_000_000_000),
            Disk(model: "REPLACEMENT DEVICE"),
            Disk(transport: "sat")
        })
        {
            var current = Snapshot(CaseA, CurrentTime, candidate);
            var match = Assert.Single(SmartInventorySnapshot.Preview(
                prior, current, new[] { DiskA }, EvaluationTime));
            Assert.Equal(SmartInventoryMatchStatus.ChangedIdentity, match.Status);
            Assert.Null(match.PreviousSubjectId);
        }
    }

    [Fact]
    public void CrossEquipmentInventories_RejectEvenIdenticalSerials()
    {
        var before = Snapshot(CaseA, PriorTime, Disk());
        var after = Snapshot(CaseB, CurrentTime, Disk());

        Assert.Throws<ArgumentException>(() =>
            SmartInventorySnapshot.Preview(
                before, after, new[] { DiskA }, EvaluationTime));
    }

    [Fact]
    public void BackwardsOrFutureInventories_AreNotComparable()
    {
        var prior = Snapshot(CaseA, PriorTime, Disk());
        var inverted = Snapshot(CaseA, PriorTime.AddMinutes(-1), Disk());
        var future = Snapshot(CaseA, EvaluationTime.AddMinutes(1), Disk());

        Assert.Throws<ArgumentException>(() =>
            SmartInventorySnapshot.Preview(
                prior, inverted, new[] { DiskA }, EvaluationTime));
        Assert.Throws<ArgumentException>(() =>
            SmartInventorySnapshot.Preview(
                prior, future, new[] { DiskA }, EvaluationTime));
    }

    [Fact]
    public void BaselineSubjects_MustBeUniqueValidAndCountMatched()
    {
        var old = Snapshot(CaseA, PriorTime, Disk(serial: "AAA"), Disk(serial: "BBB"));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: "AAA"));

        Assert.Throws<ArgumentException>(() =>
            SmartInventorySnapshot.Preview(
                old, current, new[] { DiskA }, EvaluationTime));
        Assert.Throws<ArgumentException>(() =>
            SmartInventorySnapshot.Preview(
                old, current, new[] { DiskA, DiskA }, EvaluationTime));
        Assert.Throws<ArgumentException>(() =>
            SmartInventorySnapshot.Preview(
                old, current, new[] { DiskA, default(DiagnosticSubjectId) },
                EvaluationTime));
    }

    [Fact]
    public void InputReportMutations_DoNotChangePreviousSnapshotIdentity()
    {
        var original = Disk();
        var baseline = Snapshot(CaseA, PriorTime, original);
        original.SerialNumber = "MODIFIED-IN-CALLER";
        original.Device = "/dev/nvme999";

        var current = Snapshot(CaseA, CurrentTime, Disk());
        var match = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.CandidateRequiresReview, match.Status);
        Assert.Equal(DiskA, match.PreviousSubjectId);
    }

    [Fact]
    public void PreviewOutputAndSnapshotJson_ExcludePrivateDeviceIdentity()
    {
        const string serial = "PRIVATE-SERIAL-DO-NOT-PUBLISH";
        const string path = @"C:\Users\Private\disks\PhysicalDrive0";
        var prior = Snapshot(CaseA, PriorTime, Disk(serial: serial, path: path));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: serial, path: path));
        var match = SmartInventorySnapshot.Preview(
            prior, current, new[] { DiskA }, EvaluationTime);

        var serialized = JsonSerializer.Serialize(match) + JsonSerializer.Serialize(prior);
        Assert.DoesNotContain(serial, serialized);
        Assert.DoesNotContain(path, serialized);
        Assert.DoesNotContain(CaseA.Value.ToString(), serialized,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DiskA.Value.ToString(), serialized,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverlargeInventory_AndDefaultContext_RefuseSnapshot()
    {
        Assert.Throws<ArgumentException>(() =>
            Snapshot(default, PriorTime, Disk()));
        Assert.Throws<ArgumentException>(() => SmartInventorySnapshot.Capture(
            CaseA, PriorTime, Enumerable.Repeat(Disk(), 129)));
    }

    [Fact]
    public void FailedCurrentReportWithSameClaimedSerial_BlocksValidCandidate()
    {
        var valid = Disk(serial: "DUPLICATE-DECLARED", path: "/dev/sda");
        var incomplete = Disk(serial: "DUPLICATE-DECLARED", path: "/dev/sdb");
        incomplete.IsAnalysisSuccessful = false;
        incomplete.ModelName = "";

        var previous = Snapshot(CaseA, PriorTime,
            Disk(serial: "DUPLICATE-DECLARED"));
        var current = Snapshot(CaseA, CurrentTime, valid, incomplete);
        var result = SmartInventorySnapshot.Preview(
            previous, current, new[] { DiskA }, EvaluationTime);

        Assert.Equal(SmartInventoryMatchStatus.AmbiguousSerial, result[0].Status);
        Assert.Null(result[0].PreviousSubjectId);
        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity,
            result[1].Status);
    }

    [Fact]
    public void FailedBaselineReportWithSameClaimedSerial_BlocksRebinding()
    {
        var failed = Disk(serial: "BASELINE-DUPLICATE", path: "/dev/sdb");
        failed.IsAnalysisSuccessful = false;

        var previous = Snapshot(CaseA, PriorTime,
            Disk(serial: "BASELINE-DUPLICATE", path: "/dev/sda"), failed);
        var current = Snapshot(CaseA, CurrentTime,
            Disk(serial: "BASELINE-DUPLICATE", path: "/dev/sdz"));
        var result = SmartInventorySnapshot.Preview(
            previous, current, new[] { DiskA, DiskB }, EvaluationTime);

        var match = Assert.Single(result);
        Assert.Equal(SmartInventoryMatchStatus.AmbiguousSerial, match.Status);
        Assert.Null(match.PreviousSubjectId);
    }


    [Fact]
    public void MatchingPreview_IsCurrentOnlyForOriginalFrozenSnapshots()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(serial: "SN1"));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: "SN1"));
        var preview = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.CandidateRequiresReview, preview.Status);
        Assert.True(preview.IsCurrentCandidateFor(
            baseline, current, new[] { DiskA }, selectedCurrentIndex: 0));

        // A new capture must require a new review even with identical reports.
        var reCapturedCurrent = Snapshot(CaseA, CurrentTime, Disk(serial: "SN1"));
        var reCapturedBaseline = Snapshot(CaseA, PriorTime, Disk(serial: "SN1"));
        Assert.False(preview.IsCurrentCandidateFor(
            baseline, reCapturedCurrent, new[] { DiskA }, 0));
        Assert.False(preview.IsCurrentCandidateFor(
            reCapturedBaseline, current, new[] { DiskA }, 0));
    }

    [Fact]
    public void ReorderedDisks_RejectOldRowOrRemappedBaselineSubjects()
    {
        var baseline = Snapshot(CaseA, PriorTime,
            Disk(serial: "SN-A"), Disk(serial: "SN-B"));
        var current = Snapshot(CaseA, CurrentTime,
            Disk(serial: "SN-B"), Disk(serial: "SN-A"));
        var map = new List<DiagnosticSubjectId> { DiskA, DiskB };
        var matches = SmartInventorySnapshot.Preview(
            baseline, current, map, EvaluationTime);

        Assert.True(matches[0].IsCurrentCandidateFor(baseline, current, map, 0));
        Assert.Equal(DiskB, matches[0].PreviousSubjectId);
        Assert.False(matches[0].IsCurrentCandidateFor(baseline, current, map, 1));

        // A caller mutating the previous subject mapping cannot reuse the
        // originally evaluated candidate to bind a different disk.
        map[0] = DiskB;
        map[1] = DiskA;
        Assert.False(matches[0].IsCurrentCandidateFor(baseline, current, map, 0));
        Assert.Equal(DiskB, matches[0].PreviousSubjectId);
    }

    [Fact]
    public void NonCandidateAndInvalidReviewMapping_NeverPassFreshnessGate()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(serial: "ORIGINAL"));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: "REPLACEMENT"));
        var preview = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.NewDevice, preview.Status);
        Assert.False(preview.IsCurrentCandidateFor(
            baseline, current, new[] { DiskA }, 0));
        Assert.False(preview.IsCurrentCandidateFor(
            baseline, current, Array.Empty<DiagnosticSubjectId>(), 0));
    }

    [Fact]
    public void FreshnessTokens_AreNeverIncludedInPublicJson()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(serial: "SECRET-SN-1"));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: "SECRET-SN-1"));
        var preview = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        var json = JsonSerializer.Serialize(preview)
            + JsonSerializer.Serialize(baseline)
            + JsonSerializer.Serialize(current);
        Assert.DoesNotContain("CaptureToken", json);
        Assert.DoesNotContain("BaselineCaptureToken", json);
        Assert.DoesNotContain("CurrentCaptureToken", json);
        Assert.DoesNotContain("SECRET-SN-1", json);
        Assert.DoesNotContain(DiskA.Value.ToString(), json,
            StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData("SN-EDGE\n")]
    [InlineData("\tSN-EDGE")]
    [InlineData("SN-EDGE\r\n")]
    [InlineData("SN-EDGE\u200B")]
    public void RawSerialEdgeCharacters_AreNotSanitizedIntoMatchingIds(string serial)
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(serial: "SN-EDGE"));
        var current = Snapshot(CaseA, CurrentTime, Disk(serial: serial));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));
        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
        Assert.Null(result.PreviousSubjectId);
    }

    [Fact]
    public void MalformedRawModel_IsNotTrimmedIntoGoodIdentity()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk());
        var current = Snapshot(CaseA, CurrentTime,
            Disk(model: "EXAMPLE NVME 1\t"));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
    }

    [Fact]
    public void MalformedDevicePath_CannotClaimVerifiedInventory()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk());
        var current = Snapshot(CaseA, CurrentTime,
            Disk(path: "/dev/nvme0\r"));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
    }

    [Fact]
    public void UnsafeExplicitSmartctlType_CannotFallbackToProtocol()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk());
        var current = Snapshot(CaseA, CurrentTime,
            Disk(transport: "\t"));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
    }

    [Fact]
    public void ExcessiveRawPadding_IsRejectedBeforeTrim()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(serial: "SN-123"));
        var current = Snapshot(CaseA, CurrentTime,
            Disk(serial: "SN-123" + new string(' ', 1600)));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
    }

    [Fact]
    public void PlainSpacePadding_RemainsAValidReviewCandidate()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk(serial: "SN-123"));
        var current = Snapshot(CaseA, CurrentTime,
            Disk(serial: "  SN-123  ", model: " EXAMPLE NVME 1 "));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.CandidateRequiresReview, result.Status);
        Assert.Equal(DiskA, result.PreviousSubjectId);
    }


    [Fact]
    public void ExcessivelyLongExplicitType_CannotFallbackToProtocol()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk());
        var current = Snapshot(CaseA, CurrentTime,
            Disk(transport: new string(' ', 1500)));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
    }

    [Fact]
    public void ExcessivelyLongDevicePath_CannotClaimInventoryIdentity()
    {
        var baseline = Snapshot(CaseA, PriorTime, Disk());
        var current = Snapshot(CaseA, CurrentTime,
            Disk(path: "/dev/" + new string('x', 1500)));
        var result = Assert.Single(SmartInventorySnapshot.Preview(
            baseline, current, new[] { DiskA }, EvaluationTime));

        Assert.Equal(SmartInventoryMatchStatus.UnverifiableIdentity, result.Status);
    }


}
