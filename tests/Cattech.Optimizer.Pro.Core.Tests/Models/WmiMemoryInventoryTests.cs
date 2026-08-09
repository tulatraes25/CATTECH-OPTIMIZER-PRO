using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Hardware;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class WmiMemoryInventoryTests
{
    // =====================
    // Fake del reader WMI (nunca consulta WMI real)
    // =====================

    private sealed class FakeWmiMemoryReader : IWmiMemoryReader
    {
        public WmiMemorySnapshot Snapshot { get; set; } = new();
        public bool ThrowOnRead { get; set; }

        public WmiMemorySnapshot Read()
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("WMI falló");
            }

            return Snapshot;
        }
    }

    private static WmiPhysicalMemoryModule CreateModule(
        string deviceLocator = "DIMM 0",
        string bankLabel = "BANK 0",
        string manufacturer = "Kingston",
        string partNumber = "KF3200C16D4",
        string serialNumber = "SN123456",
        ulong? capacityBytes = 8UL * 1024 * 1024 * 1024,
        uint? speedMHz = 3200,
        uint? smbiosCode = 26,
        ushort? dataWidth = 64,
        ushort? totalWidth = 72,
        uint? attributes = 1)
    {
        return new WmiPhysicalMemoryModule
        {
            DeviceLocator = deviceLocator,
            BankLabel = bankLabel,
            Manufacturer = manufacturer,
            PartNumber = partNumber,
            SerialNumber = serialNumber,
            CapacityBytes = capacityBytes,
            ConfiguredClockSpeedMHz = speedMHz,
            SMBIOSMemoryTypeCode = smbiosCode,
            DataWidthBits = dataWidth,
            TotalWidthBits = totalWidth,
            Attributes = attributes
        };
    }

    private static WmiHardwareService CreateService(FakeWmiMemoryReader reader) => new(reader);

    private static WmiHardwareService CreateServiceWith(WmiMemorySnapshot snapshot)
        => CreateService(new FakeWmiMemoryReader { Snapshot = snapshot });

    // =====================
    // Inventario de módulos
    // =====================

    [Fact]
    public async Task MemoryInfo_LoadsModules()
    {
        var reader = new FakeWmiMemoryReader
        {
            Snapshot = new WmiMemorySnapshot
            {
                Modules = [CreateModule(), CreateModule(deviceLocator: "DIMM 1")]
            }
        };
        var service = CreateService(reader);

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(2, info.Modules.Count);
        Assert.True(info.HasModuleDetails);
    }

    [Fact]
    public async Task DeviceLocator_Preserved()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(deviceLocator: "DIMM 2")]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("DIMM 2", info.Modules[0].DeviceLocator);
    }

    [Fact]
    public async Task BankLabel_Preserved()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(bankLabel: "BANK 2")]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("BANK 2", info.Modules[0].BankLabel);
    }

    [Fact]
    public async Task Manufacturer_PreservedAndTrimmed()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(manufacturer: "  Kingston  ")]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("Kingston", info.Modules[0].Manufacturer);
    }

    [Fact]
    public async Task PartNumber_PreservedAndTrimmed()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(partNumber: "  KF3200C16D4/16  ")]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("KF3200C16D4/16", info.Modules[0].PartNumber);
    }

    [Fact]
    public async Task SerialNumber_PreservedAndTrimmed()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(serialNumber: "  2E5A1B3C  ")]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("2E5A1B3C", info.Modules[0].SerialNumber);
    }

    [Fact]
    public async Task CapacityBytes_PreservedExactly()
    {
        const ulong capacity = 17_179_869_184; // 16 GB
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(capacityBytes: capacity)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(capacity, info.Modules[0].CapacityBytes);
    }

    [Fact]
    public async Task CapacityGB_CalculatedCorrectly()
    {
        const ulong capacity = 17_179_869_184; // 16 GB
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(capacityBytes: capacity)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(16.0, info.Modules[0].CapacityGB);
    }

    [Fact]
    public async Task ConfiguredClockSpeed_SavedAsMHz()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(speedMHz: 3600)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(3600u, info.Modules[0].ConfiguredClockSpeedMHz);
    }

    [Fact]
    public async Task ConfiguredClockSpeedZero_ToNull()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(speedMHz: 0)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Null(info.Modules[0].ConfiguredClockSpeedMHz);
    }

    [Fact]
    public async Task SmbiosMemoryTypeCode_Preserved()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 26)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(26u, info.Modules[0].SMBIOSMemoryTypeCode);
    }

    [Fact]
    public async Task Ddr3_Recognized()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 24)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("DDR3", info.Modules[0].MemoryType);
    }

    [Fact]
    public async Task Ddr4_Recognized()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 26)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("DDR4", info.Modules[0].MemoryType);
    }

    [Fact]
    public async Task Lpddr4_Recognized()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 30)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("LPDDR4", info.Modules[0].MemoryType);
    }

    [Fact]
    public async Task Ddr5_Recognized()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 34)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("DDR5", info.Modules[0].MemoryType);
    }

    [Fact]
    public async Task Lpddr5_Recognized()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 35)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("LPDDR5", info.Modules[0].MemoryType);
    }

    [Fact]
    public async Task UnknownCode_Desconocida_RawPreserved()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(smbiosCode: 99)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("Desconocida", info.Modules[0].MemoryType);
        Assert.Equal(99u, info.Modules[0].SMBIOSMemoryTypeCode);
    }

    [Fact]
    public async Task DataWidth_Preserved()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(dataWidth: 64)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal((ushort?)64, info.Modules[0].DataWidthBits);
    }

    [Fact]
    public async Task TotalWidth_Preserved()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(totalWidth: 72)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal((ushort?)72, info.Modules[0].TotalWidthBits);
    }

    [Fact]
    public async Task AttributesValid_ToRank()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(attributes: 3)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(3u, info.Modules[0].Rank);
    }

    [Fact]
    public async Task AttributesZero_RankNull()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules = [CreateModule(attributes: 0)]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Null(info.Modules[0].Rank);
    }

    // =====================
    // Slots
    // =====================

    [Fact]
    public async Task SlotsUsed_CountsOnlyValidCapacity()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(capacityBytes: 8UL * 1024 * 1024 * 1024),
                CreateModule(capacityBytes: null),
                CreateModule(capacityBytes: 0)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(1, info.SlotsUsed);
        Assert.Equal(3, info.Modules.Count);
    }

    [Fact]
    public async Task SlotsTotal_FromMemoryDevices()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Arrays = [new WmiPhysicalMemoryArray { MemoryDevices = 4, Use = 3 }]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(4, info.SlotsTotal);
    }

    [Fact]
    public async Task MultipleSystemArrays_Summed()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Arrays =
            [
                new WmiPhysicalMemoryArray { MemoryDevices = 2, Use = 3 },
                new WmiPhysicalMemoryArray { MemoryDevices = 3, Use = 3 }
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(5, info.SlotsTotal);
    }

    [Fact]
    public async Task NonSystemArrays_NotSummed()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Arrays =
            [
                new WmiPhysicalMemoryArray { MemoryDevices = 8, Use = 2 },
                new WmiPhysicalMemoryArray { MemoryDevices = 2, Use = 3 }
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(2, info.SlotsTotal);
    }

    [Fact]
    public async Task NoValidArrays_SlotsTotalZero()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Arrays = [new WmiPhysicalMemoryArray { MemoryDevices = 8, Use = 2 }]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(0, info.SlotsTotal);
    }

    [Fact]
    public async Task SlotsUsedGreaterThanTotal_NotCorrected()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(capacityBytes: 8UL * 1024 * 1024 * 1024),
                CreateModule(capacityBytes: 8UL * 1024 * 1024 * 1024),
                CreateModule(capacityBytes: 8UL * 1024 * 1024 * 1024)
            ],
            Arrays = [new WmiPhysicalMemoryArray { MemoryDevices = 2, Use = 3 }]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(3, info.SlotsUsed);
        Assert.Equal(2, info.SlotsTotal);
    }

    // =====================
    // Resúmenes
    // =====================

    [Fact]
    public async Task SpeedSummary_AllEqual()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(speedMHz: 3200),
                CreateModule(deviceLocator: "DIMM 1", speedMHz: 3200)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(3200, info.SpeedMHz);
    }

    [Fact]
    public async Task SpeedSummary_Different_Zero()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(speedMHz: 3200),
                CreateModule(deviceLocator: "DIMM 1", speedMHz: 3600)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(0, info.SpeedMHz);
    }

    [Fact]
    public async Task SpeedSummary_NoSpeeds_Zero()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(speedMHz: null),
                CreateModule(deviceLocator: "DIMM 1", speedMHz: 0)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(0, info.SpeedMHz);
    }

    [Fact]
    public async Task TypeSummary_Uniform()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(smbiosCode: 26),
                CreateModule(deviceLocator: "DIMM 1", smbiosCode: 26)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("DDR4", info.Type);
    }

    [Fact]
    public async Task TypeSummary_Mixed_Mixta()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(smbiosCode: 26),
                CreateModule(deviceLocator: "DIMM 1", smbiosCode: 34)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("Mixta", info.Type);
    }

    [Fact]
    public async Task TypeSummary_NoInfo_Desconocida()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                CreateModule(smbiosCode: null),
                CreateModule(deviceLocator: "DIMM 1", smbiosCode: 99)
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal("Desconocida", info.Type);
    }

    // =====================
    // Tolerancia
    // =====================

    [Fact]
    public async Task PartialModule_NotDiscarded()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot
        {
            Modules =
            [
                new WmiPhysicalMemoryModule
                {
                    DeviceLocator = "DIMM 0",
                    BankLabel = "BANK 0",
                    CapacityBytes = 8UL * 1024 * 1024 * 1024
                    // Sin serial, sin velocidad, sin tipo
                }
            ]
        });

        var info = await service.GetMemoryInfoAsync();

        var module = Assert.Single(info.Modules);
        Assert.Equal("DIMM 0", module.DeviceLocator);
        Assert.Equal(8.0, module.CapacityGB);
        Assert.Equal(string.Empty, module.SerialNumber);
        Assert.Null(module.ConfiguredClockSpeedMHz);
        Assert.Equal("Desconocida", module.MemoryType);
    }

    [Fact]
    public async Task NoModules_NoFailure()
    {
        var service = CreateServiceWith(new WmiMemorySnapshot());

        var info = await service.GetMemoryInfoAsync();

        Assert.Empty(info.Modules);
        Assert.False(info.HasModuleDetails);
        Assert.Equal(0, info.SlotsUsed);
        Assert.Equal("Desconocida", info.Type);
    }

    [Fact]
    public async Task ReaderFailure_NoPropagation()
    {
        var service = CreateService(new FakeWmiMemoryReader { ThrowOnRead = true });

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(0, info.TotalGB);
        Assert.Equal(0, info.AvailableGB);
        Assert.Empty(info.Modules);
    }

    [Fact]
    public async Task GetMemoryInfoAsync_UsesInjectedReader()
    {
        var reader = new FakeWmiMemoryReader
        {
            Snapshot = new WmiMemorySnapshot
            {
                TotalPhysicalMemoryBytes = 17_179_869_184,
                FreePhysicalMemoryKilobytes = 8_388_608,
                Modules = [CreateModule()],
                Arrays = [new WmiPhysicalMemoryArray { MemoryDevices = 4, Use = 3 }]
            }
        };
        var service = CreateService(reader);

        var info = await service.GetMemoryInfoAsync();

        Assert.Equal(16.0, info.TotalGB);
        Assert.Equal(8.0, info.AvailableGB);
        Assert.Single(info.Modules);
        Assert.Equal(4, info.SlotsTotal);
    }

    // =====================
    // Compatibilidad y alcance
    // =====================

    [Fact]
    public async Task IHardwareService_StillCompatible()
    {
        IHardwareService service = CreateServiceWith(new WmiMemorySnapshot());

        var info = await service.GetMemoryInfoAsync();

        Assert.NotNull(info);
        Assert.NotNull(info.Modules);
    }

    [Fact]
    public void HardwareViewModel_NoMemoryModulesExposure()
    {
        var propertyNames = typeof(Cattech.Optimizer.Pro.UI.ViewModels.HardwareViewModel)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "Modules" or "MemoryModules" or "RamModules");
    }

    [Fact]
    public void MemoryModuleInfo_NoSpdTimings()
    {
        var propertyNames = typeof(MemoryModuleInfo)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "CASLatency" or "TRCD" or "TRP" or "TRAS" or
            "TimingProfile" or "XmpProfile" or "ExpoProfile");
    }
}
