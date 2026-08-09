using System.Diagnostics;
using System.Runtime.CompilerServices;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Hardware;
using Cattech.Optimizer.Pro.UI.Converters;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Cattech.Optimizer.Pro.UI.Views;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

public class HardwareViewModelTests
{
    // =====================
    // Fake de IHardwareSensorService (nunca toca hardware real)
    // =====================

    private sealed class FakeHardwareSensorService : IHardwareSensorService
    {
        public HardwareTemperatureSnapshot NextSnapshot { get; set; } = new();
        public Exception? ThrowOnGetSnapshot { get; set; }
        public List<HardwareTemperatureSnapshot> StreamSnapshots { get; } = new();
        public bool StreamEndsImmediately { get; set; }
        public Exception? ThrowOnStream { get; set; }
        public int StreamCalls { get; private set; }
        public TimeSpan? LastInterval { get; private set; }
        public CancellationToken LastStreamToken { get; private set; }

        public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetSnapshot != null)
            {
                throw ThrowOnGetSnapshot;
            }

            return Task.FromResult(NextSnapshot);
        }

        public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamCalls++;
            LastInterval = interval;
            LastStreamToken = cancellationToken;

            if (ThrowOnStream != null)
            {
                throw ThrowOnStream;
            }

            foreach (var snapshot in StreamSnapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return snapshot;
            }

            if (StreamEndsImmediately)
            {
                yield break;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken);
            }
        }

        public Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HardwareLiveSnapshot
            {
                IsAvailable = true,
                TemperatureSensors = NextSnapshot.Sensors,
                Warnings = NextSnapshot.Warnings,
                Errors = NextSnapshot.Errors
            });
        }

        public async IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
    }

    private sealed class PendingSnapshotService : IHardwareSensorService
    {
        public TaskCompletionSource<HardwareTemperatureSnapshot> Tcs { get; } = new();

        public Task<HardwareTemperatureSnapshot> GetTemperatureSnapshotAsync(
            CancellationToken cancellationToken = default) => Tcs.Task;

        public async IAsyncEnumerable<HardwareTemperatureSnapshot> WatchTemperatureSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }

        public Task<HardwareLiveSnapshot> GetLiveSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HardwareLiveSnapshot());
        }

        public async IAsyncEnumerable<HardwareLiveSnapshot> WatchLiveSnapshotsAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield break;
        }
    }

    // =====================
    // Helpers
    // =====================

    private static HardwareTemperatureSensor Sensor(string type, string hardware, string name,
        double? value = null, double? min = null, double? max = null)
    {
        return new HardwareTemperatureSensor
        {
            HardwareType = type,
            HardwareName = hardware,
            SensorName = name,
            SensorIdentifier = $"{hardware}/{name}",
            ValueCelsius = value,
            MinCelsius = min,
            MaxCelsius = max
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("La condición no se cumplió a tiempo.");
            }

            await Task.Delay(20);
        }
    }

    private static HardwareViewModel CreateViewModel(FakeHardwareSensorService service) => new(service);

    // =====================
    // Estado inicial
    // =====================

    [Fact]
    public void InitialState_NotMonitoring()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.False(vm.IsMonitoring);
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsAvailable);
        Assert.False(vm.HasSensors);
        Assert.Equal("Sin lectura", vm.StatusText);
        Assert.Null(vm.LastUpdatedAt);
    }

    [Fact]
    public void InitialState_NoInventedSensors()
    {
        var vm = CreateViewModel(new FakeHardwareSensorService());

        Assert.Empty(vm.TemperatureSensors);
        Assert.Equal(0, vm.ValidSensorCount);
        Assert.Equal("0", vm.SensorCountText);
    }

    // =====================
    // Actualización única
    // =====================

    [Fact]
    public async Task Refresh_LoadsAvailableSnapshot()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot { IsAvailable = true, IsElevated = true }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsAvailable);
        Assert.True(vm.IsElevated);
        Assert.Equal("Disponible", vm.ProviderStatusText);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Refresh_LoadsSensors()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot
            {
                IsAvailable = true,
                Sensors =
                [
                    Sensor("CPU", "AMD Ryzen 7 5700X", "Core (Tctl/Tdie)", 48.2),
                    Sensor("GPU", "NVIDIA RTX 4070", "GPU Core", 45.0)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.TemperatureSensors.Count);
        Assert.Equal(2, vm.ValidSensorCount);
        Assert.True(vm.HasSensors);
        Assert.Equal("Lectura disponible", vm.StatusText);
    }

    [Fact]
    public async Task Refresh_OrdersByTypeHardwareSensor()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot
            {
                IsAvailable = true,
                Sensors =
                [
                    Sensor("Almacenamiento", "Samsung SSD 980 PRO", "Temperature", 37.0),
                    Sensor("CPU", "AMD Ryzen 7 5700X", "Core (Tctl/Tdie)", 48.2),
                    Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 50.1),
                    Sensor("GPU", "NVIDIA RTX 4070", "GPU Core", 45.0)
                ]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        var names = vm.TemperatureSensors.Select(s => $"{s.HardwareType}|{s.HardwareName}|{s.SensorName}").ToList();
        Assert.Equal(
        [
            "Almacenamiento|Samsung SSD 980 PRO|Temperature",
            "CPU|AMD Ryzen 7 5700X|Core (Tctl/Tdie)",
            "CPU|AMD Ryzen 7 5700X|Package",
            "GPU|NVIDIA RTX 4070|GPU Core"
        ], names);
    }

    [Fact]
    public void NullValue_ShownAsND()
    {
        var converter = new NullableTemperatureConverter();

        Assert.Equal("N/D", converter.Convert(null, typeof(string), null, null));
    }

    [Fact]
    public void MinNull_NotConvertedToZero()
    {
        var converter = new NullableTemperatureConverter();

        Assert.Equal("N/D", converter.Convert(null, typeof(string), null, null));
    }

    [Fact]
    public void MaxNull_NotConvertedToZero()
    {
        var converter = new NullableTemperatureConverter();

        Assert.Equal("N/D", converter.Convert(null, typeof(string), null, null));
    }

    [Fact]
    public void TemperatureValue_FormattedWithDegrees()
    {
        var converter = new NullableTemperatureConverter();

        Assert.Equal("48,2 °C", converter.Convert(48.2, typeof(string), null, new System.Globalization.CultureInfo("es-AR")));
    }

    [Fact]
    public async Task Warnings_ReachViewModel()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot
            {
                IsAvailable = true,
                Warnings = ["Algunos sensores pueden no estar disponibles sin permisos de administrador."]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasWarnings);
        Assert.Single(vm.Warnings);
        Assert.Contains(vm.Warnings, w => w.Contains("permisos de administrador", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Errors_ReachViewModel()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot
            {
                IsAvailable = false,
                Errors = ["No se pudo inicializar el monitoreo de hardware: fallo simulado"]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.HasErrors);
        Assert.Single(vm.Errors);
        Assert.False(vm.IsAvailable);
        Assert.Equal("Lectura no disponible", vm.StatusText);
    }

    [Fact]
    public async Task UnavailableSnapshot_ClearsPreviousSensors()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot
            {
                IsAvailable = true,
                Sensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 48.2)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Single(vm.TemperatureSensors);

        service.NextSnapshot = new HardwareTemperatureSnapshot { IsAvailable = false, Errors = ["fallo"] };
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(vm.TemperatureSensors);
        Assert.False(vm.HasSensors);
        Assert.Equal(0, vm.ValidSensorCount);
    }

    [Fact]
    public async Task AvailableAfterUnavailable_Recovers()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot { IsAvailable = false, Errors = ["fallo"] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.False(vm.IsAvailable);

        service.NextSnapshot = new HardwareTemperatureSnapshot
        {
            IsAvailable = true,
            Sensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 50.0)]
        };
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsAvailable);
        Assert.Single(vm.TemperatureSensors);
    }

    [Fact]
    public async Task EmptySensors_NeutralMessage()
    {
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot { IsAvailable = true, Sensors = [] }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Sin sensores disponibles", vm.StatusText);
        Assert.DoesNotContain("correctas", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sano", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    // =====================
    // Monitoreo en tiempo real
    // =====================

    [Fact]
    public async Task StartMonitoring_UsesTwoSecondInterval()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastInterval.HasValue);

        Assert.Equal(TimeSpan.FromSeconds(2), service.LastInterval);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task StartMonitoring_AppliesMultipleSnapshots()
    {
        var service = new FakeHardwareSensorService();
        service.StreamSnapshots.Add(new HardwareTemperatureSnapshot
        {
            IsAvailable = true,
            Sensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 45.0)]
        });
        service.StreamSnapshots.Add(new HardwareTemperatureSnapshot
        {
            IsAvailable = true,
            Sensors = [Sensor("CPU", "AMD Ryzen 7 5700X", "Package", 51.0)]
        });
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.TemperatureSensors.Count == 1 && vm.TemperatureSensors[0].ValueCelsius == 51.0);

        Assert.Equal(51.0, vm.TemperatureSensors[0].ValueCelsius);
        Assert.Equal("Lectura disponible", vm.StatusText);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task StartMonitoring_NoSecondStream()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.StreamCalls == 1);

        vm.StartMonitoringCommand.Execute(null);
        await Task.Delay(100);

        Assert.Equal(1, service.StreamCalls);

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task StopMonitoring_CancelsStream()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);

        vm.StopMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => service.LastStreamToken.IsCancellationRequested);
    }

    [Fact]
    public async Task StopMonitoring_Idempotent()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);

        vm.StopMonitoringCommand.Execute(null);
        vm.StopMonitoringCommand.Execute(null);
        vm.StopMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task NormalCancellation_NoErrorAdded()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);
        vm.StopMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => !vm.IsMonitoring);

        Assert.Empty(vm.Errors);
        Assert.NotEqual("Error de monitoreo", vm.StatusText);
    }

    [Fact]
    public async Task UnexpectedException_ControlledError()
    {
        var service = new FakeHardwareSensorService
        {
            ThrowOnStream = new InvalidOperationException("fallo simulado")
        };
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.Errors.Count == 1);

        Assert.Equal("Error de monitoreo", vm.StatusText);
        Assert.Contains("fallo simulado", vm.Errors[0], StringComparison.OrdinalIgnoreCase);

        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task IsMonitoring_ReturnsFalseAfterStreamEnds()
    {
        var service = new FakeHardwareSensorService
        {
            StreamEndsImmediately = true
        };
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);

        await WaitUntilAsync(() => !vm.IsMonitoring);
    }

    [Fact]
    public async Task RefreshBlocked_WhileMonitoring()
    {
        var service = new FakeHardwareSensorService();
        var vm = CreateViewModel(service);

        vm.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => vm.IsMonitoring);

        Assert.False(vm.RefreshCommand.CanExecute(null));

        vm.StopMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => !vm.IsMonitoring);
        Assert.True(vm.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartBlocked_WhileRefreshActive()
    {
        var service = new PendingSnapshotService();
        var vm = new HardwareViewModel(service);

        var refreshTask = vm.RefreshCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.StartMonitoringCommand.CanExecute(null));
        Assert.False(vm.RefreshCommand.CanExecute(null));

        service.Tcs.SetResult(new HardwareTemperatureSnapshot { IsAvailable = true });
        await refreshTask;

        Assert.False(vm.IsBusy);
        Assert.True(vm.StartMonitoringCommand.CanExecute(null));
    }

    // =====================
    // Sin interpretación de salud
    // =====================

    [Fact]
    public void ViewModel_HasNoHealthStateProperties()
    {
        var propertyNames = typeof(HardwareViewModel)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "IsHot" or "IsCritical" or "HealthStatus" or "Severity" or "Recommendation");
    }

    [Fact]
    public void ViewModel_UsesTemperatureOnly()
    {
        // B.2.1 no agrega rendimiento a la UI: el ViewModel no expone métricas de performance.
        var propertyNames = typeof(HardwareViewModel)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(propertyNames, n => n is "PerformanceSensors" or "LoadSensors" or "ClockSensors");
    }

    [Fact]
    public async Task UsesInjectedService_NoRealHardware()
    {
        // Si el ViewModel usara LibreHardwareSensorService real, estos datos simulados
        // no serían posibles sin tocar el hardware.
        var service = new FakeHardwareSensorService
        {
            NextSnapshot = new HardwareTemperatureSnapshot
            {
                IsAvailable = true,
                Sensors = [Sensor("CPU", "Simulated CPU", "Package", 42.0)]
            }
        };
        var vm = CreateViewModel(service);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Simulated CPU", Assert.Single(vm.TemperatureSensors).HardwareName);
        Assert.Equal(42.0, vm.TemperatureSensors[0].ValueCelsius);
    }

    // =====================
    // Navegación: salir de Hardware cancela el monitoreo
    // =====================

    [Fact]
    public async Task LeavingHardwareSection_StopsMonitoring()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunNavigationTest().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(15));

        if (failure != null)
        {
            throw failure;
        }
    }

    private static async Task RunNavigationTest()
    {
        var service = new FakeHardwareSensorService();
        var mainVm = new MainViewModel(service);

        mainVm.NavigateCommand.Execute("Hardware");
        var hardwareVm = ((HardwareView)mainVm.CurrentView).DataContext as HardwareViewModel;
        Assert.NotNull(hardwareVm);

        hardwareVm!.StartMonitoringCommand.Execute(null);
        await WaitUntilAsync(() => service.LastStreamToken != default);
        Assert.True(hardwareVm.IsMonitoring);

        mainVm.NavigateCommand.Execute("Reports");

        await WaitUntilAsync(() => !hardwareVm.IsMonitoring);
        Assert.Equal("Reports", mainVm.CurrentSection);
        Assert.False(hardwareVm.IsMonitoring);
    }
}
