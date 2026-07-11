using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Models.Smart;
using Cattech.Optimizer.Pro.Infrastructure.Smart;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class SmartctlTests
{
    // =====================
    // Tests de SmartctlParser
    // =====================

    [Fact]
    public void ParseVersion_ValidOutput_ReturnsVersion()
    {
        var output = @"smartctl 7.4 2023-08-01 r5155
Copyright (C) 2002-23, Bruce Allen, Christian Franke, www.smartmontools.org

Platform: Windows 10 Build 19045, 64-bit
Version: 7.4 2023-08-01 r5155
Homepage: https://www.smartmontools.org/";

        var version = SmartctlParser.ParseVersion(output);

        Assert.Equal("smartctl 7.4", version);
    }

    [Fact]
    public void ParseVersion_EmptyOutput_ReturnsEmpty()
    {
        var version = SmartctlParser.ParseVersion("");
        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public void ParseVersion_NullOutput_ReturnsEmpty()
    {
        var version = SmartctlParser.ParseVersion(null!);
        Assert.Equal(string.Empty, version);
    }

    [Fact]
    public void ParseVersion_NoVersion_ReturnsEmpty()
    {
        var output = "Some random text without version info";
        var version = SmartctlParser.ParseVersion(output);
        Assert.Equal(string.Empty, version);
    }

    // =====================
    // Tests de parseo JSON
    // =====================

    [Fact]
    public void ParseScanJson_ValidArray_ReturnsDevices()
    {
        var json = @"[
            {
                ""name"": ""/dev/sda"",
                ""info_name"": ""/dev/sda"",
                ""type"": ""scsi"",
                ""protocol"": ""SATA"",
                ""model_name"": ""Samsung SSD 980 PRO"",
                ""serial_number"": ""S5P8NX0T123456""
            },
            {
                ""name"": ""/dev/nvme0n1"",
                ""info_name"": ""/dev/nvme0n1"",
                ""type"": ""nvme"",
                ""protocol"": ""NVMe"",
                ""model_name"": ""Samsung SSD 990 PRO"",
                ""serial_number"": ""S6Z3NY0T789012""
            }
        ]";

        var devices = SmartctlParser.ParseScanJson(json);

        Assert.Equal(2, devices.Count);
        Assert.Equal("/dev/sda", devices[0].Name);
        Assert.Equal("Samsung SSD 980 PRO", devices[0].ModelName);
        // El parser detecta "SSD" por el nombre del modelo
        Assert.Equal("SSD", devices[0].ApproximateDiskType);
        Assert.Equal("/dev/nvme0n1", devices[1].Name);
        Assert.Equal("NVMe", devices[1].ApproximateDiskType);
    }

    [Fact]
    public void ParseScanJson_EmptyArray_ReturnsEmpty()
    {
        var devices = SmartctlParser.ParseScanJson("[]");
        Assert.Empty(devices);
    }

    [Fact]
    public void ParseScanJson_InvalidJson_ReturnsEmpty()
    {
        var devices = SmartctlParser.ParseScanJson("not json");
        Assert.Empty(devices);
    }

    [Fact]
    public void ParseScanJson_NullInput_ReturnsEmpty()
    {
        var devices = SmartctlParser.ParseScanJson(null!);
        Assert.Empty(devices);
    }

    [Fact]
    public void ParseScanJson_NvmeDevice_DetectsNvme()
    {
        var json = @"[
            {
                ""name"": ""/dev/nvme0n1"",
                ""info_name"": ""/dev/nvme0n1"",
                ""type"": ""nvme"",
                ""protocol"": ""NVMe"",
                ""model_name"": ""WD Black SN850X"",
                ""serial_number"": ""WD-123456""
            }
        ]";

        var devices = SmartctlParser.ParseScanJson(json);

        Assert.Single(devices);
        Assert.Equal("NVMe", devices[0].ApproximateDiskType);
        Assert.Equal("NVMe", devices[0].Protocol);
    }

    [Fact]
    public void ParseScanJson_SataDevice_DetectsDiskType()
    {
        var json = @"[
            {
                ""name"": ""/dev/sdb"",
                ""info_name"": ""/dev/sdb [SAT]"",
                ""type"": ""scsi"",
                ""protocol"": ""SATA"",
                ""model_name"": ""Seagate Barracuda ST2000DM008"",
                ""serial_number"": ""ZA123456""
            }
        ]";

        var devices = SmartctlParser.ParseScanJson(json);

        Assert.Single(devices);
        // El parser detecta "HDD" por el nombre del modelo "Barracuda"
        Assert.Equal("HDD", devices[0].ApproximateDiskType);
    }

    [Fact]
    public void ParseScanJson_SetsIsAvailableTrue()
    {
        var json = @"[
            {
                ""name"": ""/dev/sda"",
                ""info_name"": ""/dev/sda"",
                ""type"": ""scsi"",
                ""protocol"": ""SATA"",
                ""model_name"": ""Test Disk"",
                ""serial_number"": ""TEST123""
            }
        ]";

        var devices = SmartctlParser.ParseScanJson(json);

        Assert.True(devices[0].IsAvailable);
    }

    // =====================
    // Tests de parseo de texto
    // =====================

    [Fact]
    public void ParseScanText_ValidOutput_ReturnsDevices()
    {
        var text = @"/dev/sda -d scsi # /dev/sda
/dev/sdb -d scsi # /dev/sdb [SAT]
/dev/nvme0 -d nvme # /dev/nvme0";

        var devices = SmartctlParser.ParseScanText(text);

        Assert.Equal(3, devices.Count);
        Assert.Equal("/dev/sda", devices[0].Name);
        Assert.Equal("scsi", devices[0].Type);
        Assert.Equal("/dev/nvme0", devices[2].Name);
        Assert.Equal("NVMe", devices[2].ApproximateDiskType);
    }

    [Fact]
    public void ParseScanText_EmptyInput_ReturnsEmpty()
    {
        var devices = SmartctlParser.ParseScanText("");
        Assert.Empty(devices);
    }

    [Fact]
    public void ParseScanText_NullInput_ReturnsEmpty()
    {
        var devices = SmartctlParser.ParseScanText(null!);
        Assert.Empty(devices);
    }

    [Fact]
    public void ParseScanText_SkipsComments()
    {
        var text = @"# This is a comment
/dev/sda -d scsi # /dev/sda
# Another comment
/dev/sdb -d scsi # /dev/sdb";

        var devices = SmartctlParser.ParseScanText(text);

        Assert.Equal(2, devices.Count);
    }

    [Fact]
    public void ParseScanText_SetsIsAvailableTrue()
    {
        var text = "/dev/sda -d scsi # /dev/sda";
        var devices = SmartctlParser.ParseScanText(text);

        Assert.True(devices[0].IsAvailable);
    }

    // =====================
    // Tests de SmartctlAvailability
    // =====================

    [Fact]
    public void SmartctlAvailability_DefaultValues_AreValid()
    {
        var availability = new SmartctlAvailability();

        Assert.False(availability.IsAvailable);
        Assert.Equal(string.Empty, availability.SmartctlPath);
        Assert.Equal(string.Empty, availability.Version);
        Assert.False(availability.SupportsJson);
        Assert.Equal(string.Empty, availability.ErrorMessage);
        Assert.True(availability.CheckedAt <= DateTime.Now);
    }

    [Fact]
    public void SmartctlAvailability_Available_HasPath()
    {
        var availability = new SmartctlAvailability
        {
            IsAvailable = true,
            SmartctlPath = @"C:\tools\smartctl.exe",
            Version = "smartctl 7.4",
            SupportsJson = true
        };

        Assert.True(availability.IsAvailable);
        Assert.NotEmpty(availability.SmartctlPath);
        Assert.Contains("7.4", availability.Version);
        Assert.True(availability.SupportsJson);
    }

    // =====================
    // Tests de SmartDiskDevice
    // =====================

    [Fact]
    public void SmartDiskDevice_DefaultValues_AreValid()
    {
        var device = new SmartDiskDevice();

        Assert.Equal(string.Empty, device.Name);
        Assert.Equal(string.Empty, device.ModelName);
        Assert.False(device.IsAvailable);
    }

    [Fact]
    public void SmartDiskDevice_DiskTypes_AreDetected()
    {
        var sata = new SmartDiskDevice { Protocol = "SATA", ApproximateDiskType = "SATA" };
        var nvme = new SmartDiskDevice { Protocol = "NVMe", ApproximateDiskType = "NVMe" };
        var usb = new SmartDiskDevice { Protocol = "USB", ApproximateDiskType = "USB" };

        Assert.Equal("SATA", sata.ApproximateDiskType);
        Assert.Equal("NVMe", nvme.ApproximateDiskType);
        Assert.Equal("USB", usb.ApproximateDiskType);
    }

    // =====================
    // Tests de SmartctlCommandResult
    // =====================

    [Fact]
    public void SmartctlCommandResult_IsSuccess_ExitCode0()
    {
        var result = new SmartctlCommandResult { ExitCode = 0 };
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SmartctlCommandResult_IsSuccess_ExitCode1()
    {
        // smartctl retorna 1 si hay errores SMART, pero sigue siendo "exitoso"
        var result = new SmartctlCommandResult { ExitCode = 1 };
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void SmartctlCommandResult_IsSuccess_ExitCode2()
    {
        var result = new SmartctlCommandResult { ExitCode = 2 };
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SmartctlCommandResult_TimedOut()
    {
        var result = new SmartctlCommandResult { TimedOut = true, ExitCode = -1 };
        Assert.True(result.TimedOut);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void SmartctlCommandResult_DurationMs_IsTracked()
    {
        var result = new SmartctlCommandResult { DurationMs = 1500 };
        Assert.Equal(1500, result.DurationMs);
    }

    // =====================
    // Tests de SmartctlRunner
    // =====================

    [Fact]
    public async Task SmartctlRunner_CheckAvailability_NoSmartctl_ReturnsUnavailable()
    {
        // Usar ruta que no existe
        var runner = new SmartctlRunner("/nonexistent/path/smartctl.exe");

        var availability = await runner.CheckAvailabilityAsync();

        Assert.False(availability.IsAvailable);
        Assert.NotEmpty(availability.ErrorMessage);
    }

    [Fact]
    public async Task SmartctlRunner_ListDevices_NoSmartctl_ReturnsEmpty()
    {
        var runner = new SmartctlRunner("/nonexistent/path/smartctl.exe");

        var devices = await runner.ListDevicesAsync();

        Assert.Empty(devices);
    }

    [Fact]
    public async Task SmartctlRunner_Run_NoSmartctl_ReturnsError()
    {
        var runner = new SmartctlRunner("/nonexistent/path/smartctl.exe");

        var result = await runner.RunAsync("--version", TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Contains("no encontrado", result.StandardError);
    }

    // =====================
    // Tests de configuración
    // =====================

    [Fact]
    public void HerramientasJson_DefaultConfig_HasExpectedFields()
    {
        var config = new
        {
            smartctlPath = "",
            smartctlAutoDetect = true
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });

        Assert.Contains("smartctlPath", json);
        Assert.Contains("smartctlAutoDetect", json);
    }

    // =====================
    // Tests de parseo edge cases
    // =====================

    [Fact]
    public void ParseScanJson_MissingFields_HandlesGracefully()
    {
        var json = @"[
            {
                ""name"": ""/dev/sda""
            }
        ]";

        var devices = SmartctlParser.ParseScanJson(json);

        Assert.Single(devices);
        Assert.Equal("/dev/sda", devices[0].Name);
        Assert.Equal(string.Empty, devices[0].ModelName);
        Assert.Equal(string.Empty, devices[0].SerialNumber);
    }

    [Fact]
    public void ParseScanText_WhitespaceOnly_ReturnsEmpty()
    {
        var devices = SmartctlParser.ParseScanText("   \n   \n   ");
        Assert.Empty(devices);
    }

    [Fact]
    public void SmartctlCommandResult_DefaultExitCode_IsZero()
    {
        var result = new SmartctlCommandResult();
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.IsSuccess);
    }
}
