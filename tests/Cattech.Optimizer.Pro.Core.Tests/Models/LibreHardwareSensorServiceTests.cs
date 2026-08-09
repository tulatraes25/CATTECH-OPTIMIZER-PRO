using System.Linq;
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
        public float? Value { get; set; }
        public float? Min { get; set; }
        public float? Max { get; set; }

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
        public bool ThrowOnRefresh { get; set; }
        public bool ThrowOnFirstRefresh { get; set; }
        public Action? OnRefresh { get; set; }
        public int RefreshCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int MaxConcurrentRefreshes { get; private set; }
        public bool Disposed => DisposeCount > 0;

        private int _inFlight;
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

        public void Refresh()
        {
            RefreshCount++;
            _inFlight++;
            try
            {
                if (_inFlight > MaxConcurrentRefreshes)
                {
                    MaxConcurrentRefreshes = _inFlight;
                }

                if (ThrowOnRefresh || (ThrowOnFirstRefresh && RefreshCount == 1))
                {
                    throw new InvalidOperationException("Simulated refresh failure");
                }

                OnRefresh?.Invoke();
            }
            finally
            {
                _inFlight--;
            }
        }

        public void Dispose() => DisposeCount++;
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

    private sealed class FakeDelay : IHardwareMonitorDelay
    {
        public List<TimeSpan> Delays { get; } = new();
        public bool ThrowOnDelay { get; set; }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            if (ThrowOnDelay)
            {
                throw new InvalidOperationException("Simulated delay failure");
            }

            // Completa inmediatamente: los tests no esperan segundos reales.
            return Task.CompletedTask;
        }
    }

    private static FakeSensor Temp(string name, string identifier, float? value,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Temperature, value, min, max);

    private static LibreHardwareSensorService CreateService(IHardwareMonitorFactory factory,
        bool isElevated = true) => new(factory, isElevated);

    private static LibreHardwareSensorService CreateService(IHardwareMonitorFactory factory,
        IHardwareMonitorDelay delay, bool isElevated = true) => new(factory, delay, isElevated);

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

    // =====================
    // Tests Fase B.1.2 - Sesión reutilizable y muestreo repetido
    // =====================

    private static readonly TimeSpan WatchInterval = TimeSpan.FromMilliseconds(250);

    private static FakeSession CreateCpuSession(out FakeSensor packageSensor)
    {
        packageSensor = new FakeSensor("Package", "/intelcpu/0/temperature/0", InternalSensorType.Temperature, 45.0f);
        return new FakeSession
        {
            Hardware =
            [
                new FakeHardware
                {
                    Name = "Intel Core i7-13700K",
                    Identifier = "/intelcpu/0",
                    HardwareType = InternalHardwareType.Cpu,
                    Sensors = [packageSensor]
                }
            ]
        };
    }

    private static async Task<List<HardwareTemperatureSnapshot>> TakeWatchSamplesAsync(
        LibreHardwareSensorService service, int count)
    {
        var samples = new List<HardwareTemperatureSnapshot>();
        await using var enumerator = service.WatchTemperatureSnapshotsAsync(WatchInterval).GetAsyncEnumerator();
        while (samples.Count < count && await enumerator.MoveNextAsync())
        {
            samples.Add(enumerator.Current);
        }

        return samples;
    }

    [Fact]
    public async Task SingleSnapshot_CallsCreateOnce()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetTemperatureSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task SingleSnapshot_CallsRefreshOnce()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetTemperatureSnapshotAsync();

        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task SingleSnapshot_DisposesSession()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetTemperatureSnapshotAsync();

        Assert.True(factory.Session!.Disposed);
        Assert.Equal(1, factory.Session.DisposeCount);
    }

    [Fact]
    public async Task WatchThreeSamples_SingleSession()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task WatchThreeSamples_ThreeRefreshes()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await TakeWatchSamplesAsync(service, 3);

        Assert.Equal(3, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task FirstSample_BeforeFirstDelay()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var delay = new FakeDelay();
        var service = CreateService(factory, delay);

        await using var enumerator = service.WatchTemperatureSnapshotsAsync(WatchInterval).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Empty(delay.Delays);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Single(delay.Delays);
    }

    [Fact]
    public async Task Delay_ReceivesExactInterval()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var delay = new FakeDelay();
        var service = CreateService(factory, delay);

        await TakeWatchSamplesAsync(service, 3);

        Assert.Equal(2, delay.Delays.Count);
        Assert.All(delay.Delays, d => Assert.Equal(WatchInterval, d));
    }

    [Fact]
    public async Task EachSample_IsDifferentObject()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 3);

        Assert.NotSame(samples[0], samples[1]);
        Assert.NotSame(samples[1], samples[2]);
    }

    [Fact]
    public async Task EachSample_IndependentSensorsList()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 3);

        Assert.NotSame(samples[0].Sensors, samples[1].Sensors);
        Assert.NotSame(samples[1].Sensors, samples[2].Sensors);
    }

    [Fact]
    public async Task SameIdentifier_DedupedPerSnapshot_NotPerStream()
    {
        var package = new FakeSensor("Package", "/intelcpu/0/temperature/0", InternalSensorType.Temperature, 45.0f);
        var duplicate = new FakeSensor("Package", "/intelcpu/0/temperature/0", InternalSensorType.Temperature, 46.0f);
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
                        Sensors = [package, duplicate]
                    }
                ]
            }
        };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 2);

        Assert.All(samples, s => Assert.Single(s.Sensors));
    }

    [Fact]
    public async Task ValueChanges_AppearAcrossSamples()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out var sensor) };
        var current = 45.0f;
        factory.Session!.OnRefresh = () => { current += 6; sensor.Value = current; };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 2);

        Assert.Equal(51.0, samples[0].Sensors[0].ValueCelsius);
        Assert.Equal(57.0, samples[1].Sensors[0].ValueCelsius);
    }

    [Fact]
    public async Task MinMaxChanges_AreReflected()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out var sensor) };
        var max = 80.0f;
        factory.Session!.OnRefresh = () => { max += 5; sensor.Max = max; };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 2);

        Assert.Equal(85.0, samples[0].Sensors[0].MaxCelsius);
        Assert.Equal(90.0, samples[1].Sensors[0].MaxCelsius);
    }

    [Fact]
    public async Task ConsumerBreak_DisposesSession()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await foreach (var _ in service.WatchTemperatureSnapshotsAsync(WatchInterval))
        {
            break;
        }

        Assert.True(factory.Session!.Disposed);
    }

    [Fact]
    public async Task Cancellation_DisposesSession()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());
        using var cts = new CancellationTokenSource();

        var enumerator = service.WatchTemperatureSnapshotsAsync(WatchInterval, cts.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync());

        Assert.True(factory.Session!.Disposed);
    }

    [Fact]
    public async Task Cancellation_PreventsFurtherRefreshes()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());
        using var cts = new CancellationTokenSource();

        var enumerator = service.WatchTemperatureSnapshotsAsync(WatchInterval, cts.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(1, factory.Session!.RefreshCount);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync());

        Assert.Equal(1, factory.Session.RefreshCount);
    }

    [Fact]
    public async Task ZeroInterval_ThrowsArgumentOutOfRange()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in service.WatchTemperatureSnapshotsAsync(TimeSpan.Zero))
            {
            }
        });
    }

    [Fact]
    public async Task NegativeInterval_ThrowsArgumentOutOfRange()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await foreach (var _ in service.WatchTemperatureSnapshotsAsync(TimeSpan.FromMilliseconds(-100)))
            {
            }
        });
    }

    [Fact]
    public async Task CreateFailure_SingleUnavailableSample_ThenEnds()
    {
        var factory = new FakeFactory { ThrowOnCreate = true };
        var service = CreateService(factory, new FakeDelay());

        var samples = new List<HardwareTemperatureSnapshot>();
        await foreach (var s in service.WatchTemperatureSnapshotsAsync(WatchInterval))
        {
            samples.Add(s);
        }

        var unavailable = Assert.Single(samples);
        Assert.False(unavailable.IsAvailable);
        Assert.NotEmpty(unavailable.Errors);
        Assert.Empty(unavailable.Sensors);
    }

    [Fact]
    public async Task RefreshFailure_UnavailableSnapshot_NoStaleSensors()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        factory.Session!.ThrowOnRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 1);

        Assert.False(samples[0].IsAvailable);
        Assert.Empty(samples[0].Sensors);
        Assert.NotEmpty(samples[0].Errors);
    }

    [Fact]
    public async Task RefreshFailure_ThenSuccess_Recovers()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        factory.Session!.ThrowOnFirstRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 2);

        Assert.False(samples[0].IsAvailable);
        Assert.True(samples[1].IsAvailable);
        Assert.Single(samples[1].Sensors);
        Assert.Equal(45.0, samples[1].Sensors[0].ValueCelsius);
    }

    [Fact]
    public async Task Dispose_ExactlyOnce()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await TakeWatchSamplesAsync(service, 3);

        Assert.Equal(1, factory.Session!.DisposeCount);
    }

    [Fact]
    public async Task NoConcurrentRefreshes()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        await TakeWatchSamplesAsync(service, 3);

        Assert.Equal(1, factory.Session!.MaxConcurrentRefreshes);
    }

    [Fact]
    public async Task Watch_UsesInjectedDelay_NoRealTimers()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var delay = new FakeDelay();
        var service = CreateService(factory, delay);

        await TakeWatchSamplesAsync(service, 3);

        // El delay inyectado recibió las esperas: el servicio no usa Task.Delay interno
        Assert.Equal(2, delay.Delays.Count);
    }

    [Fact]
    public async Task Watch_WithFakes_NoRealHardwareAccess()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 1);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(samples[0].IsAvailable);
        Assert.Equal("Intel Core i7-13700K", samples[0].Sensors[0].HardwareName);
    }

    [Fact]
    public async Task Watch_NotElevated_ProducesWarning_NoErrors()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };
        var service = CreateService(factory, new FakeDelay(), isElevated: false);

        var samples = await TakeWatchSamplesAsync(service, 2);

        Assert.All(samples, s =>
        {
            Assert.True(s.IsAvailable);
            Assert.Contains(s.Warnings, w => w.Contains("permisos de administrador", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(s.Errors);
        });
    }

    // =====================
    // Tests Fase B.2.1 - Métricas dinámicas CPU/GPU (Load + Clock)
    // =====================

    private static FakeSensor Load(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Load, value, min, max);

    private static FakeSensor Clock(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Clock, value, min, max);

    private static async Task<HardwareLiveSnapshot> CaptureLive(IHardwareMonitorFactory factory,
        bool isElevated = true)
    {
        var service = CreateService(factory, isElevated);
        return await service.GetLiveSnapshotAsync();
    }

    private static FakeSession CreateCpuGpuSession(out FakeSensor cpuLoad, out FakeSensor cpuClock,
        out FakeSensor gpuLoad, out FakeSensor gpuClock)
    {
        cpuLoad = Load("CPU Total", "/intelcpu/0/load/0", 35.5f);
        cpuClock = Clock("CPU Clock", "/intelcpu/0/clock/0", 3700f);
        gpuLoad = Load("GPU Core", "/gpu-nvidia/0/load/0", 42.0f);
        gpuClock = Clock("GPU Clock", "/gpu-nvidia/0/clock/0", 1905f);

        return new FakeSession
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
                        cpuLoad,
                        cpuClock
                    ]
                },
                new FakeHardware
                {
                    Name = "NVIDIA GeForce RTX 4070",
                    Identifier = "/gpu-nvidia/0",
                    HardwareType = InternalHardwareType.Gpu,
                    Sensors =
                    [
                        Temp("GPU Core", "/gpu-nvidia/0/temperature/0", 55.0f),
                        gpuLoad,
                        gpuClock
                    ]
                }
            ]
        };
    }

    private static async Task<List<HardwareLiveSnapshot>> TakeLiveWatchSamplesAsync(
        LibreHardwareSensorService service, int count)
    {
        var samples = new List<HardwareLiveSnapshot>();
        await using var enumerator = service.WatchLiveSnapshotsAsync(WatchInterval).GetAsyncEnumerator();
        while (samples.Count < count && await enumerator.MoveNextAsync())
        {
            samples.Add(enumerator.Current);
        }

        return samples;
    }

    [Fact]
    public async Task CpuLoad_Captured()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        var sensor = live.PerformanceSensors.Single(s =>
            s.HardwareName == "Intel Core i7-13700K" && s.SensorName == "CPU Total");
        Assert.Equal("CPU", sensor.HardwareType);
        Assert.Equal(35.5, sensor.Value);
        Assert.True(live.HasPerformanceSensors);
    }

    [Fact]
    public async Task CpuClock_Captured()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        var sensor = live.PerformanceSensors.Single(s => s.SensorName == "CPU Clock");
        Assert.Equal(3700.0, sensor.Value);
    }

    [Fact]
    public async Task GpuLoad_Captured()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        var sensor = live.PerformanceSensors.Single(s =>
            s.HardwareName == "NVIDIA GeForce RTX 4070" && s.SensorName == "GPU Core");
        Assert.Equal("GPU", sensor.HardwareType);
        Assert.Equal(42.0, sensor.Value);
    }

    [Fact]
    public async Task GpuClock_Captured()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        var sensor = live.PerformanceSensors.Single(s => s.SensorName == "GPU Clock");
        Assert.Equal(1905.0, sensor.Value);
    }

    [Fact]
    public async Task MemoryLoad_IgnoredInPerformance()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "G.Skill DDR5",
                        Identifier = "/ram/0",
                        HardwareType = InternalHardwareType.Memory,
                        Sensors = [Load("Memory Load", "/ram/0/load/0", 30.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.PerformanceSensors);
    }

    [Fact]
    public async Task MotherboardClock_Ignored()
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
                        Sensors = [Clock("System Clock", "/mb/0/clock/0", 100.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.PerformanceSensors);
    }

    [Fact]
    public async Task Temperature_StillCaptured()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.TemperatureSensors.Count);
        Assert.True(live.HasTemperatureSensors);
        Assert.Equal(2, live.ValidTemperatureSensorCount);
    }

    [Fact]
    public async Task Temperature_NotInPerformanceSensors()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.PerformanceSensors, s => s.MetricType == HardwarePerformanceMetricType.Load && s.SensorName == "Package");
        Assert.DoesNotContain(live.PerformanceSensors, s => s.SensorName == "GPU Core" && s.MetricType != HardwarePerformanceMetricType.Load);
    }

    [Fact]
    public async Task Load_NotInTemperatureSensors()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.TemperatureSensors, s => s.SensorName == "CPU Total");
    }

    [Fact]
    public async Task Clock_NotInTemperatureSensors()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.TemperatureSensors, s => s.SensorName == "CPU Clock");
    }

    [Fact]
    public async Task MetricType_Load_Correct()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.PerformanceSensors.Where(s => s.SensorName.Contains("Load") || s.SensorName == "CPU Total"),
            s => Assert.Equal(HardwarePerformanceMetricType.Load, s.MetricType));
    }

    [Fact]
    public async Task MetricType_Clock_Correct()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.PerformanceSensors.Where(s => s.SensorName.Contains("Clock")),
            s => Assert.Equal(HardwarePerformanceMetricType.Clock, s.MetricType));
    }

    [Fact]
    public async Task Load_UsesPercentUnit()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.PerformanceSensors.Where(s => s.MetricType == HardwarePerformanceMetricType.Load),
            s => Assert.Equal("%", s.Unit));
    }

    [Fact]
    public async Task Clock_UsesMhzUnit()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.PerformanceSensors.Where(s => s.MetricType == HardwarePerformanceMetricType.Clock),
            s => Assert.Equal("MHz", s.Unit));
    }

    [Fact]
    public async Task PerformanceValueNull_StaysNull()
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
                        Sensors = [Load("CPU Total", "/intelcpu/0/load/0", null)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.PerformanceSensors);
        Assert.Null(sensor.Value);
        Assert.Equal(0, live.ValidPerformanceSensorCount);
    }

    [Fact]
    public async Task PerformanceNaN_NormalizedToNull()
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
                        Sensors = [Load("CPU Total", "/intelcpu/0/load/0", float.NaN)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.PerformanceSensors).Value);
    }

    [Fact]
    public async Task PerformanceInfinity_NormalizedToNull()
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
                        Sensors = [Clock("CPU Clock", "/intelcpu/0/clock/0", float.NegativeInfinity)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.PerformanceSensors).Value);
    }

    [Fact]
    public async Task PerformanceMinMax_Preserved()
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
                        Sensors = [Load("CPU Total", "/intelcpu/0/load/0", 50.0f, 5.0f, 95.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.PerformanceSensors);
        Assert.Equal(50.0, sensor.Value);
        Assert.Equal(5.0, sensor.Min);
        Assert.Equal(95.0, sensor.Max);
    }

    [Fact]
    public async Task SameName_DifferentIdentifier_PerformancePreserved()
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
                            Load("CPU Total", "/intelcpu/0/load/0", 40.0f),
                            Load("CPU Total", "/intelcpu/0/load/1", 55.0f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.PerformanceSensors.Count);
    }

    [Fact]
    public async Task DuplicateIdentifier_PerformanceDeduped()
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
                            Load("CPU Total", "/intelcpu/0/load/0", 40.0f),
                            Load("CPU Total", "/intelcpu/0/load/0", 55.0f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.PerformanceSensors);
        Assert.Equal(40.0, sensor.Value);
    }

    // =====================
    // B.2.1 - Un solo Refresh por captura
    // =====================

    [Fact]
    public async Task GetLiveSnapshot_CreateOnce()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task GetLiveSnapshot_RefreshOnce()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task GetLiveSnapshot_DisposeOnce()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.DisposeCount);
    }

    [Fact]
    public async Task CombinedCapture_SingleRefresh()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var live = await service.GetLiveSnapshotAsync();

        // Temperaturas + Load + Clock en el MISMO refresh: Create=1, Refresh=1, Dispose=1
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
        Assert.Equal(1, factory.Session.DisposeCount);

        Assert.Equal(2, live.TemperatureSensors.Count);
        Assert.Equal(4, live.PerformanceSensors.Count);
        Assert.Contains(live.PerformanceSensors, s => s.SensorName == "CPU Total" && s.MetricType == HardwarePerformanceMetricType.Load);
        Assert.Contains(live.PerformanceSensors, s => s.SensorName == "CPU Clock" && s.MetricType == HardwarePerformanceMetricType.Clock);
        Assert.Contains(live.PerformanceSensors, s => s.SensorName == "GPU Core" && s.MetricType == HardwarePerformanceMetricType.Load);
        Assert.Contains(live.PerformanceSensors, s => s.SensorName == "GPU Clock" && s.MetricType == HardwarePerformanceMetricType.Clock);
    }

    // =====================
    // B.2.1 - Watch live
    // =====================

    [Fact]
    public async Task WatchLive_ThreeSamples_SingleSession()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task WatchLive_ThreeSamples_ThreeRefreshes()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await TakeLiveWatchSamplesAsync(service, 3);

        Assert.Equal(3, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task LoadValues_ChangeAcrossSamples()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out var cpuLoad, out _, out _, out _) };
        var current = 35.5f;
        factory.Session!.OnRefresh = () => { current += 10; cpuLoad.Value = current; };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.Equal(45.5, samples[0].PerformanceSensors.Single(s => s.SensorName == "CPU Total").Value);
        Assert.Equal(55.5, samples[1].PerformanceSensors.Single(s => s.SensorName == "CPU Total").Value);
    }

    [Fact]
    public async Task ClockValues_ChangeAcrossSamples()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out var cpuClock, out _, out _) };
        var current = 3700f;
        factory.Session!.OnRefresh = () => { current += 100; cpuClock.Value = current; };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.Equal(3800.0, samples[0].PerformanceSensors.Single(s => s.SensorName == "CPU Clock").Value);
        Assert.Equal(3900.0, samples[1].PerformanceSensors.Single(s => s.SensorName == "CPU Clock").Value);
    }

    [Fact]
    public async Task LiveSamples_IndependentObjects()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.NotSame(samples[0], samples[1]);
        Assert.NotSame(samples[0].TemperatureSensors, samples[1].TemperatureSensors);
        Assert.NotSame(samples[0].PerformanceSensors, samples[1].PerformanceSensors);
    }

    [Fact]
    public async Task LiveRefreshFailure_EmptiesBothLists()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        factory.Session!.ThrowOnRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 1);

        Assert.False(samples[0].IsAvailable);
        Assert.Empty(samples[0].TemperatureSensors);
        Assert.Empty(samples[0].PerformanceSensors);
        Assert.NotEmpty(samples[0].Errors);
    }

    [Fact]
    public async Task LiveRefreshFailure_ThenSuccess_RecoversBoth()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        factory.Session!.ThrowOnFirstRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.False(samples[0].IsAvailable);
        Assert.True(samples[1].IsAvailable);
        Assert.Equal(2, samples[1].TemperatureSensors.Count);
        Assert.Equal(4, samples[1].PerformanceSensors.Count);
    }

    [Fact]
    public async Task LivePartialNodeError_PreservesValidMetrics()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Broken GPU",
                        Identifier = "/gpu-nvidia/0",
                        HardwareType = InternalHardwareType.Gpu,
                        ThrowOnSensorsRead = true
                    },
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors =
                        [
                            Temp("Package", "/intelcpu/0/temperature/0", 60.0f),
                            Load("CPU Total", "/intelcpu/0/load/0", 33.0f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
        Assert.NotEmpty(live.Errors);
        Assert.Single(live.TemperatureSensors);
        Assert.Single(live.PerformanceSensors);
        Assert.Equal("Intel Core i7-13700K", live.PerformanceSensors[0].HardwareName);
    }

    [Fact]
    public async Task LiveCancellation_DisposesSession()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());
        using var cts = new CancellationTokenSource();

        var enumerator = service.WatchLiveSnapshotsAsync(WatchInterval, cts.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync());

        Assert.True(factory.Session!.Disposed);
    }

    [Fact]
    public async Task LiveBreak_DisposesSession()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await foreach (var _ in service.WatchLiveSnapshotsAsync(WatchInterval))
        {
            break;
        }

        Assert.True(factory.Session!.Disposed);
    }

    // =====================
    // B.2.1 - Compatibilidad con la API antigua
    // =====================

    [Fact]
    public async Task OldGetTemperatureSnapshot_StillWorks()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var snapshot = await service.GetTemperatureSnapshotAsync();

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(2, snapshot.Sensors.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task OldWatchTemperatureSnapshots_StillWorks()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeWatchSamplesAsync(service, 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(3, factory.Session!.RefreshCount);
        Assert.All(samples, s => Assert.Equal(2, s.Sensors.Count));
    }

    [Fact]
    public async Task TemperatureProjection_PreservesWarningsAndErrors()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Broken GPU",
                        Identifier = "/gpu-nvidia/0",
                        HardwareType = InternalHardwareType.Gpu,
                        ThrowOnSensorsRead = true
                    },
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

        var service = CreateService(factory, new FakeDelay(), isElevated: false);
        var snapshot = await service.GetTemperatureSnapshotAsync();

        Assert.True(snapshot.IsAvailable);
        Assert.Contains(snapshot.Warnings, w => w.Contains("permisos de administrador", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshot.Errors, e => e.Contains("Broken GPU", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LiveSnapshot_NoPerformanceHealthProperties()
    {
        var propertyNames = typeof(HardwareLiveSnapshot)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "IsOverloaded" or "IsSlow" or "HealthStatus" or
            "Severity" or "Recommendation" or "IsHot" or "IsCritical");
    }

    [Fact]
    public async Task Live_UsesInjectedFactory_NoRealHardware()
    {
        var factory = new FakeFactory { Session = CreateCpuGpuSession(out _, out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(live.IsAvailable);
        Assert.Equal(2, live.TemperatureSensors.Count);
        Assert.Equal(4, live.PerformanceSensors.Count);
    }

    // =====================
    // Tests Fase B.2.2 - Memoria GPU (SensorType.SmallData)
    // =====================

    private static FakeSensor SmallData(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.SmallData, value, min, max);

    private static FakeSession CreateGpuMemorySession(out FakeSensor gpuMemoryUsed, out FakeSensor gpuMemoryTotal)
    {
        gpuMemoryUsed = SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 6144f);
        gpuMemoryTotal = SmallData("GPU Memory Total", "/gpu-nvidia/0/small-data/1", 12288f);

        return new FakeSession
        {
            Hardware =
            [
                new FakeHardware
                {
                    Name = "NVIDIA GeForce RTX 4070",
                    Identifier = "/gpu-nvidia/0",
                    HardwareType = InternalHardwareType.Gpu,
                    Sensors =
                    [
                        Temp("GPU Core", "/gpu-nvidia/0/temperature/0", 55.0f),
                        Load("GPU Core", "/gpu-nvidia/0/load/0", 42.0f),
                        Clock("GPU Clock", "/gpu-nvidia/0/clock/0", 1905f),
                        gpuMemoryUsed,
                        gpuMemoryTotal
                    ]
                }
            ]
        };
    }

    [Fact]
    public async Task GpuSmallData_Captured()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.GpuMemorySensors.Count);
        Assert.True(live.HasGpuMemorySensors);
        Assert.Equal(2, live.ValidGpuMemorySensorCount);
        Assert.Contains(live.GpuMemorySensors, s => s.SensorName == "GPU Memory Used");
        Assert.Contains(live.GpuMemorySensors, s => s.SensorName == "GPU Memory Total");
        Assert.All(live.GpuMemorySensors, s => Assert.Equal("NVIDIA GeForce RTX 4070", s.HardwareName));
    }

    [Fact]
    public async Task SmallData_UnitMB()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.GpuMemorySensors, s => Assert.Equal("MB", s.Unit));
    }

    [Fact]
    public async Task GpuMemoryValue_PreservedNoConversion()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        var used = live.GpuMemorySensors.Single(s => s.SensorName == "GPU Memory Used");
        Assert.Equal(6144.0, used.ValueMB);
    }

    [Fact]
    public async Task GpuMemoryMin_Preserved()
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
                        Sensors = [SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 4000f, 2000f, 8000f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(2000.0, live.GpuMemorySensors[0].MinMB);
    }

    [Fact]
    public async Task GpuMemoryMax_Preserved()
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
                        Sensors = [SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 4000f, 2000f, 8000f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(8000.0, live.GpuMemorySensors[0].MaxMB);
    }

    [Fact]
    public async Task GpuMemoryValueNull_StaysNull()
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
                        Sensors = [SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", null)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.GpuMemorySensors).ValueMB);
        Assert.Equal(0, live.ValidGpuMemorySensorCount);
    }

    [Fact]
    public async Task GpuMemoryNaN_NormalizedToNull()
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
                        Sensors = [SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", float.NaN)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.GpuMemorySensors).ValueMB);
    }

    [Fact]
    public async Task GpuMemoryInfinity_NormalizedToNull()
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
                        Sensors = [SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", float.PositiveInfinity)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.GpuMemorySensors).ValueMB);
    }

    [Fact]
    public async Task CpuSmallData_Ignored()
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
                        Sensors = [SmallData("L2 Cache", "/intelcpu/0/small-data/0", 2048f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.GpuMemorySensors);
    }

    [Fact]
    public async Task MemorySmallData_Ignored()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "G.Skill DDR5",
                        Identifier = "/ram/0",
                        HardwareType = InternalHardwareType.Memory,
                        Sensors = [SmallData("Memory Used", "/ram/0/small-data/0", 8192f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.GpuMemorySensors);
    }

    [Fact]
    public async Task StorageSmallData_Ignored()
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
                        Sensors = [SmallData("Used Space", "/nvme/0/small-data/0", 102400f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.GpuMemorySensors);
    }

    [Fact]
    public async Task MotherboardSmallData_Ignored()
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
                        Sensors = [SmallData("Chipset Data", "/mb/0/small-data/0", 100f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.GpuMemorySensors);
    }

    [Fact]
    public async Task Temperature_NotInGpuMemorySensors()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.GpuMemorySensors, s => s.SensorName == "GPU Core");
    }

    [Fact]
    public async Task Load_NotInGpuMemorySensors()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.GpuMemorySensors, s => Assert.Equal(0, s.ValueMB == 42.0 ? 1 : 0));
        Assert.Equal(2, live.GpuMemorySensors.Count);
    }

    [Fact]
    public async Task Clock_NotInGpuMemorySensors()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.GpuMemorySensors, s => s.SensorName == "GPU Clock");
    }

    [Fact]
    public async Task SmallData_NotInPerformanceSensors()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.PerformanceSensors, s => s.SensorName == "GPU Memory Used");
        Assert.DoesNotContain(live.PerformanceSensors, s => s.SensorName == "GPU Memory Total");
    }

    [Fact]
    public async Task SmallData_NotInTemperatureSensors()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.TemperatureSensors, s => s.SensorName == "GPU Memory Used");
    }

    [Fact]
    public async Task TwoGpus_SameName_DifferentIdentifier_Preserved()
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
                        Sensors = [SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 6144f)]
                    },
                    new FakeHardware
                    {
                        Name = "AMD Radeon RX 7800 XT",
                        Identifier = "/gpu-amd/0",
                        HardwareType = InternalHardwareType.Gpu,
                        Sensors = [SmallData("GPU Memory Used", "/gpu-amd/0/small-data/0", 8192f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.GpuMemorySensors.Count);
        Assert.Contains(live.GpuMemorySensors, s => s.HardwareName == "NVIDIA GeForce RTX 4070");
        Assert.Contains(live.GpuMemorySensors, s => s.HardwareName == "AMD Radeon RX 7800 XT");
    }

    [Fact]
    public async Task DuplicateIdentifier_GpuMemoryDeduped()
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
                        Sensors =
                        [
                            SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 6144f),
                            SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 7000f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(6144.0, Assert.Single(live.GpuMemorySensors).ValueMB);
    }

    [Fact]
    public async Task MultipleSmallData_PreservedWithoutClassification()
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
                        Sensors =
                        [
                            SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 6144f),
                            SmallData("GPU Memory Free", "/gpu-nvidia/0/small-data/1", 6144f),
                            SmallData("GPU Memory Total", "/gpu-nvidia/0/small-data/2", 12288f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(3, live.GpuMemorySensors.Count);
        Assert.Equal("GPU Memory Used", live.GpuMemorySensors[0].SensorName);
        Assert.Equal("GPU Memory Free", live.GpuMemorySensors[1].SensorName);
        Assert.Equal("GPU Memory Total", live.GpuMemorySensors[2].SensorName);
    }

    [Fact]
    public void UsedFreeTotalNames_PreservedLiterally_NoLogic()
    {
        // El modelo solo conserva el nombre: no existen propiedades semánticas de memoria.
        var properties = typeof(HardwareGpuMemorySensor).GetProperties().Select(p => p.Name).ToList();

        Assert.Contains("SensorName", properties);
        Assert.DoesNotContain(properties, n => n is "UsedMB" or "FreeMB" or "TotalMB" or
            "DedicatedMB" or "SharedMB" or "UsagePercent");
    }

    [Fact]
    public void DedicatedName_NoSpecialLogic()
    {
        var sensor = new HardwareGpuMemorySensor { SensorName = "D3D Dedicated Memory Used", ValueMB = 4096 };

        Assert.Equal("D3D Dedicated Memory Used", sensor.SensorName);
        Assert.Equal(4096.0, sensor.ValueMB);
        Assert.Equal("MB", sensor.Unit);
    }

    [Fact]
    public void SharedName_NoSpecialLogic()
    {
        var sensor = new HardwareGpuMemorySensor { SensorName = "D3D Shared Memory Used", ValueMB = 1024 };

        Assert.Equal("D3D Shared Memory Used", sensor.SensorName);
        Assert.Equal(1024.0, sensor.ValueMB);
        Assert.Equal("MB", sensor.Unit);
    }

    // =====================
    // B.2.2 - Un solo Refresh con SmallData
    // =====================

    [Fact]
    public async Task CombinedCaptureWithSmallData_CreateOnce()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task CombinedCaptureWithSmallData_RefreshOnce()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task CombinedCaptureWithSmallData_DisposeOnce()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.DisposeCount);
    }

    [Fact]
    public async Task CombinedCapture_SingleRefresh_AllMetricFamilies()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var live = await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
        Assert.Equal(1, factory.Session.DisposeCount);

        Assert.Single(live.TemperatureSensors);
        Assert.Equal(2, live.PerformanceSensors.Count);
        Assert.Equal(2, live.GpuMemorySensors.Count);
    }

    // =====================
    // B.2.2 - Watch live con memoria GPU
    // =====================

    [Fact]
    public async Task WatchLiveGpuMemory_ReusesSession()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(3, factory.Session!.RefreshCount);
        Assert.All(samples, s => Assert.Equal(2, s.GpuMemorySensors.Count));
    }

    [Fact]
    public async Task GpuMemoryValues_ChangeAcrossSamples()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out var used, out _) };
        var current = 6144f;
        factory.Session!.OnRefresh = () => { current += 512; used.Value = current; };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.Equal(6656.0, samples[0].GpuMemorySensors.Single(s => s.SensorName == "GPU Memory Used").ValueMB);
        Assert.Equal(7168.0, samples[1].GpuMemorySensors.Single(s => s.SensorName == "GPU Memory Used").ValueMB);
    }

    [Fact]
    public async Task GpuMemoryLists_IndependentBetweenSamples()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.NotSame(samples[0].GpuMemorySensors, samples[1].GpuMemorySensors);
        Assert.NotSame(samples[1].GpuMemorySensors, samples[2].GpuMemorySensors);
    }

    [Fact]
    public async Task RefreshFailure_EmptiesAllThreeLists()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        factory.Session!.ThrowOnRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 1);

        Assert.False(samples[0].IsAvailable);
        Assert.Empty(samples[0].TemperatureSensors);
        Assert.Empty(samples[0].PerformanceSensors);
        Assert.Empty(samples[0].GpuMemorySensors);
    }

    [Fact]
    public async Task RefreshFailure_ThenSuccess_RecoversAllThree()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        factory.Session!.ThrowOnFirstRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.False(samples[0].IsAvailable);
        Assert.True(samples[1].IsAvailable);
        Assert.Single(samples[1].TemperatureSensors);
        Assert.Equal(2, samples[1].PerformanceSensors.Count);
        Assert.Equal(2, samples[1].GpuMemorySensors.Count);
    }

    [Fact]
    public async Task PartialNodeError_PreservesValidGpuMemory()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Broken GPU",
                        Identifier = "/gpu-nvidia/0",
                        HardwareType = InternalHardwareType.Gpu,
                        ThrowOnSensorsRead = true
                    },
                    new FakeHardware
                    {
                        Name = "AMD Radeon RX 7800 XT",
                        Identifier = "/gpu-amd/0",
                        HardwareType = InternalHardwareType.Gpu,
                        Sensors = [SmallData("GPU Memory Used", "/gpu-amd/0/small-data/0", 8192f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
        Assert.NotEmpty(live.Errors);
        var sensor = Assert.Single(live.GpuMemorySensors);
        Assert.Equal("AMD Radeon RX 7800 XT", sensor.HardwareName);
    }

    [Fact]
    public async Task OldTemperatureApis_IgnoreGpuMemory()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var snapshot = await service.GetTemperatureSnapshotAsync();

        Assert.True(snapshot.IsAvailable);
        Assert.Single(snapshot.Sensors);
        Assert.DoesNotContain(snapshot.Sensors, s => s.SensorName == "GPU Memory Used");
    }

    [Fact]
    public void LiveSnapshot_NoGpuMemorySummaryProperties()
    {
        var propertyNames = typeof(HardwareLiveSnapshot)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "GpuMemorySummary" or "UsedMB" or "TotalMB" or
            "FreeMB" or "UsagePercent");
    }

    [Fact]
    public async Task GpuMemory_UsesInjectedFactory_NoRealHardware()
    {
        var factory = new FakeFactory { Session = CreateGpuMemorySession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(live.IsAvailable);
        Assert.Equal(2, live.GpuMemorySensors.Count);
    }

    // =====================
    // Tests Fase B.2.3 - Telemetría de batería
    // =====================

    private static FakeSensor Level(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Level, value, min, max);

    private static FakeSensor Energy(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Energy, value, min, max);

    private static FakeSensor Voltage(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Voltage, value, min, max);

    private static FakeSensor Current(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Current, value, min, max);

    private static FakeSensor Power(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Power, value, min, max);

    private static FakeSensor BatteryTime(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.TimeSpan, value, min, max);

    private static FakeHardware CreateBatteryHardware(string name = "Standard Battery",
        string identifier = "/battery/0", IReadOnlyList<ISensorNode>? sensors = null)
    {
        return new FakeHardware
        {
            Name = name,
            Identifier = identifier,
            HardwareType = InternalHardwareType.Battery,
            Sensors = sensors ?? new List<ISensorNode>()
        };
    }

    private static FakeSession CreateFullSession(out FakeSensor batteryLevel, out FakeSensor batteryEnergy,
        out FakeSensor batteryPower)
    {
        batteryLevel = Level("Charge Level", "/battery/0/level/0", 85f);
        batteryEnergy = Energy("Remaining Capacity", "/battery/0/energy/0", 45000f);
        batteryPower = Power("Discharge Rate", "/battery/0/power/0", 12.5f);

        return new FakeSession
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
                        Load("CPU Total", "/intelcpu/0/load/0", 35.0f),
                        Clock("CPU Clock", "/intelcpu/0/clock/0", 3700f)
                    ]
                },
                new FakeHardware
                {
                    Name = "NVIDIA GeForce RTX 4070",
                    Identifier = "/gpu-nvidia/0",
                    HardwareType = InternalHardwareType.Gpu,
                    Sensors =
                    [
                        Temp("GPU Core", "/gpu-nvidia/0/temperature/0", 55.0f),
                        Load("GPU Core", "/gpu-nvidia/0/load/0", 42.0f),
                        Clock("GPU Clock", "/gpu-nvidia/0/clock/0", 1905f),
                        SmallData("GPU Memory Used", "/gpu-nvidia/0/small-data/0", 6144f)
                    ]
                },
                new FakeHardware
                {
                    Name = "Standard Battery",
                    Identifier = "/battery/0",
                    HardwareType = InternalHardwareType.Battery,
                    Sensors =
                    [
                        Temp("Battery Temperature", "/battery/0/temperature/0", 32.0f),
                        batteryLevel,
                        batteryEnergy,
                        batteryPower
                    ]
                }
            ]
        };
    }

    [Fact]
    public async Task BatteryHardwareType_MapsToBateria()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Level("Charge Level", "/battery/0/level/0", 85f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal("Batería", sensor.HardwareType);
        Assert.Equal("Standard Battery", sensor.HardwareName);
    }

    [Fact]
    public void IsBatteryEnabled_Activated()
    {
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Battery);
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Cpu);
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Gpu);
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Memory);
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Motherboard);
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Storage);
        Assert.True(LibreHardwareMonitorFactory.EnabledHardwareConfiguration.Controller);
    }

    [Fact]
    public async Task BatteryLevel_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Level("Charge Level", "/battery/0/level/0", 85f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal(HardwareBatteryMetricType.Level, sensor.MetricType);
        Assert.Equal(85.0, sensor.Value);
        Assert.True(live.HasBatterySensors);
        Assert.Equal(1, live.ValidBatterySensorCount);
    }

    [Fact]
    public async Task BatteryEnergy_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Energy("Remaining Capacity", "/battery/0/energy/0", 45000f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal(HardwareBatteryMetricType.Energy, sensor.MetricType);
        Assert.Equal(45000.0, sensor.Value);
    }

    [Fact]
    public async Task BatteryVoltage_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Voltage("Voltage", "/battery/0/voltage/0", 11.4f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal(HardwareBatteryMetricType.Voltage, sensor.MetricType);
        Assert.Equal(11.4, (double)sensor.Value!, 2);
    }

    [Fact]
    public async Task BatteryCurrent_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Current("Current", "/battery/0/current/0", 1.8f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal(HardwareBatteryMetricType.Current, sensor.MetricType);
        Assert.Equal(1.8, (double)sensor.Value!, 2);
    }

    [Fact]
    public async Task BatteryPower_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Power("Charge Rate", "/battery/0/power/0", 25.0f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal(HardwareBatteryMetricType.Power, sensor.MetricType);
        Assert.Equal(25.0, sensor.Value);
    }

    [Fact]
    public async Task BatteryTimeSpan_Captured()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [BatteryTime("Remaining Time (Estimated)", "/battery/0/timespan/0", 7200f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal(HardwareBatteryMetricType.TimeSpan, sensor.MetricType);
        Assert.Equal(7200.0, sensor.Value);
    }

    [Fact]
    public async Task BatteryTemperature_InTemperatureSensors()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Temp("Battery Temperature", "/battery/0/temperature/0", 32.0f)])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.TemperatureSensors);
        Assert.Equal("Batería", sensor.HardwareType);
        Assert.Equal(32.0, sensor.ValueCelsius);
    }

    [Fact]
    public async Task BatteryTemperature_NotInBatterySensors()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Temp("Battery Temperature", "/battery/0/temperature/0", 32.0f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.BatterySensors);
    }

    [Fact]
    public async Task BatteryLevel_UnitPercent()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Level("Charge Level", "/battery/0/level/0", 85f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("%", Assert.Single(live.BatterySensors).Unit);
    }

    [Fact]
    public async Task BatteryEnergy_UnitMWh()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Energy("Remaining Capacity", "/battery/0/energy/0", 45000f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("mWh", Assert.Single(live.BatterySensors).Unit);
    }

    [Fact]
    public async Task BatteryVoltage_UnitV()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Voltage("Voltage", "/battery/0/voltage/0", 11.4f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("V", Assert.Single(live.BatterySensors).Unit);
    }

    [Fact]
    public async Task BatteryCurrent_UnitA()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Current("Current", "/battery/0/current/0", 1.8f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("A", Assert.Single(live.BatterySensors).Unit);
    }

    [Fact]
    public async Task BatteryPower_UnitW()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Power("Charge Rate", "/battery/0/power/0", 25.0f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("W", Assert.Single(live.BatterySensors).Unit);
    }

    [Fact]
    public async Task BatteryTimeSpan_UnitSeconds()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [BatteryTime("Remaining Time (Estimated)", "/battery/0/timespan/0", 7200f)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("s", Assert.Single(live.BatterySensors).Unit);
    }

    [Fact]
    public async Task BatteryValues_PreservedNoConversion()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors:
                [
                    Energy("Remaining Capacity", "/battery/0/energy/0", 45000f),
                    BatteryTime("Remaining Time (Estimated)", "/battery/0/timespan/0", 7200f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(45000.0, live.BatterySensors.Single(s => s.MetricType == HardwareBatteryMetricType.Energy).Value);
        Assert.Equal(7200.0, live.BatterySensors.Single(s => s.MetricType == HardwareBatteryMetricType.TimeSpan).Value);
    }

    [Fact]
    public async Task BatteryValueNull_StaysNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Level("Charge Level", "/battery/0/level/0", null)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.BatterySensors).Value);
        Assert.Equal(0, live.ValidBatterySensorCount);
    }

    [Fact]
    public async Task BatteryNaN_NormalizedToNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Level("Charge Level", "/battery/0/level/0", float.NaN)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.BatterySensors).Value);
    }

    [Fact]
    public async Task BatteryInfinity_NormalizedToNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors: [Power("Charge Rate", "/battery/0/power/0", float.PositiveInfinity)])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.BatterySensors).Value);
    }

    [Fact]
    public async Task CpuPower_NotInBatterySensors()
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
                        Sensors = [Power("CPU Package Power", "/intelcpu/0/power/0", 45.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.BatterySensors);
    }

    [Fact]
    public async Task MotherboardVoltage_NotInBatterySensors()
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
                        Sensors = [Voltage("CPU VCore", "/mb/0/voltage/0", 1.2f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.BatterySensors);
    }

    [Fact]
    public async Task GpuLevel_NotInBatterySensors()
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
                        Sensors = [Level("GPU Level", "/gpu-nvidia/0/level/0", 50.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.BatterySensors);
    }

    [Fact]
    public async Task MultipleMetrics_SingleBattery_Preserved()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors:
                [
                    Level("Charge Level", "/battery/0/level/0", 85f),
                    Energy("Remaining Capacity", "/battery/0/energy/0", 45000f),
                    Voltage("Voltage", "/battery/0/voltage/0", 11.4f),
                    Current("Current", "/battery/0/current/0", 1.8f),
                    Power("Discharge Rate", "/battery/0/power/0", 12.5f),
                    BatteryTime("Remaining Time (Estimated)", "/battery/0/timespan/0", 7200f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(6, live.BatterySensors.Count);
        Assert.Equal(6, live.ValidBatterySensorCount);
        Assert.Contains(live.BatterySensors, s => s.MetricType == HardwareBatteryMetricType.Level);
        Assert.Contains(live.BatterySensors, s => s.MetricType == HardwareBatteryMetricType.Energy);
        Assert.Contains(live.BatterySensors, s => s.MetricType == HardwareBatteryMetricType.Voltage);
        Assert.Contains(live.BatterySensors, s => s.MetricType == HardwareBatteryMetricType.Current);
        Assert.Contains(live.BatterySensors, s => s.MetricType == HardwareBatteryMetricType.Power);
        Assert.Contains(live.BatterySensors, s => s.MetricType == HardwareBatteryMetricType.TimeSpan);
    }

    [Fact]
    public async Task TwoBatteries_Coexist()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    CreateBatteryHardware("Battery A", "/battery/0",
                        [Level("Charge Level", "/battery/0/level/0", 80f)]),
                    CreateBatteryHardware("Battery B", "/battery/1",
                        [Level("Charge Level", "/battery/1/level/0", 60f)])
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.BatterySensors.Count);
        Assert.Contains(live.BatterySensors, s => s.HardwareName == "Battery A" && s.Value == 80.0);
        Assert.Contains(live.BatterySensors, s => s.HardwareName == "Battery B" && s.Value == 60.0);
    }

    [Fact]
    public async Task SameSensorName_DifferentIdentifier_BatteryPreserved()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    CreateBatteryHardware("Battery A", "/battery/0",
                        [Level("Charge Level", "/battery/0/level/0", 80f)]),
                    CreateBatteryHardware("Battery B", "/battery/1",
                        [Level("Charge Level", "/battery/1/level/0", 60f)])
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.BatterySensors.Count);
        Assert.All(live.BatterySensors, s => Assert.Equal("Charge Level", s.SensorName));
    }

    [Fact]
    public async Task DuplicateIdentifier_BatteryDeduped()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateBatteryHardware(sensors:
                [
                    Level("Charge Level", "/battery/0/level/0", 80f),
                    Level("Charge Level", "/battery/0/level/0", 90f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(80.0, Assert.Single(live.BatterySensors).Value);
    }

    [Fact]
    public void ChargeLevelName_NoChargePercentSemantics()
    {
        var sensor = new HardwareBatterySensor { SensorName = "Charge Level", Value = 85 };
        var properties = typeof(HardwareBatterySensor).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal("Charge Level", sensor.SensorName);
        Assert.Equal(85.0, sensor.Value);
        Assert.DoesNotContain(properties, n => n is "ChargePercent" or "IsCharging");
    }

    [Fact]
    public void DegradationLevelName_NoHealthSemantics()
    {
        var sensor = new HardwareBatterySensor { SensorName = "Degradation Level", Value = 20 };
        var properties = typeof(HardwareBatterySensor).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal("Degradation Level", sensor.SensorName);
        Assert.Equal(20.0, sensor.Value);
        Assert.DoesNotContain(properties, n => n is "HealthStatus" or "HealthPercent" or "NeedsReplacement" or "IsHealthy");
    }

    [Fact]
    public void DischargeRateName_NoIsDischarging()
    {
        var sensor = new HardwareBatterySensor { SensorName = "Discharge Rate", Value = 12.5 };

        Assert.Equal("Discharge Rate", sensor.SensorName);
        Assert.Equal(12.5, sensor.Value);
    }

    [Fact]
    public void ChargeRateName_NoIsCharging()
    {
        var sensor = new HardwareBatterySensor { SensorName = "Charge Rate", Value = 25.0 };

        Assert.Equal("Charge Rate", sensor.SensorName);
        Assert.Equal(25.0, sensor.Value);
    }

    [Fact]
    public void BatteryModel_NoHealthPercentCalculated()
    {
        var batteryProperties = typeof(HardwareBatterySensor).GetProperties().Select(p => p.Name).ToList();
        var snapshotProperties = typeof(HardwareLiveSnapshot).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(batteryProperties, n => n is "HealthPercent" or "DegradationPercent" or
            "DesignedCapacity" or "FullChargedCapacity" or "RemainingCapacity");
        Assert.DoesNotContain(snapshotProperties, n => n is "IsCharging" or "IsDischarging" or
            "HealthStatus" or "Severity" or "Recommendation");
    }

    // =====================
    // B.2.3 - Un solo Refresh con batería
    // =====================

    [Fact]
    public async Task FullCombinedCapture_CreateOnce()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task FullCombinedCapture_RefreshOnce()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task FullCombinedCapture_DisposeOnce()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.DisposeCount);
    }

    [Fact]
    public async Task FullCombinedCapture_SingleRefresh_AllMetricFamilies()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var live = await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
        Assert.Equal(1, factory.Session.DisposeCount);

        // CPU temp + load + clock; GPU temp + load + clock + smalldata; battery temp + level + energy + power
        Assert.Equal(3, live.TemperatureSensors.Count);
        Assert.Equal(4, live.PerformanceSensors.Count);
        Assert.Single(live.GpuMemorySensors);
        Assert.Equal(3, live.BatterySensors.Count);
        Assert.Contains(live.TemperatureSensors, s => s.HardwareType == "Batería");
    }

    // =====================
    // B.2.3 - Watch live con batería
    // =====================

    [Fact]
    public async Task WatchLiveBattery_ReusesSession()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(3, factory.Session!.RefreshCount);
        Assert.All(samples, s => Assert.Equal(3, s.BatterySensors.Count));
    }

    [Fact]
    public async Task BatteryValues_ChangeAcrossSamples()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out var level, out _, out _) };
        var current = 85f;
        factory.Session!.OnRefresh = () => { current -= 2; level.Value = current; };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.Equal(83.0, samples[0].BatterySensors.Single(s => s.MetricType == HardwareBatteryMetricType.Level).Value);
        Assert.Equal(81.0, samples[1].BatterySensors.Single(s => s.MetricType == HardwareBatteryMetricType.Level).Value);
    }

    [Fact]
    public async Task BatteryLists_IndependentBetweenSamples()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.NotSame(samples[0].BatterySensors, samples[1].BatterySensors);
        Assert.NotSame(samples[1].BatterySensors, samples[2].BatterySensors);
    }

    [Fact]
    public async Task RefreshFailure_EmptiesAllFourLists()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        factory.Session!.ThrowOnRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 1);

        Assert.False(samples[0].IsAvailable);
        Assert.Empty(samples[0].TemperatureSensors);
        Assert.Empty(samples[0].PerformanceSensors);
        Assert.Empty(samples[0].GpuMemorySensors);
        Assert.Empty(samples[0].BatterySensors);
    }

    [Fact]
    public async Task RefreshFailure_ThenSuccess_RecoversAllFour()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        factory.Session!.ThrowOnFirstRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.False(samples[0].IsAvailable);
        Assert.True(samples[1].IsAvailable);
        Assert.Equal(3, samples[1].TemperatureSensors.Count);
        Assert.Equal(4, samples[1].PerformanceSensors.Count);
        Assert.Single(samples[1].GpuMemorySensors);
        Assert.Equal(3, samples[1].BatterySensors.Count);
    }

    [Fact]
    public async Task BatteryNodeError_PreservesCpuGpu()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Standard Battery",
                        Identifier = "/battery/0",
                        HardwareType = InternalHardwareType.Battery,
                        ThrowOnSensorsRead = true
                    },
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors =
                        [
                            Temp("Package", "/intelcpu/0/temperature/0", 60.0f),
                            Load("CPU Total", "/intelcpu/0/load/0", 35.0f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
        Assert.NotEmpty(live.Errors);
        Assert.Single(live.TemperatureSensors);
        Assert.Single(live.PerformanceSensors);
        Assert.Empty(live.BatterySensors);
    }

    [Fact]
    public async Task CpuNodeError_PreservesBattery()
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
                        ThrowOnSensorsRead = true
                    },
                    CreateBatteryHardware(sensors: [Level("Charge Level", "/battery/0/level/0", 85f)])
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
        Assert.NotEmpty(live.Errors);
        Assert.Empty(live.TemperatureSensors);
        var sensor = Assert.Single(live.BatterySensors);
        Assert.Equal("Batería", sensor.HardwareType);
    }

    // =====================
    // B.2.3 - PC sin batería
    // =====================

    [Fact]
    public async Task NoBattery_IsAvailableTrue()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
    }

    [Fact]
    public async Task NoBattery_EmptyBatterySensors()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };

        var live = await CaptureLive(factory);

        Assert.Empty(live.BatterySensors);
        Assert.False(live.HasBatterySensors);
    }

    [Fact]
    public async Task NoBattery_NoSpecificError()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };

        var live = await CaptureLive(factory);

        Assert.DoesNotContain(live.Errors, e => e.Contains("bater", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(live.Errors);
    }

    // =====================
    // B.2.3 - Compatibilidad con API de temperatura
    // =====================

    [Fact]
    public async Task OldTemperatureApis_StillWork_WithBattery()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var snapshot = await service.GetTemperatureSnapshotAsync();

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(3, snapshot.Sensors.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task BatteryTemperature_InTemperatureApi()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var snapshot = await service.GetTemperatureSnapshotAsync();

        Assert.Contains(snapshot.Sensors, s => s.SensorName == "Battery Temperature");
    }

    [Fact]
    public void LiveSnapshot_NoBatteryHealthProperties()
    {
        var snapshotProperties = typeof(HardwareLiveSnapshot).GetProperties().Select(p => p.Name).ToList();
        var batteryProperties = typeof(HardwareBatterySensor).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(snapshotProperties, n => n is "IsCharging" or "IsDischarging" or
            "HealthStatus" or "Severity" or "Recommendation");
        Assert.DoesNotContain(batteryProperties, n => n is "IsCharging" or "IsDischarging" or
            "HealthStatus" or "Severity" or "Recommendation" or "NeedsReplacement");
    }

    [Fact]
    public async Task Battery_UsesInjectedFactory_NoRealHardware()
    {
        var factory = new FakeFactory { Session = CreateFullSession(out _, out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(live.IsAvailable);
        Assert.Equal(3, live.BatterySensors.Count);
    }

    // =====================
    // Tests Fase B.3.2 - Timings SPD (SensorType.Timing)
    // =====================

    private static FakeSensor Timing(string name, string identifier, float? value = null,
        float? min = null, float? max = null) =>
        new(name, identifier, InternalSensorType.Timing, value, min, max);

    private static FakeHardware CreateDimmHardware(string name = "DDR4-3200 DIMM",
        string identifier = "/mem/dimm/0", IReadOnlyList<ISensorNode>? sensors = null)
    {
        return new FakeHardware
        {
            Name = name,
            Identifier = identifier,
            HardwareType = InternalHardwareType.Memory,
            Sensors = sensors ?? new List<ISensorNode>()
        };
    }

    private static FakeSession CreateDimmSession(out FakeSensor taa, out FakeSensor trcd)
    {
        taa = Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f);
        trcd = Timing("tRCD (RAS to CAS Delay Time)", "/mem/dimm/0/timing/1", 16.0f);

        return new FakeSession
        {
            Hardware = [CreateDimmHardware(sensors: [taa, trcd])]
        };
    }

    [Fact]
    public async Task Timing_OfMemoryHardware_Captured()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.MemoryTimingSensors.Count);
        Assert.True(live.HasMemoryTimingSensors);
        Assert.Equal(2, live.ValidMemoryTimingSensorCount);
    }

    [Fact]
    public async Task CpuTiming_Ignored()
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
                        Sensors = [Timing("tAA (CAS Latency Time)", "/intelcpu/0/timing/0", 14.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.MemoryTimingSensors);
    }

    [Fact]
    public async Task GpuTiming_Ignored()
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
                        Sensors = [Timing("tAA (CAS Latency Time)", "/gpu-nvidia/0/timing/0", 14.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.MemoryTimingSensors);
    }

    [Fact]
    public async Task BatteryTiming_Ignored()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "Standard Battery",
                        Identifier = "/battery/0",
                        HardwareType = InternalHardwareType.Battery,
                        Sensors = [Timing("tAA (CAS Latency Time)", "/battery/0/timing/0", 14.0f)]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.MemoryTimingSensors);
    }

    [Fact]
    public async Task Timing_UnitIsNs()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.MemoryTimingSensors, s => Assert.Equal("ns", s.Unit));
    }

    [Fact]
    public async Task ValueNanoseconds_Preserved()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(14.0, live.MemoryTimingSensors[0].ValueNanoseconds);
        Assert.Equal(16.0, live.MemoryTimingSensors[1].ValueNanoseconds);
    }

    [Fact]
    public async Task MinNanoseconds_Preserved()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f, 13.5f, 15.0f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(13.5, live.MemoryTimingSensors[0].MinNanoseconds);
    }

    [Fact]
    public async Task MaxNanoseconds_Preserved()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f, 13.5f, 15.0f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(15.0, live.MemoryTimingSensors[0].MaxNanoseconds);
    }

    [Fact]
    public async Task TimingNull_StaysNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", null)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.MemoryTimingSensors).ValueNanoseconds);
        Assert.Equal(0, live.ValidMemoryTimingSensorCount);
    }

    [Fact]
    public async Task TimingNaN_NormalizedToNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", float.NaN)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.MemoryTimingSensors).ValueNanoseconds);
    }

    [Fact]
    public async Task TimingInfinity_NormalizedToNull()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", float.PositiveInfinity)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Null(Assert.Single(live.MemoryTimingSensors).ValueNanoseconds);
    }

    [Fact]
    public async Task SensorName_PreservedLiterally()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f),
                    Timing("tCKAVGmin (Minimum Cycle Time)", "/mem/dimm/0/timing/2", 0.625f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal("tAA (CAS Latency Time)", live.MemoryTimingSensors[0].SensorName);
        Assert.Equal("tCKAVGmin (Minimum Cycle Time)", live.MemoryTimingSensors[1].SensorName);
    }

    [Fact]
    public void TimingModel_NoCasLatencyCycles()
    {
        var propertyNames = typeof(HardwareMemoryTimingSensor).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(propertyNames, n => n is "CASLatency" or "CasLatencyCycles" or "CL" or
            "TRCD" or "TRP" or "TRAS" or "TRC" or "TRFC" or "TimingProfile" or "XmpProfile" or "ExpoProfile");
    }

    [Fact]
    public void TRcdName_NoSpecificProperty()
    {
        var sensor = new HardwareMemoryTimingSensor { SensorName = "tRCD (RAS to CAS Delay Time)", ValueNanoseconds = 16.0 };
        var propertyNames = typeof(HardwareMemoryTimingSensor).GetProperties().Select(p => p.Name).ToList();

        Assert.Equal("tRCD (RAS to CAS Delay Time)", sensor.SensorName);
        Assert.Equal(16.0, sensor.ValueNanoseconds);
        Assert.DoesNotContain(propertyNames, n => n == "TRCD");
    }

    [Fact]
    public void NoClCalculated_FromNanoseconds()
    {
        // tAA = 14.0 ns se conserva como 14.0 ns; nunca se convierte a CL14/ciclos.
        var sensor = new HardwareMemoryTimingSensor
        {
            SensorName = "tAA (CAS Latency Time)",
            ValueNanoseconds = 14.0
        };

        Assert.Equal(14.0, sensor.ValueNanoseconds);
        Assert.Equal("ns", sensor.Unit);
    }

    [Fact]
    public void NoNsToCyclesConversion()
    {
        // El valor se conserva tal cual: sin dividir por tCK ni multiplicar por frecuencia.
        var sensor = new HardwareMemoryTimingSensor
        {
            SensorName = "tAA (CAS Latency Time)",
            ValueNanoseconds = 14.0
        };

        Assert.Equal(14.0, sensor.ValueNanoseconds);
    }

    [Fact]
    public async Task TwoDimms_SameTimingName_DifferentIdentifier_Preserved()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    CreateDimmHardware("DDR4-3200 DIMM", "/mem/dimm/0",
                        [Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f)]),
                    CreateDimmHardware("DDR4-3200 DIMM", "/mem/dimm/1",
                        [Timing("tAA (CAS Latency Time)", "/mem/dimm/1/timing/0", 15.0f)])
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(2, live.MemoryTimingSensors.Count);
        Assert.Contains(live.MemoryTimingSensors, s => s.HardwareIdentifier == "/mem/dimm/0");
        Assert.Contains(live.MemoryTimingSensors, s => s.HardwareIdentifier == "/mem/dimm/1");
    }

    [Fact]
    public async Task DuplicateIdentifier_TimingDeduped()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f),
                    Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 15.0f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Equal(14.0, Assert.Single(live.MemoryTimingSensors).ValueNanoseconds);
    }

    [Fact]
    public async Task HardwareIdentifier_Preserved()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.All(live.MemoryTimingSensors, s => Assert.Equal("/mem/dimm/0", s.HardwareIdentifier));
        Assert.All(live.MemoryTimingSensors, s => Assert.Equal("Memoria", s.HardwareType));
    }

    [Fact]
    public async Task SensorTypeData_NotInMemoryTimingSensors()
    {
        // SensorType.Data NO se incorpora en B.3.2: queda como Other.
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    new FakeSensor("Memory Used", "/mem/dimm/0/data/0", InternalSensorType.Other, 8192f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.MemoryTimingSensors);
    }

    [Fact]
    public async Task DimmTemperature_InTemperatureSensors()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Temp("Temperature", "/mem/dimm/0/temperature/0", 45.0f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        var sensor = Assert.Single(live.TemperatureSensors);
        Assert.Equal("Memoria", sensor.HardwareType);
        Assert.Equal(45.0, sensor.ValueCelsius);
    }

    [Fact]
    public async Task DimmTemperature_NotInMemoryTimingSensors()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware = [CreateDimmHardware(sensors:
                [
                    Temp("Temperature", "/mem/dimm/0/temperature/0", 45.0f)
                ])]
            }
        };

        var live = await CaptureLive(factory);

        Assert.Empty(live.MemoryTimingSensors);
    }

    // =====================
    // B.3.2 - Un solo Refresh con timings
    // =====================

    private static FakeSession CreateFullDimmSession()
    {
        var session = CreateFullSession(out _, out _, out _);
        session.Hardware = session.Hardware.Append(CreateDimmHardware(sensors:
        [
            Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f),
            Timing("tRCD (RAS to CAS Delay Time)", "/mem/dimm/0/timing/1", 16.0f)
        ])).ToList();

        return session;
    }

    [Fact]
    public async Task CombinedWithTimings_CreateOnce()
    {
        var factory = new FakeFactory { Session = CreateFullDimmSession() };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
    }

    [Fact]
    public async Task CombinedWithTimings_RefreshOnce()
    {
        var factory = new FakeFactory { Session = CreateFullDimmSession() };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public async Task CombinedWithTimings_DisposeOnce()
    {
        var factory = new FakeFactory { Session = CreateFullDimmSession() };
        var service = CreateService(factory, new FakeDelay());

        await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.Session!.DisposeCount);
    }

    [Fact]
    public async Task CombinedWithTimings_SingleRefresh_AllFamilies()
    {
        var factory = new FakeFactory { Session = CreateFullDimmSession() };
        var service = CreateService(factory, new FakeDelay());

        var live = await service.GetLiveSnapshotAsync();

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
        Assert.Equal(1, factory.Session.DisposeCount);

        Assert.Equal(3, live.TemperatureSensors.Count);
        Assert.Equal(4, live.PerformanceSensors.Count);
        Assert.Single(live.GpuMemorySensors);
        Assert.Equal(3, live.BatterySensors.Count);
        Assert.Equal(2, live.MemoryTimingSensors.Count);
    }

    // =====================
    // B.3.2 - Watch live y detección tardía de SPD
    // =====================

    [Fact]
    public async Task WatchLiveTiming_ReusesSession()
    {
        var factory = new FakeFactory { Session = CreateFullDimmSession() };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.Equal(3, samples.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(3, factory.Session!.RefreshCount);
        Assert.All(samples, s => Assert.Equal(2, s.MemoryTimingSensors.Count));
    }

    [Fact]
    public async Task WatchLive_IncludesMemoryTimingSensors()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.All(samples, s => Assert.Equal(2, s.MemoryTimingSensors.Count));
        Assert.True(samples[0].HasMemoryTimingSensors);
    }

    [Fact]
    public async Task TimingLists_IndependentBetweenSamples()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 3);

        Assert.NotSame(samples[0].MemoryTimingSensors, samples[1].MemoryTimingSensors);
        Assert.NotSame(samples[1].MemoryTimingSensors, samples[2].MemoryTimingSensors);
    }

    [Fact]
    public async Task NoSpd_NoSpecificError()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
        Assert.Empty(live.MemoryTimingSensors);
        Assert.False(live.HasMemoryTimingSensors);
        Assert.Empty(live.Errors);
    }

    [Fact]
    public async Task NoSpd_IsAvailableTrue()
    {
        var factory = new FakeFactory { Session = CreateCpuSession(out _) };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
    }

    [Fact]
    public async Task LateTimingAppears_InLaterSnapshot_WithoutNewSession()
    {
        // Simula la detección tardía interna de LHM (MemoryGroup agrega el DIMM después de Open):
        // el servicio vuelve a consultar session.Hardware en cada snapshot.
        var session = new FakeSession
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
        };
        var factory = new FakeFactory { Session = session };
        var service = CreateService(factory, new FakeDelay());

        var samples = new List<HardwareLiveSnapshot>();
        await using var enumerator = service.WatchLiveSnapshotsAsync(WatchInterval).GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        samples.Add(enumerator.Current);
        Assert.Empty(samples[0].MemoryTimingSensors);

        // Después del delay fake, LHM "agrega" el DIMM con timings.
        session.Hardware = session.Hardware.Append(CreateDimmHardware(sensors:
        [
            Timing("tAA (CAS Latency Time)", "/mem/dimm/0/timing/0", 14.0f)
        ])).ToList();

        Assert.True(await enumerator.MoveNextAsync());
        samples.Add(enumerator.Current);

        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(2, samples.Count);
        Assert.Empty(samples[0].MemoryTimingSensors);
        var timing = Assert.Single(samples[1].MemoryTimingSensors);
        Assert.Equal("tAA (CAS Latency Time)", timing.SensorName);
        Assert.Equal(14.0, timing.ValueNanoseconds);
    }

    [Fact]
    public async Task RefreshFailure_EmptiesTimingSensors()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };
        factory.Session!.ThrowOnRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 1);

        Assert.False(samples[0].IsAvailable);
        Assert.Empty(samples[0].MemoryTimingSensors);
    }

    [Fact]
    public async Task RefreshFailure_ThenSuccess_RecoversTiming()
    {
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };
        factory.Session!.ThrowOnFirstRefresh = true;
        var service = CreateService(factory, new FakeDelay());

        var samples = await TakeLiveWatchSamplesAsync(service, 2);

        Assert.False(samples[0].IsAvailable);
        Assert.True(samples[1].IsAvailable);
        Assert.Equal(2, samples[1].MemoryTimingSensors.Count);
    }

    [Fact]
    public async Task PartialDimmError_PreservesOtherFamilies()
    {
        var factory = new FakeFactory
        {
            Session = new FakeSession
            {
                Hardware =
                [
                    new FakeHardware
                    {
                        Name = "DDR4-3200 DIMM",
                        Identifier = "/mem/dimm/0",
                        HardwareType = InternalHardwareType.Memory,
                        ThrowOnSensorsRead = true
                    },
                    new FakeHardware
                    {
                        Name = "Intel Core i7-13700K",
                        Identifier = "/intelcpu/0",
                        HardwareType = InternalHardwareType.Cpu,
                        Sensors =
                        [
                            Temp("Package", "/intelcpu/0/temperature/0", 60.0f),
                            Load("CPU Total", "/intelcpu/0/load/0", 35.0f)
                        ]
                    }
                ]
            }
        };

        var live = await CaptureLive(factory);

        Assert.True(live.IsAvailable);
        Assert.NotEmpty(live.Errors);
        Assert.Single(live.TemperatureSensors);
        Assert.Single(live.PerformanceSensors);
        Assert.Empty(live.MemoryTimingSensors);
    }

    [Fact]
    public async Task OldTemperatureApis_StillWork_WithTimings()
    {
        var factory = new FakeFactory { Session = CreateFullDimmSession() };
        var service = CreateService(factory, new FakeDelay());

        var snapshot = await service.GetTemperatureSnapshotAsync();

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(3, snapshot.Sensors.Count);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(1, factory.Session!.RefreshCount);
    }

    [Fact]
    public void LiveSnapshot_NoClXmpExpoHealth()
    {
        var snapshotProperties = typeof(HardwareLiveSnapshot).GetProperties().Select(p => p.Name).ToList();
        var timingProperties = typeof(HardwareMemoryTimingSensor).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(snapshotProperties, n => n is "CL" or "XMP" or "EXPO" or "XmpProfile" or
            "ExpoProfile" or "HealthStatus" or "Severity" or "Recommendation" or "MemoryHealth");
        Assert.DoesNotContain(timingProperties, n => n is "CL" or "XMP" or "EXPO" or "XmpProfile" or
            "ExpoProfile" or "HealthStatus" or "Severity" or "Recommendation");
    }

    [Fact]
    public async Task Timing_UsesInjectedFactory_NoRealHardwareOrSmbusOrDriver()
    {
        // Si el servicio usara la fábrica real, no sería posible simular
        // timings sin acceso a SMBus/drivers reales.
        var factory = new FakeFactory { Session = CreateDimmSession(out _, out _) };

        var live = await CaptureLive(factory);

        Assert.Equal(1, factory.CreateCount);
        Assert.True(live.IsAvailable);
        Assert.Equal(2, live.MemoryTimingSensors.Count);
    }
}
