using Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class BugCheckDecoderTests
{
    [Fact]
    public void LoadCatalog_DecodesKnownHexCode()
    {
        const string json = """
        [
          {
            "code": "0x00000124",
            "name": "WHEA_UNCORRECTABLE_ERROR",
            "description": "Hardware no corregible",
            "typicalAreas": ["cpu", "memoria"]
          }
        ]
        """;

        var catalog = BugCheckDecoder.LoadCatalog(json);
        var decoder = new BugCheckDecoder(catalog);

        var result = decoder.Decode(0x124);

        Assert.True(result.IsKnown);
        Assert.Equal("0x00000124", result.HexCode);
        Assert.Equal("WHEA_UNCORRECTABLE_ERROR", result.Name);
        Assert.Equal("Hardware no corregible", result.Description);
        Assert.Equal(new[] { "cpu", "memoria" }, result.TypicalAreas);
    }

    [Fact]
    public void Decode_UnknownCode_IsHonestAndNonFatal()
    {
        var decoder = new BugCheckDecoder(new Dictionary<uint, Core.Models.Diagnostics.BugCheckDecodeResult>());

        var result = decoder.Decode(0xDEADBEEF);

        Assert.False(result.IsKnown);
        Assert.Equal("0xDEADBEEF", result.HexCode);
        Assert.Equal("UNKNOWN_BUG_CHECK", result.Name);
        Assert.NotEmpty(result.Description);
        Assert.Empty(result.TypicalAreas);
    }

    [Theory]
    [InlineData("0x0000000A", 0x0000000Au)]
    [InlineData("0X00000124", 0x00000124u)]
    [InlineData("292", 292u)]
    public void TryParseCode_AcceptsHexAndDecimal(string input, uint expected)
    {
        var ok = BugCheckDecoder.TryParseCode(input, out var actual);

        Assert.True(ok);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0xZZZZ")]
    [InlineData("-1")]
    [InlineData("not-a-code")]
    public void TryParseCode_RejectsInvalidValues(string input)
    {
        Assert.False(BugCheckDecoder.TryParseCode(input, out _));
    }

    [Fact]
    public void LoadCatalog_FirstDuplicateWins()
    {
        const string json = """
        [
          { "code": "0xA", "name": "FIRST", "description": "first" },
          { "code": "0xA", "name": "SECOND", "description": "second" }
        ]
        """;

        var catalog = BugCheckDecoder.LoadCatalog(json);

        Assert.Single(catalog);
        Assert.Equal("FIRST", catalog[0xAu].Name);
    }

    [Fact]
    public void LoadCatalog_IgnoresInvalidAndNamelessEntries()
    {
        const string json = """
        [
          { "code": "bad", "name": "INVALID" },
          { "code": "0x50", "name": "" },
          { "code": "0x9F", "name": "DRIVER_POWER_STATE_FAILURE" }
        ]
        """;

        var catalog = BugCheckDecoder.LoadCatalog(json);

        Assert.Single(catalog);
        Assert.True(catalog.ContainsKey(0x9Fu));
    }
}
