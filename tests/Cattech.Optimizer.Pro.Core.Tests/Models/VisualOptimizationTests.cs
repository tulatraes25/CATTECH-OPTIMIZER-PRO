using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class VisualOptimizationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void VisualOptimizationSetting_DefaultValues_AreValid()
    {
        var setting = new VisualOptimizationSetting();

        Assert.Equal(string.Empty, setting.Id);
        Assert.Equal(string.Empty, setting.DisplayName);
        Assert.Equal(string.Empty, setting.RegistryPath);
        Assert.Equal(string.Empty, setting.RegistryValueName);
        Assert.Equal("DWORD", setting.ValueType);
        Assert.False(setting.RequiresRestart);
        Assert.False(setting.RequiresSignOut);
        Assert.False(setting.IsSelected);
        Assert.True(setting.IsSupported);
    }

    [Fact]
    public void VisualOptimizationSetting_IsAlreadyOptimized_CompareCorrectly()
    {
        var setting = new VisualOptimizationSetting
        {
            CurrentValue = "2",
            RecommendedValue = "2"
        };

        Assert.True(setting.IsAlreadyOptimized);

        setting.CurrentValue = "0";
        Assert.False(setting.IsAlreadyOptimized);
    }

    [Fact]
    public void VisualOptimizationPresets_GetDefaultSettings_ReturnsValid()
    {
        var settings = VisualOptimizationPresets.GetDefaultSettings();

        Assert.NotEmpty(settings);
        Assert.All(settings, s => Assert.False(string.IsNullOrEmpty(s.Id)));
        Assert.All(settings, s => Assert.False(string.IsNullOrEmpty(s.DisplayName)));
        Assert.All(settings, s => Assert.False(string.IsNullOrEmpty(s.RegistryPath)));
        Assert.All(settings, s => Assert.False(string.IsNullOrEmpty(s.RegistryValueName)));
    }

    [Fact]
    public void VisualOptimizationPresets_NoProhibitedSettings()
    {
        var settings = VisualOptimizationPresets.GetDefaultSettings();

        // No debe incluir ajustes de resolución, drivers, servicios, accesibilidad
        foreach (var setting in settings)
        {
            Assert.DoesNotContain("resolution", setting.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("driver", setting.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("service", setting.DisplayName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("accessibility", setting.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void VisualRiskLevel_HasAllValues()
    {
        Assert.Equal(0, (int)VisualRiskLevel.Low);
        Assert.Equal(1, (int)VisualRiskLevel.Medium);
    }

    [Fact]
    public void VisualOptimizationBackup_DefaultValues_AreValid()
    {
        var backup = new VisualOptimizationBackup();

        Assert.NotNull(backup.Id);
        Assert.NotEmpty(backup.Id);
        Assert.Equal(8, backup.Id.Length);
        Assert.True(backup.CreatedAt <= DateTime.Now);
        Assert.True(backup.CanRestore);
        Assert.Null(backup.RestoredAt);
    }

    [Fact]
    public void VisualOptimizationBackup_SerializeToJson_ProducesValidJson()
    {
        var backup = new VisualOptimizationBackup
        {
            SettingId = "ANIMATIONS",
            SettingName = "Desactivar animaciones",
            RegistryPath = @"Control Panel\Desktop",
            RegistryValueName = "UserPreferencesMask",
            OriginalValue = "9012038010000000",
            NewValue = "9012038010000000",
            ValueType = "BINARY",
            AppliedBy = "Tecnico Test"
        };

        var json = JsonSerializer.Serialize(backup, SerializerOptions);

        Assert.Contains("ANIMATIONS", json);
        Assert.Contains("Desactivar animaciones", json);
        Assert.Contains("Tecnico Test", json);
    }

    [Fact]
    public void VisualOptimizationResult_DefaultValues_AreValid()
    {
        var result = new VisualOptimizationResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.True(result.StartedAt <= DateTime.Now);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.RequiresRestart);
        Assert.False(result.RequiresSignOut);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void VisualOptimizationResult_IsSuccess_WhenNoErrors()
    {
        var result = new VisualOptimizationResult();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void VisualOptimizationResult_HasWarnings_WhenSkipped()
    {
        var result = new VisualOptimizationResult { SkippedCount = 3 };
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void VisualOptimizationResult_Duration_CalculatesCorrectly()
    {
        var result = new VisualOptimizationResult
        {
            StartedAt = new DateTime(2024, 1, 1, 10, 0, 0),
            FinishedAt = new DateTime(2024, 1, 1, 10, 0, 15)
        };

        Assert.Equal(15, result.Duration.TotalSeconds);
    }

    [Fact]
    public void VisualOptimizationResult_SerializeToJson_ProducesValidJson()
    {
        var result = new VisualOptimizationResult
        {
            AppliedCount = 5,
            SkippedCount = 2,
            FailedCount = 1,
            RequiresRestart = true
        };

        result.Errors.Add(new VisualOptimizationError
        {
            SettingId = "TEST",
            SettingName = "Test Setting",
            Message = "Error de prueba",
            ErrorType = "TestError"
        });

        var json = JsonSerializer.Serialize(result, SerializerOptions);

        Assert.Contains("appliedCount", json);
        Assert.Contains("requiresRestart", json);
        Assert.Contains("Error de prueba", json);
    }

    [Fact]
    public void VisualOptimizationError_DefaultValues_AreValid()
    {
        var error = new VisualOptimizationError();

        Assert.Equal(string.Empty, error.SettingId);
        Assert.Equal(string.Empty, error.SettingName);
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(string.Empty, error.ErrorType);
    }

    [Fact]
    public void VisualOptimizationSetting_BackupFieldValues_AreCorrect()
    {
        var setting = new VisualOptimizationSetting
        {
            Id = "TEST",
            DisplayName = "Test Setting",
            RegistryPath = @"SOFTWARE\Test",
            RegistryValueName = "TestValue",
            RecommendedValue = "1",
            ValueType = "DWORD"
        };

        Assert.Equal("TEST", setting.Id);
        Assert.Equal("Test Setting", setting.DisplayName);
        Assert.Equal(@"SOFTWARE\Test", setting.RegistryPath);
        Assert.Equal("TestValue", setting.RegistryValueName);
        Assert.Equal("1", setting.RecommendedValue);
        Assert.Equal("DWORD", setting.ValueType);
    }

    [Fact]
    public void VisualOptimizationResult_BackupsList_IsInitiallyEmpty()
    {
        var result = new VisualOptimizationResult();
        Assert.NotNull(result.Backups);
        Assert.Empty(result.Backups);
    }

    [Fact]
    public void VisualOptimizationResult_ErrorsList_IsInitiallyEmpty()
    {
        var result = new VisualOptimizationResult();
        Assert.NotNull(result.Errors);
        Assert.Empty(result.Errors);
    }
}
