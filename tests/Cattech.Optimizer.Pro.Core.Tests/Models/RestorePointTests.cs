using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class RestorePointTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    [Fact]
    public void RestorePointStatus_DefaultValues_AreValid()
    {
        var status = new RestorePointStatus();

        Assert.False(status.IsAdministrator);
        Assert.False(status.IsSystemRestoreAvailable);
        Assert.False(status.IsProtectionEnabled);
        Assert.False(status.CanCreatePoint);
        Assert.NotNull(status.Warnings);
        Assert.Empty(status.Warnings);
    }

    [Fact]
    public void RestorePointStatus_CanCreatePoint_RequiresAllConditions()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = true,
            IsSystemRestoreAvailable = true,
            IsProtectionEnabled = true
        };

        Assert.True(status.CanCreatePoint);
    }

    [Fact]
    public void RestorePointStatus_CanCreatePoint_FailsWithoutAdmin()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = false,
            IsSystemRestoreAvailable = true,
            IsProtectionEnabled = true
        };

        Assert.False(status.CanCreatePoint);
        Assert.Contains("administrador", status.CannotCreateReason);
    }

    [Fact]
    public void RestorePointStatus_CanCreatePoint_FailsWithoutProtection()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = true,
            IsSystemRestoreAvailable = true,
            IsProtectionEnabled = false
        };

        Assert.False(status.CanCreatePoint);
        Assert.Contains("protección", status.CannotCreateReason);
    }

    [Fact]
    public void RestorePointStatus_CanCreatePoint_FailsWithoutService()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = true,
            IsSystemRestoreAvailable = false,
            IsProtectionEnabled = true
        };

        Assert.False(status.CanCreatePoint);
        Assert.Contains("servicio", status.CannotCreateReason);
    }

    [Fact]
    public void RestorePointMethod_HasAllValues()
    {
        Assert.Equal(0, (int)RestorePointMethod.Unknown);
        Assert.Equal(1, (int)RestorePointMethod.PowerShellCheckpoint);
        Assert.Equal(2, (int)RestorePointMethod.WmiSystemRestore);
        Assert.Equal(3, (int)RestorePointMethod.PowerShellDisable);
        Assert.Equal(4, (int)RestorePointMethod.ManualRecommendation);
    }

    [Fact]
    public void RestorePointResult_DefaultValues_AreValid()
    {
        var result = new RestorePointResult();

        Assert.NotNull(result.Id);
        Assert.NotEmpty(result.Id);
        Assert.Equal(8, result.Id.Length);
        Assert.True(result.RequestedAt <= DateTime.Now);
        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.ErrorMessage);
        Assert.False(result.RequiresAdministrator);
    }

    [Fact]
    public void RestorePointResult_SerializeToJson_ProducesValidJson()
    {
        var result = new RestorePointResult
        {
            RestorePointName = "CATTECH - Test Point",
            Success = true,
            MethodUsed = RestorePointMethod.PowerShellCheckpoint,
            TechnicianName = "Tecnico Test",
            Notes = "Punto creado exitosamente"
        };

        var json = JsonSerializer.Serialize(result, SerializerOptions);

        Assert.Contains("CATTECH - Test Point", json);
        Assert.Contains("Tecnico Test", json);
        Assert.Contains("Punto creado exitosamente", json);
    }

    [Fact]
    public void RestorePointResult_DeserializeFromJson_PreservesAllFields()
    {
        var original = new RestorePointResult
        {
            RestorePointName = "Test Point",
            Success = true,
            MethodUsed = RestorePointMethod.WmiSystemRestore,
            ErrorMessage = "",
            ErrorCode = "",
            RequiresAdministrator = true,
            ProtectionEnabled = true,
            TechnicianName = "Tech User",
            Notes = "Test notes"
        };

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<RestorePointResult>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("Test Point", deserialized!.RestorePointName);
        Assert.True(deserialized.Success);
        Assert.Equal(RestorePointMethod.WmiSystemRestore, deserialized.MethodUsed);
        Assert.True(deserialized.RequiresAdministrator);
        Assert.True(deserialized.ProtectionEnabled);
        Assert.Equal("Tech User", deserialized.TechnicianName);
    }

    [Fact]
    public void RestorePointResult_ErrorResult_HasCorrectFields()
    {
        var result = new RestorePointResult
        {
            Success = false,
            ErrorMessage = "Acceso denegado",
            ErrorCode = "ACCESS_DENIED",
            RequiresAdministrator = true
        };

        Assert.False(result.Success);
        Assert.Equal("Acceso denegado", result.ErrorMessage);
        Assert.Equal("ACCESS_DENIED", result.ErrorCode);
        Assert.True(result.RequiresAdministrator);
    }

    [Fact]
    public void RestorePointResult_FrequencyLimit_HasCorrectFields()
    {
        var result = new RestorePointResult
        {
            Success = false,
            ErrorMessage = "Ya se creó un punto recientemente",
            ErrorCode = "FREQUENCY_LIMIT"
        };

        Assert.False(result.Success);
        Assert.Contains("recientemente", result.ErrorMessage);
        Assert.Equal("FREQUENCY_LIMIT", result.ErrorCode);
    }

    [Fact]
    public void RestorePointResult_ProtectionDisabled_HasCorrectFields()
    {
        var result = new RestorePointResult
        {
            Success = false,
            ErrorMessage = "Protección del sistema deshabilitada",
            ErrorCode = "PROTECTION_DISABLED"
        };

        Assert.False(result.Success);
        Assert.Contains("deshabilitada", result.ErrorMessage);
        Assert.Equal("PROTECTION_DISABLED", result.ErrorCode);
    }

    [Fact]
    public void RestorePointResult_MethodUsed_DefaultsToUnknown()
    {
        var result = new RestorePointResult();
        Assert.Equal(RestorePointMethod.Unknown, result.MethodUsed);
    }

    [Fact]
    public void RestorePointResult_StandardOutput_IsSettable()
    {
        var result = new RestorePointResult
        {
            StandardOutput = "Process output here"
        };

        Assert.Equal("Process output here", result.StandardOutput);
    }

    [Fact]
    public void RestorePointResultSummary_DefaultValues_AreValid()
    {
        var summary = new RestorePointResultSummary();

        Assert.Equal(string.Empty, summary.Id);
        Assert.Equal(string.Empty, summary.RestorePointName);
        Assert.False(summary.Success);
        Assert.Equal(string.Empty, summary.ErrorMessage);
    }
}
