using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class StartupEntryTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void StartupEntry_DefaultValues_AreValid()
    {
        var entry = new StartupEntry();

        Assert.NotNull(entry.Id);
        Assert.NotEmpty(entry.Id);
        Assert.Equal(string.Empty, entry.Name);
        Assert.Equal(string.Empty, entry.Command);
        Assert.Equal(string.Empty, entry.Publisher);
        Assert.False(entry.IsMicrosoft);
        Assert.Equal(StartupEntryStatus.Active, entry.Status);
        Assert.Equal(RiskLevel.Low, entry.Risk);
        Assert.Equal(StartupRecommendation.Keep, entry.Recommendation);
        Assert.True(entry.FirstDetected <= DateTime.Now);
    }

    [Fact]
    public void StartupEntry_Id_Is8Chars()
    {
        var entry = new StartupEntry();
        Assert.Equal(8, entry.Id.Length);
    }

    [Fact]
    public void StartupEntry_SerializeToJson_ProducesValidJson()
    {
        var entry = new StartupEntry
        {
            Name = "SecurityHealth",
            Command = @"C:\Program Files\Windows Defender\MSASCuiL.exe",
            Location = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            SourceType = StartupSourceType.RegistryRun,
            Publisher = "Microsoft",
            IsMicrosoft = true,
            Status = StartupEntryStatus.Active,
            Risk = RiskLevel.Low,
            Recommendation = StartupRecommendation.Keep
        };

        var json = JsonSerializer.Serialize(entry, SerializerOptions);

        Assert.Contains("SecurityHealth", json);
        Assert.Contains("Microsoft", json);
        Assert.Contains("name", json); // camelCase property
        Assert.Contains("command", json);
    }

    [Fact]
    public void StartupEntry_DeserializeFromJson_PreservesAllFields()
    {
        var original = new StartupEntry
        {
            Name = "Discord",
            Command = @"C:\Users\test\AppData\Local\Discord\Update.exe",
            SourceType = StartupSourceType.RegistryRun,
            IsMicrosoft = false,
            Risk = RiskLevel.Medium,
            Recommendation = StartupRecommendation.PossibleDisable,
            Notes = "Editor desconocido"
        };

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<StartupEntry>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("Discord", deserialized!.Name);
        Assert.False(deserialized.IsMicrosoft);
        Assert.Equal(RiskLevel.Medium, deserialized.Risk);
        Assert.Equal(StartupRecommendation.PossibleDisable, deserialized.Recommendation);
        Assert.Contains("desconocido", deserialized.Notes);
    }

    [Fact]
    public void StartupAnalysis_DefaultValues_AreValid()
    {
        var analysis = new StartupAnalysis();

        Assert.NotNull(analysis.Id);
        Assert.NotEmpty(analysis.Id);
        Assert.True(analysis.AnalysisDate <= DateTime.Now);
        Assert.NotNull(analysis.Entries);
        Assert.Empty(analysis.Entries);
        Assert.Equal(0, analysis.TotalCount);
        Assert.Equal(0, analysis.MicrosoftCount);
        Assert.Equal(0, analysis.ThirdPartyCount);
        Assert.Equal(0, analysis.PossibleDisableCount);
        Assert.Equal(0, analysis.AlertCount);
    }

    [Fact]
    public void StartupAnalysis_Counts_AreCalculated()
    {
        var analysis = new StartupAnalysis();
        analysis.Entries.Add(new StartupEntry { IsMicrosoft = true, Recommendation = StartupRecommendation.Keep });
        analysis.Entries.Add(new StartupEntry { IsMicrosoft = true, Recommendation = StartupRecommendation.Keep });
        analysis.Entries.Add(new StartupEntry { IsMicrosoft = false, Recommendation = StartupRecommendation.PossibleDisable });
        analysis.Entries.Add(new StartupEntry { IsMicrosoft = false, Recommendation = StartupRecommendation.Review });
        analysis.Entries.Add(new StartupEntry { IsMicrosoft = false, Recommendation = StartupRecommendation.PossibleDisable });

        Assert.Equal(5, analysis.TotalCount);
        Assert.Equal(2, analysis.MicrosoftCount);
        Assert.Equal(3, analysis.ThirdPartyCount);
        Assert.Equal(2, analysis.PossibleDisableCount);
    }

    [Fact]
    public void StartupAnalysis_AlertCount_DetectsIssues()
    {
        var analysis = new StartupAnalysis();
        analysis.Entries.Add(new StartupEntry
        {
            Status = StartupEntryStatus.PathNotFound,
            Recommendation = StartupRecommendation.Review
        });
        analysis.Entries.Add(new StartupEntry
        {
            Risk = RiskLevel.High,
            Recommendation = StartupRecommendation.Review,
            Notes = "Ruta en Temp"
        });
        analysis.Entries.Add(new StartupEntry
        {
            Risk = RiskLevel.Low,
            Recommendation = StartupRecommendation.Keep
        });

        Assert.Equal(2, analysis.AlertCount);
    }

    [Fact]
    public void StartupAnalysis_SerializeToJson_ProducesValidJson()
    {
        var analysis = new StartupAnalysis();
        analysis.Entries.Add(new StartupEntry
        {
            Name = "TestEntry",
            IsMicrosoft = false,
            Risk = RiskLevel.Medium,
            Recommendation = StartupRecommendation.Review
        });

        var json = JsonSerializer.Serialize(analysis, SerializerOptions);

        Assert.Contains("TestEntry", json);
        Assert.Contains("entries", json);
        Assert.Contains("analysisDate", json);
    }

    [Fact]
    public void StartupSourceType_HasAllValues()
    {
        Assert.Equal(0, (int)StartupSourceType.RegistryRun);
        Assert.Equal(1, (int)StartupSourceType.RegistryRunOnce);
        Assert.Equal(2, (int)StartupSourceType.StartupFolder);
        Assert.Equal(3, (int)StartupSourceType.StartupFolderCommon);
        Assert.Equal(4, (int)StartupSourceType.ScheduledTask);
    }

    [Fact]
    public void RiskLevel_HasAllValues()
    {
        Assert.Equal(0, (int)RiskLevel.Low);
        Assert.Equal(1, (int)RiskLevel.Medium);
        Assert.Equal(2, (int)RiskLevel.High);
        Assert.Equal(3, (int)RiskLevel.Unknown);
    }

    [Fact]
    public void StartupRecommendation_HasAllValues()
    {
        Assert.Equal(0, (int)StartupRecommendation.Keep);
        Assert.Equal(1, (int)StartupRecommendation.Review);
        Assert.Equal(2, (int)StartupRecommendation.PossibleDisable);
    }

    [Fact]
    public void StartupEntryStatus_HasAllValues()
    {
        Assert.Equal(0, (int)StartupEntryStatus.Active);
        Assert.Equal(1, (int)StartupEntryStatus.Inactive);
        Assert.Equal(2, (int)StartupEntryStatus.PathNotFound);
        Assert.Equal(3, (int)StartupEntryStatus.Unknown);
    }

    [Fact]
    public void StartupAnalysis_TechnicianName_IsSettable()
    {
        var analysis = new StartupAnalysis
        {
            TechnicianName = "Test Technician"
        };

        Assert.Equal("Test Technician", analysis.TechnicianName);
    }

    [Fact]
    public void StartupEntry_Notes_Appendable()
    {
        var entry = new StartupEntry();
        entry.Notes += "Ruta no encontrada. ";
        entry.Notes += "[Duplicado]";

        Assert.Contains("Ruta no encontrada", entry.Notes);
        Assert.Contains("[Duplicado]", entry.Notes);
    }
}
