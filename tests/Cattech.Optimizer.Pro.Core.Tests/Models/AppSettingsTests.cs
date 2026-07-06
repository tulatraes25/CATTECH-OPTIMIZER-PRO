using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Configuration;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_AreValid()
    {
        var settings = new AppSettings();

        Assert.NotNull(settings.Company);
        Assert.NotNull(settings.Technician);
        Assert.Equal("es-AR", settings.Language);
        Assert.Equal("light", settings.Theme);
    }

    [Fact]
    public void CompanyInfo_DefaultValues_AreEmpty()
    {
        var company = new CompanyInfo();

        Assert.Equal(string.Empty, company.Name);
        Assert.Equal(string.Empty, company.TechnicianName);
        Assert.Equal(string.Empty, company.TaxId);
        Assert.Equal(string.Empty, company.Phone);
        Assert.Equal(string.Empty, company.WhatsApp);
        Assert.Equal(string.Empty, company.Email);
        Assert.Equal(string.Empty, company.Address);
        Assert.Equal(string.Empty, company.City);
        Assert.Equal(string.Empty, company.LogoPath);
        Assert.Equal("#0078D4", company.PrimaryColor);
        Assert.Equal(string.Empty, company.FooterLegend);
    }

    [Fact]
    public void AppSettings_SerializeToJson_ProducesValidJson()
    {
        var settings = new AppSettings
        {
            Company = new CompanyInfo
            {
                Name = "CATTECH Services",
                TechnicianName = "Juan Perez",
                TaxId = "20-12345678-9",
                Phone = "+54 11 1234-5678",
                WhatsApp = "+54 11 1234-5678",
                Email = "info@cattech.com",
                Address = "Av. Corrientes 1234",
                City = "Buenos Aires",
                LogoPath = "C:\\logos\\cattech.png",
                PrimaryColor = "#FF6600",
                FooterLegend = "Servicio tecnico certificado"
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(settings, options);

        Assert.Contains("CATTECH Services", json);
        Assert.Contains("Juan Perez", json);
        Assert.Contains("20-12345678-9", json);
        Assert.Contains("info@cattech.com", json);
        Assert.Contains("#FF6600", json);
    }

    [Fact]
    public void AppSettings_DeserializeFromJson_PreservesAllFields()
    {
        var original = new AppSettings
        {
            Company = new CompanyInfo
            {
                Name = "Test Company",
                TechnicianName = "Tech User",
                TaxId = "12345",
                Phone = "555-0100",
                WhatsApp = "555-0101",
                Email = "test@example.com",
                Address = "123 Main St",
                City = "Testville",
                LogoPath = "/tmp/logo.png",
                PrimaryColor = "#AABBCC",
                FooterLegend = "Test footer"
            },
            Language = "en-US",
            Theme = "dark"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Test Company", deserialized!.Company.Name);
        Assert.Equal("Tech User", deserialized.Company.TechnicianName);
        Assert.Equal("12345", deserialized.Company.TaxId);
        Assert.Equal("555-0100", deserialized.Company.Phone);
        Assert.Equal("555-0101", deserialized.Company.WhatsApp);
        Assert.Equal("test@example.com", deserialized.Company.Email);
        Assert.Equal("123 Main St", deserialized.Company.Address);
        Assert.Equal("Testville", deserialized.Company.City);
        Assert.Equal("/tmp/logo.png", deserialized.Company.LogoPath);
        Assert.Equal("#AABBCC", deserialized.Company.PrimaryColor);
        Assert.Equal("Test footer", deserialized.Company.FooterLegend);
        Assert.Equal("en-US", deserialized.Language);
        Assert.Equal("dark", deserialized.Theme);
    }

    [Fact]
    public void AppSettings_DeserializeFromMinimalJson_UsesDefaults()
    {
        var json = """{"company":{"name":"Only Name"}}""";
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var settings = JsonSerializer.Deserialize<AppSettings>(json, options);

        Assert.NotNull(settings);
        Assert.Equal("Only Name", settings!.Company.Name);
        Assert.Equal(string.Empty, settings.Company.TechnicianName);
        Assert.Equal("#0078D4", settings.Company.PrimaryColor);
        Assert.Equal("es-AR", settings.Language);
    }

    [Fact]
    public void AppSettings_CamelCaseJson_SerializesCorrectly()
    {
        var settings = new AppSettings
        {
            Company = new CompanyInfo { Name = "Test" }
        };

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(settings, options);

        Assert.Contains("\"company\"", json);
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"technicianName\"", json);
        Assert.Contains("\"taxId\"", json);
        Assert.Contains("\"logoPath\"", json);
        Assert.Contains("\"primaryColor\"", json);
        Assert.Contains("\"footerLegend\"", json);
    }
}
