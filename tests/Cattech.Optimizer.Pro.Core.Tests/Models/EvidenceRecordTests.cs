using Cattech.Optimizer.Pro.Core.Models.Evidence;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class EvidenceRecordTests
{
    private static readonly Guid EvidenceId =
        Guid.Parse("74450514-975a-4384-aa28-60707b45ab5c");

    private static readonly DateTimeOffset ObservedAt =
        new(2026, 10, 14, 17, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void Observed_PreservesStableIdAndNormalizesUtc()
    {
        var item = EvidenceRecord.Observed(
            EvidenceId, "windows-event-log", "crash.bugcheck",
            ObservedAt, EvidenceValue.FromText("0x0000009F"),
            "synthetic-event-fixture", EvidencePrivacyClass.Equipment);

        Assert.Equal(EvidenceId, item.Id);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 20, 0, 0, TimeSpan.Zero),
            item.ObservedAtUtc);
        Assert.Equal(EvidenceAvailability.Observed, item.Availability);
        Assert.Equal(EvidenceValueKind.Text, item.Value!.Kind);
        Assert.Equal("0x0000009F", item.Value.Text);
        Assert.Equal(EvidencePrivacyClass.Equipment, item.PrivacyClass);
    }

    [Theory]
    [InlineData(EvidenceAvailability.Unavailable)]
    [InlineData(EvidenceAvailability.AccessDenied)]
    [InlineData(EvidenceAvailability.Unsupported)]
    [InlineData(EvidenceAvailability.InvalidData)]
    public void Missing_AlwaysHasNoValueAndExplainsTheGap(EvidenceAvailability reason)
    {
        var item = EvidenceRecord.Missing(
            EvidenceId, "smart", "disk.temperature", ObservedAt,
            reason, "synthetic-fixture", EvidencePrivacyClass.Equipment,
            "Provider did not return a usable reading.");

        Assert.Equal(reason, item.Availability);
        Assert.Null(item.Value);
        Assert.NotEmpty(item.Limitations);
    }

    [Fact]
    public void Missing_CannotMasqueradeAsObserved()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EvidenceRecord.Missing(
            EvidenceId, "smart", "disk.temperature", ObservedAt,
            EvidenceAvailability.Observed, "fixture",
            EvidencePrivacyClass.Equipment, "No reading."));
        Assert.Throws<ArgumentOutOfRangeException>(() => EvidenceRecord.Missing(
            EvidenceId, "smart", "disk.temperature", ObservedAt,
            (EvidenceAvailability)99, "fixture",
            EvidencePrivacyClass.Equipment, "No reading."));
    }

    [Fact]
    public void Missing_RequiresExplanation()
    {
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Missing(
            EvidenceId, "smart", "disk.temperature", ObservedAt,
            EvidenceAvailability.Unavailable, "fixture",
            EvidencePrivacyClass.Equipment, " "));
    }

    [Fact]
    public void EvidenceValues_AreTypedAndDoNotInventMeasurements()
    {
        var number = EvidenceValue.FromNumber(0m);
        var flag = EvidenceValue.FromBoolean(false);
        var text = EvidenceValue.FromText("synthetic");

        Assert.Equal(EvidenceValueKind.Number, number.Kind);
        Assert.Equal(0m, number.Number);
        Assert.Null(number.Boolean);
        Assert.Null(number.Text);
        Assert.Equal(EvidenceValueKind.Boolean, flag.Kind);
        Assert.False(flag.Boolean!.Value);
        Assert.Null(flag.Number);
        Assert.Equal(EvidenceValueKind.Text, text.Kind);
        Assert.Equal("synthetic", text.Text);
        Assert.Null(text.Number);
    }

    [Fact]
    public void EvidenceText_RejectsBlankValue()
    {
        Assert.Throws<ArgumentException>(() => EvidenceValue.FromText("  "));
    }

    [Fact]
    public void Record_RejectsEmptyIdentifiersAndInvalidPrivacy()
    {
        var value = EvidenceValue.FromBoolean(true);
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Observed(
            Guid.Empty, "sensor", "cpu.valid", ObservedAt,
            value, "fixture", EvidencePrivacyClass.Equipment));
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Observed(
            EvidenceId, "", "cpu.valid", ObservedAt,
            value, "fixture", EvidencePrivacyClass.Equipment));
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Observed(
            EvidenceId, "sensor", " ", ObservedAt,
            value, "fixture", EvidencePrivacyClass.Equipment));
        Assert.Throws<ArgumentException>(() => EvidenceRecord.Observed(
            EvidenceId, "sensor", "cpu.valid", ObservedAt,
            value, " ", EvidencePrivacyClass.Equipment));
        Assert.Throws<ArgumentOutOfRangeException>(() => EvidenceRecord.Observed(
            EvidenceId, "sensor", "cpu.valid", ObservedAt,
            value, "fixture", (EvidencePrivacyClass)99));
    }

    [Fact]
    public void Observed_RequiresAValue()
    {
        Assert.Throws<ArgumentNullException>(() => EvidenceRecord.Observed(
            EvidenceId, "sensor", "cpu.valid", ObservedAt,
            null!, "fixture", EvidencePrivacyClass.Equipment));
    }
}
