using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Startup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class StartupBackupTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void StartupBackupRecord_DefaultValues_AreValid()
    {
        var record = new StartupBackupRecord();

        Assert.NotNull(record.Id);
        Assert.NotEmpty(record.Id);
        Assert.Equal(8, record.Id.Length);
        Assert.True(record.CreatedAt <= DateTime.Now);
        Assert.True(record.CanRestore);
        Assert.Null(record.RestoredAt);
        Assert.Null(record.RestoredBy);
    }

    [Fact]
    public void StartupBackupRecord_SerializeToJson_ProducesValidJson()
    {
        var record = new StartupBackupRecord
        {
            EntryId = "ABC12345",
            EntryName = "Discord",
            SourceType = StartupSourceType.RegistryRun,
            OriginalLocation = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            BackupLocation = @"HKCU\Software\CATTECH\OptimizerPro\DisabledStartup\Run\ABC12345",
            OriginalValue = @"C:\Users\test\AppData\Local\Discord\Update.exe",
            Command = @"C:\Users\test\AppData\Local\Discord\Update.exe",
            Publisher = "Discord",
            WasMicrosoft = false,
            DisabledBy = "Tecnico Test",
            Reason = "Aplicación de chat innecesaria"
        };

        var json = JsonSerializer.Serialize(record, SerializerOptions);

        Assert.Contains("Discord", json);
        Assert.Contains("Tecnico Test", json);
        Assert.Contains("ABC12345", json);
        Assert.Contains("entryName", json);
    }

    [Fact]
    public void StartupActionResult_HasAllValues()
    {
        Assert.Equal(0, (int)StartupActionResult.Success);
        Assert.Equal(1, (int)StartupActionResult.Failed);
        Assert.Equal(2, (int)StartupActionResult.SkippedMicrosoft);
        Assert.Equal(3, (int)StartupActionResult.SkippedUnsupportedSource);
        Assert.Equal(4, (int)StartupActionResult.AlreadyDisabled);
        Assert.Equal(5, (int)StartupActionResult.BackupFailed);
        Assert.Equal(6, (int)StartupActionResult.RestoreFailed);
        Assert.Equal(7, (int)StartupActionResult.NotFound);
    }

    [Fact]
    public void StartupDisableResult_DefaultValues_AreValid()
    {
        var result = new StartupDisableResult();

        Assert.Equal(string.Empty, result.EntryName);
        Assert.Equal(string.Empty, result.EntryId);
        Assert.Equal(StartupActionResult.Success, result.Result);
        Assert.Null(result.BackupId);
    }

    [Fact]
    public void StartupDisableSummary_Counts_AreInitialZero()
    {
        var summary = new StartupDisableSummary();

        Assert.Equal(0, summary.TotalAttempted);
        Assert.Equal(0, summary.SuccessCount);
        Assert.Equal(0, summary.FailedCount);
        Assert.Equal(0, summary.SkippedMicrosoftCount);
        Assert.Equal(0, summary.SkippedUnsupportedCount);
        Assert.NotNull(summary.Results);
        Assert.Empty(summary.Results);
    }

    [Fact]
    public void StartupBackupRecord_CanRestore_DefaultsToTrue()
    {
        var record = new StartupBackupRecord();
        Assert.True(record.CanRestore);
    }

    [Fact]
    public void StartupBackupRecord_RestoredAt_IsNullable()
    {
        var record = new StartupBackupRecord();
        Assert.Null(record.RestoredAt);

        record.RestoredAt = DateTime.Now;
        Assert.NotNull(record.RestoredAt);
    }

    [Fact]
    public void StartupBackupRecord_Notes_Appendable()
    {
        var record = new StartupBackupRecord();
        record.Notes += "Desactivado el 2024-01-01. ";
        record.Notes += "Restaurado el 2024-01-02.";

        Assert.Contains("Desactivado", record.Notes);
        Assert.Contains("Restaurado", record.Notes);
    }

    [Fact]
    public void StartupDisableSummary_Results_AreAccumulated()
    {
        var summary = new StartupDisableSummary();
        summary.Results.Add(new StartupDisableResult
        {
            EntryName = "Entry1",
            Result = StartupActionResult.Success
        });
        summary.Results.Add(new StartupDisableResult
        {
            EntryName = "Entry2",
            Result = StartupActionResult.SkippedMicrosoft,
            Message = "No se puede desactivar: entrada de Microsoft"
        });
        summary.Results.Add(new StartupDisableResult
        {
            EntryName = "Entry3",
            Result = StartupActionResult.Failed,
            Message = "Error: No se pudo acceder"
        });

        Assert.Equal(3, summary.Results.Count);
        Assert.Equal(1, summary.Results.Count(r => r.Result == StartupActionResult.Success));
        Assert.Equal(1, summary.Results.Count(r => r.Result == StartupActionResult.SkippedMicrosoft));
        Assert.Equal(1, summary.Results.Count(r => r.Result == StartupActionResult.Failed));
    }

    [Fact]
    public void StartupBackupRecord_DeserializeFromJson_PreservesAllFields()
    {
        var original = new StartupBackupRecord
        {
            EntryId = "TEST1234",
            EntryName = "TestApp",
            SourceType = StartupSourceType.StartupFolder,
            OriginalLocation = @"C:\Users\test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\test.lnk",
            BackupLocation = @"backups\startup\20240101-120000\test.lnk",
            Command = @"C:\Program Files\TestApp\app.exe",
            Publisher = "TestPublisher",
            WasMicrosoft = false,
            DisabledBy = "Tecnico",
            Reason = "Test reason"
        };

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<StartupBackupRecord>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("TEST1234", deserialized!.EntryId);
        Assert.Equal("TestApp", deserialized.EntryName);
        Assert.Equal(StartupSourceType.StartupFolder, deserialized.SourceType);
        Assert.Equal("Tecnico", deserialized.DisabledBy);
        Assert.Equal("Test reason", deserialized.Reason);
        Assert.True(deserialized.CanRestore);
    }
}
