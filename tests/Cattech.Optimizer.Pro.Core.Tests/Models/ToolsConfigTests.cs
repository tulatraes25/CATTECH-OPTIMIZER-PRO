using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Tests de la persistencia de herramientas externas (config/herramientas.json).
/// Todos usan carpetas temporales: nunca tocan el config real del repositorio
/// ni el del ejecutable.
/// </summary>
public sealed class ToolsConfigTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public ToolsConfigTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"cattech_tools_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "config", "herramientas.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public void DefaultConfig_IsAutoDetectWithoutPath()
    {
        var config = ToolsConfig.CreateDefault();

        Assert.Equal(string.Empty, config.SmartctlPath);
        Assert.True(config.SmartctlAutoDetect);
    }

    [Fact]
    public void Clone_IsIndependent()
    {
        var original = new ToolsConfig { SmartctlPath = @"C:\tools\smartctl.exe", SmartctlAutoDetect = false };

        var clone = original.Clone();
        clone.SmartctlPath = @"D:\otro\smartctl.exe";
        clone.SmartctlAutoDetect = true;

        Assert.Equal(@"C:\tools\smartctl.exe", original.SmartctlPath);
        Assert.False(original.SmartctlAutoDetect);
    }

    [Fact]
    public void DefaultConstructor_UsesAppConfigDirectory()
    {
        var service = new ToolsConfigService();

        Assert.Equal(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "herramientas.json"),
            service.ConfigFilePath);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ReturnsDefaultsWithoutCreatingFile()
    {
        var service = new ToolsConfigService(_testFile);

        var config = await service.LoadAsync();

        Assert.Equal(string.Empty, config.SmartctlPath);
        Assert.True(config.SmartctlAutoDetect);
        Assert.Equal(_testFile, service.ConfigFilePath);
        Assert.False(File.Exists(_testFile));
    }

    [Fact]
    public async Task LoadAsync_ReadsExistingCamelCaseFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_testFile)!);
        await File.WriteAllTextAsync(
            _testFile,
            """
            {
              "smartctlPath": "C:\\tools\\smartmontools\\smartctl.exe",
              "smartctlAutoDetect": false
            }
            """);

        var config = await new ToolsConfigService(_testFile).LoadAsync();

        Assert.Equal(@"C:\tools\smartmontools\smartctl.exe", config.SmartctlPath);
        Assert.False(config.SmartctlAutoDetect);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_testFile)!);
        await File.WriteAllTextAsync(_testFile, "{ esto no es json valido !!!");

        var config = await new ToolsConfigService(_testFile).LoadAsync();

        Assert.Equal(string.Empty, config.SmartctlPath);
        Assert.True(config.SmartctlAutoDetect);
    }

    [Fact]
    public async Task LoadAsync_MissingAutoDetectProperty_DefaultsToTrue()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_testFile)!);
        await File.WriteAllTextAsync(
            _testFile,
            """{ "smartctlPath": "C:\\tools\\smartctl.exe" }""");

        var config = await new ToolsConfigService(_testFile).LoadAsync();

        Assert.Equal(@"C:\tools\smartctl.exe", config.SmartctlPath);
        Assert.True(config.SmartctlAutoDetect);
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryAndWritesExpectedCamelCaseFormat()
    {
        var service = new ToolsConfigService(_testFile);
        Assert.False(Directory.Exists(Path.GetDirectoryName(_testFile)));

        await service.SaveAsync(new ToolsConfig
        {
            SmartctlPath = @"C:\tools\smartctl.exe",
            SmartctlAutoDetect = false
        });

        Assert.True(File.Exists(_testFile));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_testFile));
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(@"C:\tools\smartctl.exe", root.GetProperty("smartctlPath").GetString());
        Assert.False(root.GetProperty("smartctlAutoDetect").GetBoolean());
        Assert.Equal(2, root.EnumerateObject().Count());

        // UTF-8 sin BOM: el JSON debe abrir con '{'.
        var bytes = await File.ReadAllBytesAsync(_testFile);
        Assert.Equal((byte)'{', bytes[0]);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsConfiguredPath()
    {
        var service = new ToolsConfigService(_testFile);
        var path = Path.Combine(_testDir, "bin", "smartctl.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, string.Empty);

        await service.SaveAsync(new ToolsConfig { SmartctlPath = path, SmartctlAutoDetect = false });

        var loaded = await new ToolsConfigService(_testFile).LoadAsync();

        Assert.Equal(path, loaded.SmartctlPath);
        Assert.False(loaded.SmartctlAutoDetect);
    }

    [Fact]
    public async Task SaveAsync_TrimsConfiguredPath()
    {
        var service = new ToolsConfigService(_testFile);

        await service.SaveAsync(new ToolsConfig
        {
            SmartctlPath = "  C:\\tools\\smartctl.exe  ",
            SmartctlAutoDetect = true
        });

        var loaded = await service.LoadAsync();
        Assert.Equal(@"C:\tools\smartctl.exe", loaded.SmartctlPath);
    }

    [Fact]
    public async Task SaveAsync_IsAtomicAndLeavesNoTemporaryFiles()
    {
        var service = new ToolsConfigService(_testFile);

        await service.SaveAsync(new ToolsConfig { SmartctlPath = @"C:\a.exe", SmartctlAutoDetect = true });
        await service.SaveAsync(new ToolsConfig { SmartctlPath = @"C:\b.exe", SmartctlAutoDetect = false });

        var files = Directory.GetFiles(Path.GetDirectoryName(_testFile)!);
        Assert.Single(files);
        Assert.Equal(_testFile, files[0]);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(_testFile)!, "*.tmp"));

        var loaded = await service.LoadAsync();
        Assert.Equal(@"C:\b.exe", loaded.SmartctlPath);
        Assert.False(loaded.SmartctlAutoDetect);
    }

    [Fact]
    public async Task SaveAsync_WhenTargetCannotBeReplaced_ThrowsAndLeavesNoTemporaryFiles()
    {
        // El destino es un directorio: File.Move(overwrite) falla de forma determinista
        // en Windows sin depender de permisos reales del equipo.
        var targetAsDirectory = Path.Combine(_testDir, "herramientas.json");
        Directory.CreateDirectory(targetAsDirectory);
        var service = new ToolsConfigService(targetAsDirectory);

        await Assert.ThrowsAnyAsync<Exception>(() => service.SaveAsync(
            new ToolsConfig { SmartctlPath = @"C:\tools\smartctl.exe", SmartctlAutoDetect = true }));

        Assert.Empty(Directory.GetFiles(_testDir, "*.tmp"));
        Assert.True(Directory.Exists(targetAsDirectory));
    }
}
