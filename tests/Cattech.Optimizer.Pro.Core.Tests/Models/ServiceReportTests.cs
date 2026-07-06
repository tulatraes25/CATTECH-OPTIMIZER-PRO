using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Reports;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class ServiceReportTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void ServiceReport_DefaultValues_AreValid()
    {
        var report = new ServiceReport();

        Assert.NotNull(report.Id);
        Assert.NotEmpty(report.Id);
        Assert.NotNull(report.Client);
        Assert.NotNull(report.Equipment);
        Assert.NotNull(report.Service);
        Assert.True(report.CreatedAt <= DateTime.Now);
    }

    [Fact]
    public void ClientInfo_DefaultValues_AreEmpty()
    {
        var client = new ClientInfo();

        Assert.Equal(string.Empty, client.Name);
        Assert.Equal(string.Empty, client.Phone);
        Assert.Equal(string.Empty, client.Email);
        Assert.Equal(string.Empty, client.Company);
        Assert.Equal(string.Empty, client.Address);
        Assert.Equal(string.Empty, client.Notes);
    }

    [Fact]
    public void EquipmentInfo_DefaultValues_AreEmpty()
    {
        var equipment = new EquipmentInfo();

        Assert.Equal(string.Empty, equipment.Brand);
        Assert.Equal(string.Empty, equipment.Model);
        Assert.Equal(string.Empty, equipment.SerialNumber);
        Assert.Equal(string.Empty, equipment.EquipmentType);
        Assert.Equal(string.Empty, equipment.ServiceReason);
        Assert.Equal(string.Empty, equipment.OperatingSystem);
        Assert.Equal(string.Empty, equipment.Architecture);
        Assert.Equal(string.Empty, equipment.Processor);
        Assert.Equal(0, equipment.RamTotalGB);
        Assert.Equal(string.Empty, equipment.DiskType);
    }

    [Fact]
    public void ServiceReport_SerializeToJson_ProducesValidJson()
    {
        var report = new ServiceReport
        {
            Client = new ClientInfo
            {
                Name = "Juan Perez",
                Phone = "555-0100",
                Email = "juan@test.com",
                Company = "Test Corp",
                Address = "123 Main St",
                Notes = "Cliente frecuente"
            },
            Equipment = new EquipmentInfo
            {
                Brand = "Dell",
                Model = "Latitude 5520",
                SerialNumber = "ABC123",
                EquipmentType = "Notebook",
                ServiceReason = "Pantalla rota",
                EquipmentNotes = "Golpe en esquina",
                OperatingSystem = "Windows 11 Pro",
                Architecture = "x64",
                Processor = "Intel Core i7-1165G7",
                RamTotalGB = 16,
                PrimaryDisk = "NVMe SSD 512GB",
                DiskCapacityGB = 476,
                DiskFreeGB = 200,
                DiskType = "NVMe",
                ComputerName = "DESKTOP-001",
                CurrentUser = "usuario",
                WindowsEdition = "23H2",
                WindowsVersion = "Windows 11 Pro (Build 22631)"
            },
            Service = new ServiceInfo
            {
                ServiceDate = new DateTime(2024, 6, 15, 10, 30, 0),
                Reason = "Pantalla rota"
            },
            TechnicianName = "Tecnico Test"
        };

        var json = JsonSerializer.Serialize(report, SerializerOptions);

        Assert.Contains("Juan Perez", json);
        Assert.Contains("Dell", json);
        Assert.Contains("Latitude 5520", json);
        Assert.Contains("Pantalla rota", json);
        Assert.Contains("Tecnico Test", json);
        Assert.Contains("Intel Core i7-1165G7", json);
    }

    [Fact]
    public void ServiceReport_DeserializeFromJson_PreservesAllFields()
    {
        var original = new ServiceReport
        {
            Client = new ClientInfo
            {
                Name = "Maria Lopez",
                Email = "maria@test.com",
                Company = "另一家公司"
            },
            Equipment = new EquipmentInfo
            {
                Brand = "HP",
                Model = "ProBook 450",
                EquipmentType = "Notebook",
                ServiceReason = "No enciende",
                RamTotalGB = 8,
                DiskType = "SSD"
            },
            TechnicianName = "Tech User"
        };

        var json = JsonSerializer.Serialize(original, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ServiceReport>(json, SerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("Maria Lopez", deserialized!.Client.Name);
        Assert.Equal("maria@test.com", deserialized.Client.Email);
        Assert.Equal("HP", deserialized.Equipment.Brand);
        Assert.Equal("ProBook 450", deserialized.Equipment.Model);
        Assert.Equal("Notebook", deserialized.Equipment.EquipmentType);
        Assert.Equal("No enciende", deserialized.Equipment.ServiceReason);
        Assert.Equal(8, deserialized.Equipment.RamTotalGB);
        Assert.Equal("SSD", deserialized.Equipment.DiskType);
        Assert.Equal("Tech User", deserialized.TechnicianName);
    }

    [Fact]
    public void ServiceReport_Id_IsGenerated()
    {
        var report1 = new ServiceReport();
        var report2 = new ServiceReport();

        Assert.NotEqual(report1.Id, report2.Id);
        Assert.Equal(8, report1.Id.Length);
    }

    [Fact]
    public void ServiceInfo_DefaultActions_AreEmptyLists()
    {
        var service = new ServiceInfo();

        Assert.NotNull(service.ActionsPerformed);
        Assert.Empty(service.ActionsPerformed);
        Assert.NotNull(service.Recommendations);
        Assert.Empty(service.Recommendations);
    }

    [Fact]
    public void EquipmentInfo_PurchaseDate_IsNullable()
    {
        var equipment = new EquipmentInfo();

        Assert.Null(equipment.PurchaseDate);

        equipment.PurchaseDate = new DateTime(2023, 1, 15);
        Assert.Equal(new DateTime(2023, 1, 15), equipment.PurchaseDate);
    }
}
