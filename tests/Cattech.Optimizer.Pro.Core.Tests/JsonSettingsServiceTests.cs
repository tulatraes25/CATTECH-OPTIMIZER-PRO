using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Configuration;
using Cattech.Optimizer.Pro.Infrastructure.Data;

namespace Cattech.Optimizer.Pro.Core.Tests;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public JsonSettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"cattech_test_{Guid.NewGuid():N}");
        _testFile = Path.Combine(_testDir, "empresa.json");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_FileNotExist_ReturnsDefaults()
    {
        var service = new JsonSettingsService(_testFile);

        var settings = await service.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.Company.Name);
        Assert.Equal("es-AR", settings.Language);
    }

    [Fact]
    public async Task SaveSettingsAsync_CreatesFileAndDirectory()
    {
        var service = new JsonSettingsService(_testFile);
        var settings = new AppSettings
        {
            Company = new CompanyInfo { Name = "Test Corp" }
        };

        await service.SaveSettingsAsync(settings);

        Assert.True(File.Exists(_testFile));

        var json = await File.ReadAllTextAsync(_testFile);
        Assert.Contains("Test Corp", json);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesAllFields()
    {
        var service = new JsonSettingsService(_testFile);
        var original = new AppSettings
        {
            Company = new CompanyInfo
            {
                Name = "CATTECH",
                TechnicianName = "Juan",
                TaxId = "20-123",
                Phone = "555-0100",
                WhatsApp = "555-0101",
                Email = "j@cattech.com",
                Address = "Av. Siempre Viva 123",
                City = "Springfield",
                LogoPath = "/tmp/logo.png",
                PrimaryColor = "#FF0000",
                FooterLegend = "Pie de informe"
            },
            Language = "es-AR",
            Theme = "dark"
        };

        await service.SaveSettingsAsync(original);

        // Read the file directly to verify content
        var json = await File.ReadAllTextAsync(_testFile);
        Assert.Contains("CATTECH", json);
        Assert.Contains("Juan", json);
        Assert.Contains("20-123", json);

        // Now load and verify
        var loaded = await service.LoadSettingsAsync();

        Assert.Equal("CATTECH", loaded.Company.Name);
        Assert.Equal("Juan", loaded.Company.TechnicianName);
        Assert.Equal("20-123", loaded.Company.TaxId);
        Assert.Equal("555-0100", loaded.Company.Phone);
        Assert.Equal("555-0101", loaded.Company.WhatsApp);
        Assert.Equal("j@cattech.com", loaded.Company.Email);
        Assert.Equal("Av. Siempre Viva 123", loaded.Company.Address);
        Assert.Equal("Springfield", loaded.Company.City);
        Assert.Equal("/tmp/logo.png", loaded.Company.LogoPath);
        Assert.Equal("#FF0000", loaded.Company.PrimaryColor);
        Assert.Equal("Pie de informe", loaded.Company.FooterLegend);
        Assert.Equal("dark", loaded.Theme);
    }

    [Fact]
    public async Task LoadSettingsAsync_CorruptedJson_ReturnsDefaults()
    {
        await File.WriteAllTextAsync(_testFile, "{ this is not valid json !!!");
        var service = new JsonSettingsService(_testFile);

        var settings = await service.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.Equal(string.Empty, settings.Company.Name);
    }

    [Fact]
    public async Task SaveSettingsAsync_FiresSettingsChangedEvent()
    {
        var service = new JsonSettingsService(_testFile);
        AppSettings? eventSettings = null;

        service.SettingsChanged += (sender, s) => eventSettings = s;

        var settings = new AppSettings
        {
            Company = new CompanyInfo { Name = "Event Test" }
        };

        await service.SaveSettingsAsync(settings);

        Assert.NotNull(eventSettings);
        Assert.Equal("Event Test", eventSettings!.Company.Name);
    }

    [Fact]
    public async Task CurrentSettings_AfterSave_ReturnsSavedSettings()
    {
        var service = new JsonSettingsService(_testFile);
        var settings = new AppSettings
        {
            Company = new CompanyInfo { Name = "Cached" }
        };

        await service.SaveSettingsAsync(settings);

        Assert.Equal("Cached", service.CurrentSettings.Company.Name);
    }

    [Fact]
    public async Task SaveSettingsAsync_WritesIndentedJson()
    {
        var service = new JsonSettingsService(_testFile);
        var settings = new AppSettings
        {
            Company = new CompanyInfo { Name = "Pretty Print" }
        };

        await service.SaveSettingsAsync(settings);

        var json = await File.ReadAllTextAsync(_testFile);
        // Indented JSON has newlines
        Assert.Contains(Environment.NewLine, json);
        // Has proper indentation
        Assert.Contains("  ", json);
    }
}
