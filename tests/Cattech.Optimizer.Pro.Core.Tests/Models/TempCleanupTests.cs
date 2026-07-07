using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Cleanup;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class TempCleanupTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void TempCleanupTarget_DefaultValues_AreValid()
    {
        var target = new TempCleanupTarget();

        Assert.NotNull(target.Id);
        Assert.NotEmpty(target.Id);
        Assert.Equal(8, target.Id.Length);
        Assert.False(target.IsSelected);
        Assert.False(target.IsSystemLocation);
        Assert.False(target.IsScanned);
        Assert.Equal(0, target.EstimatedSizeBytes);
        Assert.Equal(0, target.EstimatedFileCount);
    }

    [Fact]
    public void TempCleanupTarget_SizeMB_CalculatesCorrectly()
    {
        var target = new TempCleanupTarget
        {
            EstimatedSizeBytes = 500L * 1024 * 1024 // 500 MB
        };

        Assert.Equal(500, target.EstimatedSizeMB);
    }

    [Fact]
    public void CleanupTargets_GetDefaultTargets_ReturnsValidTargets()
    {
        var targets = CleanupTargets.GetDefaultTargets();

        Assert.NotEmpty(targets);
        Assert.Contains(targets, t => t.Id == "USER_TEMP");
        Assert.Contains(targets, t => t.Id == "WINDOWS_TEMP");
        Assert.Contains(targets, t => t.Id == "RECYCLE_BIN");
        Assert.Contains(targets, t => t.Id == "THUMBNAILS");
    }

    [Fact]
    public void CleanupTargets_UserTemp_IsSelectedByDefault()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var userTemp = targets.First(t => t.Id == "USER_TEMP");

        Assert.True(userTemp.IsSelected);
        Assert.False(userTemp.IsSystemLocation);
        Assert.Equal(CleanupRiskLevel.Low, userTemp.RiskLevel);
    }

    [Fact]
    public void CleanupTargets_WindowsTemp_IsNotSelectedByDefault()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var winTemp = targets.First(t => t.Id == "WINDOWS_TEMP");

        Assert.False(winTemp.IsSelected);
        Assert.True(winTemp.IsSystemLocation);
    }

    [Fact]
    public void CleanupTargets_RecycleBin_IsOptional()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var recycleBin = targets.First(t => t.Id == "RECYCLE_BIN");

        Assert.True(recycleBin.IsOptional);
        Assert.False(recycleBin.IsSelected);
        Assert.Equal(CleanupRiskLevel.Medium, recycleBin.RiskLevel);
    }

    [Fact]
    public void CleanupTargets_NoProhibitedLocations()
    {
        var targets = CleanupTargets.GetDefaultTargets();
        var allPaths = string.Join(" ", targets.Select(t => t.Path.ToLowerInvariant()));

        // No debe incluir Descargas, Documentos, Escritorio
        Assert.DoesNotContain("downloads", allPaths);
        Assert.DoesNotContain("documents", allPaths);
        Assert.DoesNotContain("desktop", allPaths);
    }

    [Fact]
    public void CleanupRiskLevel_HasAllValues()
    {
        Assert.Equal(0, (int)CleanupRiskLevel.Low);
        Assert.Equal(1, (int)CleanupRiskLevel.Medium);
    }

    [Fact]
    public void TempCleanupResult_DefaultValues_AreValid()
    {
        var result = new TempCleanupResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.Equal(8, result.Id.Length);
        Assert.True(result.StartedAt <= DateTime.Now);
        Assert.Equal(0, result.DeletedFiles);
        Assert.Equal(0, result.SkippedFiles);
        Assert.Equal(0, result.FailedFiles);
        Assert.True(result.Errors != null && result.Errors.Count == 0);
    }

    [Fact]
    public void TempCleanupResult_IsSuccess_WhenNoErrors()
    {
        var result = new TempCleanupResult();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TempCleanupResult_HasWarnings_WhenSkipped()
    {
        var result = new TempCleanupResult { SkippedFiles = 5 };
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void TempCleanupResult_HasWarnings_WhenFailed()
    {
        var result = new TempCleanupResult { FailedFiles = 2 };
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void TempCleanupResult_Duration_CalculatesCorrectly()
    {
        var result = new TempCleanupResult
        {
            StartedAt = new DateTime(2024, 1, 1, 10, 0, 0),
            FinishedAt = new DateTime(2024, 1, 1, 10, 0, 30)
        };

        Assert.Equal(30, result.Duration.TotalSeconds);
    }

    [Fact]
    public void TempCleanupResult_SerializeToJson_ProducesValidJson()
    {
        var result = new TempCleanupResult
        {
            DeletedFiles = 100,
            DeletedBytes = 500L * 1024 * 1024,
            SkippedFiles = 5,
            FailedFiles = 2
        };

        result.Errors.Add(new CleanupError
        {
            FilePath = @"C:\temp\locked.lock",
            Message = "Archivo bloqueado",
            ErrorType = "Locked"
        });

        var json = JsonSerializer.Serialize(result, SerializerOptions);

        Assert.Contains("deletedFiles", json);
        Assert.Contains("skippedFiles", json);
        Assert.Contains("Archivo bloqueado", json);
    }

    [Fact]
    public void CleanupError_DefaultValues_AreValid()
    {
        var error = new CleanupError();

        Assert.Equal(string.Empty, error.FilePath);
        Assert.Equal(string.Empty, error.Message);
        Assert.Equal(string.Empty, error.ErrorType);
    }

    [Fact]
    public void TargetResult_DefaultValues_AreValid()
    {
        var targetResult = new TargetResult();

        Assert.Equal(string.Empty, targetResult.TargetId);
        Assert.Equal(0, targetResult.DeletedFiles);
        Assert.Equal(0L, targetResult.DeletedBytes);
        Assert.Equal(0, targetResult.SkippedFiles);
        Assert.Equal(0, targetResult.FailedFiles);
    }

    [Fact]
    public void TempCleanupResultSummary_DefaultValues_AreValid()
    {
        var summary = new TempCleanupResultSummary();

        Assert.Equal(string.Empty, summary.Id);
        Assert.Equal(0, summary.DeletedMB);
        Assert.Equal(0, summary.DeletedFiles);
        Assert.False(summary.HasWarnings);
    }
}
