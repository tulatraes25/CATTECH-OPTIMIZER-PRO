using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Hardware;
using Cattech.Optimizer.Pro.Infrastructure.Hardware.SensorProvider;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class LibreHardwareSensorServiceTests
{
    // =====================
    // Fakes de la abstracción interna (nunca tocan hardware real)
    // =====================

    private sealed class FakeSensor : ISensorNode
    {
        public string Name { get; }
        public string Identifier { get; }
        public InternalSensorType SensorType { get; }
        public float? Value { get; }
        public float? Min { get; }
        public float? Max { get; }

        public FakeSensor(string name, string identifier, InternalSensorType type,
            float? value = null, float? min = null, float? max = null)
        {
            Name = name;
            Identifier = identifier;
            SensorType = type;
            Value = value;
            Min = min;
            Max = max;
        }
    }

    private sealed class FakeHardware : IHardwareNode
    {
        public string Name { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public InternalHardwareType HardwareType { get; set; }
        public IReadOnlyList<IHardwareNode> SubHardware { get; set; } = new List<IHardwareNode>();
        public bool ThrowOnSensorsRead { get; set; }

        private IReadOnlyList<ISensorNode> _sensors = new List<ISensorNode>();

        public IReadOnlyList<ISensorNode> Sensors
        {
            get
            {
                if (ThrowOnSensorsRead)
                {
                    throw new InvalidOperationException("Simulated sensor read failure");
                }

                return _sensors;
            }
            set => _sensors = value;
        }
    }

    private sealed class FakeSession : IHardwareMonitorSession
    {
        public bool ThrowOnHardwareRead { get; set; }
        public bool Disposed { get; private set; }

        private IReadOnlyList<IHardwareNode> _hardware = new List<IHardwareNode>();

        public IReadOnlyList<IHardwareNode> Hardware
        {
            get
            {
                if (ThrowOnHardwareRead)
                {
                    throw new InvalidOperationException("Simulated hardware enumeration failure");
                }

                return _hardware;
            }
            set => _hardware = value;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeFactory : IHardwareMonitorFactory
    {
        public FakeSession? Session { get; set; }
        public bool ThrowOnCreate { get; set; }
        public int CreateCount { get; private set; }

        public IHardwareMonitorSession Create()
        {
            CreateCount++;
            if (ThrowOnCreate)
            {
                throw new InvalidOperationException("Simulated open failure");
            }

            return Session!;
        }
    }

    private static FakeSensor Temp(string name, string identifier, float? value,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Temperature, value, min, max);

    private static LibreHardwareSensorService CreateService(IHardwareMonitorFactory factory,
        bool isElevated = true) => new(factory, isElevated);

    private static async Task<HardwareTemperatureSnapshot> Capture(IHardwareMonitorFactory factory,
        bool isElevated = true)
    {
        var service = CreateService(factory, isElevated);
        return await service.GetTemperatureSnapshotAsync();
    }

    // =====================
    // Captura por tipo de hardware
    // =====================

    [Fact]
    public async Task CpuTemperatureSensor_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Core Max", "/intelcpu/0/temperature/1", 65.5f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.True(snapshot.IsAvailable);
        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal("CPU", sensor.HardwareType);
        Assert.Equal("Intel Core i7-13700K", sensor.HardwareName);
        Assert.Equal(65.5, sensor.ValueCelsius);
    }

    [Fact]
    public async Task GpuTemperatureSensor_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "NVIDIA GeForce RTX 4070",
                        Identifier = "/gpu-nvidia/0",
                        HardwareType = InternalHardwareType.Gpu,
                        Sensors = [Temp("GPU Core", "/gpu-nvidia/0/temperature/0", 70.0f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal("GPU", sensor.HardwareType);
        Assert.Equal(70.0, sensor.ValueCelsius);
    }

    [Fact]
    public async Task StorageTemperatureSensor_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Samsung SSD 980 PRO",
                        Identifier = "/nvme/0",
                        HardwareType = InternalHardwareType.Storage,
                        Sensors = [Temp("Temperature", "/nvme/0/temperature/0", 42.0f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal("Almacenamiento", sensor.HardwareType);
        Assert.Equal(42.0, sensor.ValueCelsius);
    }

    [Fact]
    public async Task MotherboardSubHardwareSensor_CapturedRecursively()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "ASUS ROG STRIX Z790",
                        Identifier = "/mb/0",
                        HardwareType = InternalHardwareType.Motherboard,
                        Sensors = [Temp("Motherboard", "/mb/0/temperature/0", 38.0f)],
                        SubHardware =
                        [
                            new FakeHardware
                            {
                                Name = "Nuvoton NCT6798D",
                                Identifier = "/mb/0/superio/0",
                                HardwareType = InternalHardwareType.Motherboard,
                                Sensors = [Temp("CPU Package", "/mb/0/superio/0/temperature/1", 55.0f)]
                            }
                        ]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.Equal(2, snapshot.Sensors.Count);
        Assert.Contains(snapshot.Sensors, s => s.SensorName == "CPU Package" && s.HardwareName == "Nuvoton NCT6798D");
        Assert.Contains(snapshot.Sensors, s => s.SensorName == "Motherboard");
    }

    // =====================
    // Filtrado y normalización
    // =====================

    [Fact]
    public async Task NonTemperatureSensors_Ignored()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors =
                        [
                            Temp("Core Max", "/intelcpu/0/temperature/1", 60.0f),
                            new FakeSensor("CPU Load", "/intelcpu/0/load/0", InternalSensorType.Other, 35.0f),
                            new FakeSensor("Core Voltage", "/intelcpu/0/voltage/0", InternalSensorType.Other, 1.2f)
                        ]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal("Core Max", sensor.SensorName);
    }

    [Fact]
    public async Task NullValue_StaysNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/intelcpu/0/temperature/0", null)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Null(sensor.ValueCelsius);
        Assert.Equal(0, snapshot.ValidSensorCount);
        Assert.True(snapshot.HasSensors);
    }

    [Fact]
    public async Task NaN_NormalizedToNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/intelcpu/0/temperature/0", float.NaN)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Null(sensor.ValueCelsius);
    }

    [Fact]
    public async Task Infinity_NormalizedToNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/intelcpu/0/temperature/0", float.PositiveInfinity)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Null(sensor.ValueCelsius);
    }

    [Fact]
    public async Task MinMax_Preserved()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "NVIDIA GeForce RTX 4070",
                        Identifier = "/gpu-nvidia/0",
                        HardwareType = InternalHardwareType.Gpu,
                        Sensors = [Temp("GPU Core", "/gpu-nvidia/0/temperature/0", 71.0f, 30.0f, 82.0f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal(71.0, sensor.ValueCelsius);
        Assert.Equal(30.0, sensor.MinCelsius);
        Assert.Equal(82.0, sensor.MaxCelsius);
    }

    // =====================
    // Identificación y deduplicación
    // =====================

    [Fact]
    public async Task SameName_DifferentIdentifier_NotDeduplicated()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors =
                        [
                            Temp("Core Max", "/intelcpu/0/temperature/1", 60.0f),
                            Temp("Core Max", "/intelcpu/0/temperature/2", 61.0f)
                        ]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.Equal(2, snapshot.Sensors.Count);
    }

    [Fact]
    public async Task SameIdentifier_DuplicatedOnce()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors =
                        [
                            Temp("Package", "/intelcpu/0/temperature/0", 60.0f),
                            Temp("Package", "/intelcpu/0/temperature/0", 61.0f)
                        ]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal(60.0, sensor.ValueCelsius);
    }

    // =====================
    // Disponibilidad y errores
    // =====================

    [Fact]
    public async Task HardwareWithoutSensors_EmptyList_NoException()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = []
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.True(snapshot.IsAvailable);
        Assert.Empty(snapshot.Sensors);
        Assert.False(snapshot.HasSensors);
    }

    [Fact]
    public async Task NoTemperatures_GeneratesControlledWarning()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = []
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.Contains(snapshot.Warnings, w => w.Contains("No se detectaron sensores de temperatura", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(snapshot.Errors);
    }

    [Fact]
    public async Task OpenFailure_IsAvailableFalse()
    {
        var factory = new FakeFactory { ThrowOnCreate = true };

        var snapshot = await Capture(factory);

        Assert.False(snapshot.IsAvailable);
        Assert.NotEmpty(snapshot.Errors);
        Assert.Empty(snapshot.Sensors);
    }

    [Fact]
    public async Task HardwareReadError_DoesNotLoseOtherHardware()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Broken Sensor Hub",
                        Identifier = "/broken/0",
                        HardwareType = InternalHardwareType.Motherboard,
                        ThrowOnSensorsRead = true
                    },
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/intelcpu/0/temperature/0", 58.0f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.True(snapshot.IsAvailable);
        Assert.NotEmpty(snapshot.Errors);
        var sensor = Assert.Single(snapshot.Sensors);
        Assert.Equal("Intel Core i7-13700K", sensor.HardwareName);
    }

    [Fact]
    public async Task HardwareEnumerationFailure_IsAvailableFalse_SessionDisposed()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession { ThrowOnHardwareRead = true }
        };

        var snapshot = await Capture(factory);

        Assert.False(snapshot.IsAvailable);
        Assert.NotEmpty(snapshot.Errors);
        Assert.True(factory.Session!.Disposed);
    }

    // =====================
    // Permisos
    // =====================

    [Fact]
    public async Task IsElevated_Reflected()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession { Hardware = [] }
        };

        var snapshot = await Capture(factory, isElevated: true);

        Assert.True(snapshot.IsElevated);
        Assert.DoesNotContain(snapshot.Warnings, w => w.Contains("administrador", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NotElevated_GeneratesWarning_NotFatal()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/intelcpu/0/temperature/0", 60.0f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory, isElevated: false);

        Assert.True(snapshot.IsAvailable);
        Assert.Contains(snapshot.Warnings, w => w.Contains("permisos de administrador", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(snapshot.Errors);
    }

    // =====================
    // Cancelación y ciclo de vida
    // =====================

    [Fact]
    public async Task Cancellation_Respected()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession { Hardware = [] }
        };
        var service = CreateService(factory);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetTemperatureSnapshotAsync(cts.Token));
    }

    [Fact]
    public async Task Session_Disposed_AfterSuccessfulRead()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/intelcpu/0/temperature/0", 60.0f)]
                    }
                ]
            }
        };

        await Capture(factory);

        Assert.True(factory.Session!.Disposed);
    }

    // =====================
    // Sin interpretación de salud
    // =====================

    [Fact]
    public void Snapshot_HasNoHealthStateProperties()
    {
        var propertyNames = typeof(HardwareTemperatureSnapshot)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "IsHot" or "IsCritical" or "HealthStatus" or "Recommendation" or "Severity");
    }

    [Fact]
    public async Task Service_UsesInjectedFactory_NoRealHardwareAccess()
    {
        // Si el servicio usara la fábrica real (LibreHardwareMonitorFactory),
        // este snapshot con hardware simulado no sería posible.
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Simulated CPU",
                        Identifier = "/sim/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors = [Temp("Package", "/sim/0/temperature/0", 45.0f)]
                    }
                ]
            }
        };

        var snapshot = await Capture(factory);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(snapshot.IsAvailable);
        Assert.Equal("Simulated CPU", Assert.Single(snapshot.Sensors).HardwareName);
    }
}
