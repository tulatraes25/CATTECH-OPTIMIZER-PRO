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
}
