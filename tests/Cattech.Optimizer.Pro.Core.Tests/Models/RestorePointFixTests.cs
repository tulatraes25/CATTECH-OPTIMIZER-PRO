using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;
using Cattech.Optimizer.Pro.Infrastructure.RestorePoint;
using Cattech.Optimizer.Pro.UI.ViewModels;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Regresiones del arreglo de la pantalla "Punto de restauración".
/// Cubre: detección real del sistema (sin la sonda obsoleta "srservice"), protección
/// como dato advisory que no bloquea, mapeo de errores reales de Windows y mensajería
/// visible en el ViewModel. Ningún test ejecuta comandos reales: todo pasa por fakes.
/// </summary>
public class RestorePointFixTests
{
    private const string CheckpointMarker = "Checkpoint-Computer";
    private const string StatusProbeMarker = "Get-Service -Name 'VSS'";
    private const string CountMarker = "Get-ComputerRestorePoint";

    // =========================================================================
    // Fakes (sin efectos reales en el sistema)
    // =========================================================================

    private sealed class FakeCommandRunner : IRestorePointCommandRunner
    {
        private readonly List<string> _scripts = new();

        public IReadOnlyList<string> Scripts => _scripts;

        public Func<string, RestorePointCommandResult> Handler { get; set; } =
            _ => Ok(string.Empty);

        public Exception? Failure { get; set; }

        public Task<RestorePointCommandResult> RunAsync(
            string script,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            _scripts.Add(script);

            if (Failure is not null)
            {
                return Task.FromException<RestorePointCommandResult>(Failure);
            }

            return Task.FromResult(Handler(script));
        }
    }

    private sealed class FakeRestorePointService : IRestorePointService
    {
        private readonly List<string> _createdNames = new();

        public IReadOnlyList<string> CreatedNames => _createdNames;
        public int CheckStatusCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int ListCalls { get; private set; }

        public Func<RestorePointStatus> StatusFactory { get; set; } = () => new();
        public Func<RestorePointResult> ResultFactory { get; set; } = () => new();
        public Exception? ListFailure { get; set; }
        public TaskCompletionSource<bool>? StatusGate { get; set; }
        public TaskCompletionSource<bool>? CreateGate { get; set; }

        public async Task<RestorePointStatus> CheckStatusAsync()
        {
            CheckStatusCalls++;

            if (StatusGate is not null)
            {
                await StatusGate.Task;
            }

            return StatusFactory();
        }

        public async Task<RestorePointResult> CreateRestorePointAsync(string name)
        {
            CreateCalls++;
            _createdNames.Add(name);

            if (CreateGate is not null)
            {
                await CreateGate.Task;
            }

            return ResultFactory();
        }

        public string GenerateRestorePointName() => "CATTECH - Prueba Generada";

        public Task<string> SaveResultAsync(RestorePointResult result)
        {
            SaveCalls++;
            return Task.FromResult("restore-point-result-test.json");
        }

        public Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20)
        {
            ListCalls++;

            return ListFailure is not null
                ? Task.FromException<List<RestorePointResultSummary>>(ListFailure)
                : Task.FromResult(new List<RestorePointResultSummary>());
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static RestorePointCommandResult Ok(string standardOutput = "") => new()
    {
        ExitCode = 0,
        StandardOutput = standardOutput
    };

    private static RestorePointCommandResult Failure(string standardError, int exitCode = 1) => new()
    {
        ExitCode = exitCode,
        StandardError = standardError
    };

    private static RestorePointCommandResult Timeout() => new()
    {
        ExitCode = -1,
        TimedOut = true
    };

    private static string ProbeJson(
        bool isAdmin = true,
        bool vss = true,
        bool swprv = true,
        bool systemRestoreClass = false,
        int restorePointCount = 0,
        bool disableSrPolicy = false)
    {
        return JsonSerializer.Serialize(new
        {
            IsAdmin = isAdmin,
            VssFound = vss,
            SwprvFound = swprv,
            SystemRestoreClassFound = systemRestoreClass,
            RestorePointCount = restorePointCount,
            DisableSrPolicy = disableSrPolicy
        });
    }

    private static FakeCommandRunner NewProbeRunner(string probeOutput)
    {
        return new FakeCommandRunner
        {
            Handler = _ => Ok(probeOutput)
        };
    }

    /// <summary>
    /// Runner que responde a la sonda de estado, al conteo de puntos (con valores
    /// sucesivos por llamada) y a Checkpoint-Computer.
    /// </summary>
    private static FakeCommandRunner NewSystemRunner(
        string probeOutput,
        RestorePointCommandResult checkpointResult,
        Func<int, string>? countOutputs = null)
    {
        var countExecutions = 0;

        return new FakeCommandRunner
        {
            Handler = script =>
            {
                if (script.Contains(CheckpointMarker, StringComparison.Ordinal))
                {
                    return checkpointResult;
                }

                if (script.Contains(StatusProbeMarker, StringComparison.Ordinal))
                {
                    return Ok(probeOutput);
                }

                if (script.Contains(CountMarker, StringComparison.Ordinal))
                {
                    var count = countOutputs is null ? "0" : countOutputs(countExecutions);
                    countExecutions++;
                    return Ok(count);
                }

                throw new InvalidOperationException("Comando inesperado en el test: " + script);
            }
        };
    }

    private static RestorePointService NewService(IRestorePointCommandRunner runner)
        => new(runner, null);

    private static TaskCompletionSource<bool> NewGate()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static RestorePointStatus AllowedStatus(params string[] warnings)
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = true,
            IsSystemRestoreAvailable = true,
            IsProtectionEnabled = false,
            AvailabilityState = RestoreAvailabilityState.LikelyAvailable,
            ProtectionState = RestoreProtectionState.Unknown,
            StatusMessage = "Se puede intentar crear un punto de restauración. " +
                            "La protección del sistema es informativa y Windows confirmará el resultado real.",
            LastCheckedAt = DateTime.Now
        };

        status.Warnings.AddRange(warnings);
        return status;
    }

    // =========================================================================
    // Modelo: la protección es advisory y no bloquea
    // =========================================================================

    [Fact]
    public void Status_ProteccionProbablementeDeshabilitada_NoBloqueaElIntento()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = true,
            IsSystemRestoreAvailable = true,
            IsProtectionEnabled = false,
            AvailabilityState = RestoreAvailabilityState.LikelyAvailable,
            ProtectionState = RestoreProtectionState.LikelyDisabled
        };

        // La lógica conservadora histórica se mantiene para no romper consumidores,
        // pero la puerta real de intento ya no depende de la protección.
        Assert.False(status.CanCreatePoint);
        Assert.True(status.CanAttemptCreate);
        Assert.Empty(status.AttemptBlockedReason);
    }

    [Fact]
    public void Status_DisponibilidadDesconocida_NoBloqueaElIntento()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = true,
            AvailabilityState = RestoreAvailabilityState.Unknown,
            ProtectionState = RestoreProtectionState.Unknown
        };

        Assert.True(status.CanAttemptCreate);
        Assert.Empty(status.AttemptBlockedReason);
    }

    [Fact]
    public void Status_BloqueaSoloSinAdministradorOSinSistemaDeRestauracion()
    {
        var status = new RestorePointStatus
        {
            IsAdministrator = false,
            AvailabilityState = RestoreAvailabilityState.LikelyAvailable
        };

        Assert.False(status.CanAttemptCreate);
        Assert.Contains("administrador", status.AttemptBlockedReason, StringComparison.OrdinalIgnoreCase);

        status.IsAdministrator = true;
        status.AvailabilityState = RestoreAvailabilityState.NotAvailable;

        Assert.False(status.CanAttemptCreate);
        Assert.Contains("VSS", status.AttemptBlockedReason, StringComparison.Ordinal);
    }

    // =========================================================================
    // Sonda de estado: VSS/swprv/WMI en lugar de "srservice"
    // =========================================================================

    [Fact]
    public async Task CheckStatus_ProteccionDesconocida_EsAdvisoryYNoBloquea()
    {
        var runner = NewProbeRunner(ProbeJson());
        var service = NewService(runner);

        var status = await service.CheckStatusAsync();

        Assert.True(status.IsAdministrator);
        Assert.Equal(RestoreAvailabilityState.LikelyAvailable, status.AvailabilityState);
        Assert.Equal(RestoreProtectionState.Unknown, status.ProtectionState);
        Assert.False(status.IsProtectionEnabled);
        Assert.True(status.CanAttemptCreate);
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("No se pudo confirmar la protección", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Se puede intentar", status.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckStatus_ProteccionDeshabilitadaPorPolitica_InformaPeroNoBloquea()
    {
        var service = NewService(NewProbeRunner(ProbeJson(disableSrPolicy: true)));

        var status = await service.CheckStatusAsync();

        Assert.Equal(RestoreProtectionState.LikelyDisabled, status.ProtectionState);
        Assert.False(status.IsProtectionEnabled);
        Assert.True(status.CanAttemptCreate);
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("parece deshabilitada", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("se intentará", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckStatus_PuntosExistentes_ConfirmanProteccionProbable()
    {
        var service = NewService(NewProbeRunner(ProbeJson(restorePointCount: 3)));

        var status = await service.CheckStatusAsync();

        Assert.Equal(RestoreProtectionState.LikelyEnabled, status.ProtectionState);
        Assert.True(status.IsProtectionEnabled);
        Assert.True(status.CanAttemptCreate);
        Assert.Empty(status.Warnings);
        Assert.Contains("listo", status.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckStatus_SinVssNiWmi_InformaNoDisponibleYSoloEntoncesBloquea()
    {
        var service = NewService(NewProbeRunner(ProbeJson(vss: false, swprv: false)));

        var status = await service.CheckStatusAsync();

        Assert.Equal(RestoreAvailabilityState.NotAvailable, status.AvailabilityState);
        Assert.False(status.IsSystemRestoreAvailable);
        Assert.False(status.CanAttemptCreate);
        Assert.Contains(status.Warnings, warning => warning.Contains("VSS", StringComparison.Ordinal));
        Assert.Contains("No se puede intentar", status.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckStatus_UsaDeteccionReal_YNoElServicioObsoletoSrservice()
    {
        var runner = NewProbeRunner(ProbeJson());
        var service = NewService(runner);

        await service.CheckStatusAsync();

        Assert.DoesNotContain(
            runner.Scripts,
            script => script.Contains("srservice", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            runner.Scripts,
            script => script.Contains(StatusProbeMarker, StringComparison.Ordinal));
        Assert.Contains(
            runner.Scripts,
            script => script.Contains("SystemRestore", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckStatus_SondaNoParseable_NoLanzaYNoBloqueaPorDisponibilidad()
    {
        var service = NewService(NewProbeRunner("salida inesperada sin json"));

        var status = await service.CheckStatusAsync();

        Assert.Equal(RestoreAvailabilityState.Unknown, status.AvailabilityState);
        Assert.Equal(RestoreProtectionState.Unknown, status.ProtectionState);
        Assert.Contains(
            status.Warnings,
            warning => warning.Contains("No se pudo consultar", StringComparison.OrdinalIgnoreCase));

        if (status.IsAdministrator)
        {
            Assert.True(status.CanAttemptCreate);
        }
        else
        {
            Assert.False(status.CanAttemptCreate);
        }
    }

    // =========================================================================
    // Creación: Checkpoint-Computer y mapeo real de errores
    // =========================================================================

    [Fact]
    public async Task CreateRestorePoint_ExitoVerificado_SoloReportaExitoReal()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Ok(),
            call => call == 0 ? "0" : "1");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.True(result.Success);
        Assert.True(result.Verified);
        Assert.Empty(result.ErrorMessage);
        Assert.Equal(RestorePointMethod.PowerShellCheckpoint, result.MethodUsed);
        Assert.True(result.ProtectionEnabled);
        Assert.Contains("verificado", result.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.FinishedAt >= result.RequestedAt);
    }

    [Fact]
    public async Task CreateRestorePoint_SinVerificacion_ReportaExitoDeWindowsConAdvertencia()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Ok(),
            _ => "1");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.True(result.Success);
        Assert.False(result.Verified);
        Assert.Contains("devolvió éxito", result.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Single(result.Warnings);
    }

    [Fact]
    public async Task CreateRestorePoint_NoEjecutaPreConsultaDeProteccion()
    {
        var runner = NewSystemRunner(
            ProbeJson(disableSrPolicy: true),
            Ok(),
            call => call == 0 ? "0" : "1");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.True(result.Success);
        // No se consulta la sonda de estado ni la política antes de crear: Windows es la fuente de verdad.
        Assert.DoesNotContain(
            runner.Scripts,
            script => script.Contains(StatusProbeMarker, StringComparison.Ordinal));
        Assert.DoesNotContain(
            runner.Scripts,
            script => script.Contains("DisableSR", StringComparison.Ordinal));
        Assert.Equal(1, runner.Scripts.Count(
            script => script.Contains(CheckpointMarker, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CreateRestorePoint_LimiteDeFrecuencia_MapeaCodigoExistente()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Failure("A new system restore point cannot be created because one has already been created " +
                    "within the past 24 hours."),
            _ => "2");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("FREQUENCY_LIMIT", result.ErrorCode);
        Assert.Contains("24 horas", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("24 hours", result.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRestorePoint_ProteccionDeshabilitada_SoloSiWindowsLoDevuelve()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Failure("System Restore is disabled. To enable System Restore, open System Protection."),
            _ => "0");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("PROTECTION_DISABLED", result.ErrorCode);
        Assert.Contains("protección", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.ProtectionEnabled);
    }

    [Fact]
    public async Task CreateRestorePoint_MensajeEspanolConAcentos_MapeaProteccionDeshabilitada()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Failure("La protección del sistema está deshabilitada para esta unidad."),
            _ => "0");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("PROTECTION_DISABLED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateRestorePoint_ErrorGenerico_NoInventaProteccionDeshabilitada()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Failure("The operation completed with an unexpected failure."),
            _ => "0");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("POWERSHELL_ERROR", result.ErrorCode);
        Assert.NotEqual("PROTECTION_DISABLED", result.ErrorCode);
        Assert.NotEqual("FREQUENCY_LIMIT", result.ErrorCode);
        Assert.Contains("no pudo crear", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRestorePoint_AccesoDenegado_MapeaCodigoExistente()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Failure("Access is denied. (Exception from HRESULT: 0x80070005 (E_ACCESSDENIED))"),
            _ => "0");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("ACCESS_DENIED", result.ErrorCode);
        Assert.True(result.RequiresAdministrator);
    }

    [Fact]
    public async Task CreateRestorePoint_EscapaComillasSimplesDelNombre()
    {
        var runner = NewSystemRunner(
            ProbeJson(),
            Ok(),
            call => call == 0 ? "0" : "1");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("Punto 'del técnico'");

        Assert.True(result.Success);
        Assert.Contains(
            runner.Scripts,
            script => script.Contains(CheckpointMarker, StringComparison.Ordinal) &&
                      script.Contains("''del técnico''", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateRestorePoint_NombreVacio_NoEjecutaComandos()
    {
        var runner = NewSystemRunner(ProbeJson(), Ok());
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("   ");

        Assert.False(result.Success);
        Assert.Equal("INVALID_NAME", result.ErrorCode);
        Assert.Empty(runner.Scripts);
    }

    [Fact]
    public async Task CreateRestorePoint_Timeout_ReportaErrorSinExito()
    {
        var runner = NewSystemRunner(ProbeJson(), Timeout(), _ => "0");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.Contains("tiempo máximo", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRestorePoint_RunnerFalla_NoPropagaExcepcionYReportaError()
    {
        var runner = NewSystemRunner(ProbeJson(), Ok(), _ => "0");
        runner.Failure = new InvalidOperationException("fallo simulado del runner");
        var service = NewService(runner);

        var result = await service.CreateRestorePointAsync("CATTECH - Prueba");

        Assert.False(result.Success);
        Assert.Equal("UNEXPECTED_ERROR", result.ErrorCode);
        Assert.Contains("fallo simulado", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorMapper_ClasificaMensajeEnStdoutYConMayusculas()
    {
        var failure = RestorePointErrorMapper.Classify(
            string.Empty,
            "A NEW SYSTEM RESTORE POINT CANNOT BE CREATED BECAUSE ONE HAS ALREADY BEEN CREATED.",
            1);

        Assert.Equal("FREQUENCY_LIMIT", failure.ErrorCode);
    }

    [Fact]
    public void ErrorMapper_MensajeDesconocido_DevuelveCodigoGenerico()
    {
        var failure = RestorePointErrorMapper.Classify("Algo raro pasó.", string.Empty, 5);

        Assert.Equal("POWERSHELL_ERROR", failure.ErrorCode);
        Assert.Contains("código de salida 5", failure.ErrorMessage, StringComparison.Ordinal);
    }

    // =========================================================================
    // ViewModel: mensajería visible y guardas de reentrancia
    // =========================================================================

    [Fact]
    public async Task ViewModel_CheckStatus_ProteccionDesconocida_NoBloqueaYMuestraAdvertencias()
    {
        var fake = new FakeRestorePointService
        {
            StatusFactory = () => AllowedStatus("No se pudo confirmar la protección del sistema.")
        };
        var viewModel = new RestorePointViewModel(fake);

        await viewModel.CheckStatusCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasStatus);
        Assert.True(viewModel.CanCreatePoint);
        Assert.True(viewModel.CreateButtonEnabled);
        Assert.Equal("Desconocida (no bloquea)", viewModel.ProtectionText);
        Assert.True(viewModel.HasWarnings);
        Assert.Single(viewModel.WarningMessages);
        Assert.True(viewModel.HasInfoMessage);
        Assert.False(viewModel.IsSuccess);
        Assert.False(viewModel.HasError);
        Assert.Equal("Estado verificado", viewModel.StatusText);
    }

    [Fact]
    public async Task ViewModel_CheckStatus_SinAdministrador_MuestraErrorVisibleYNoHabilitaCreacion()
    {
        var fake = new FakeRestorePointService
        {
            StatusFactory = () =>
            {
                var status = AllowedStatus();
                status.IsAdministrator = false;
                return status;
            }
        };
        var viewModel = new RestorePointViewModel(fake);

        await viewModel.CheckStatusCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasStatus);
        Assert.False(viewModel.CanCreatePoint);
        Assert.False(viewModel.CreateButtonEnabled);
        Assert.True(viewModel.HasError);
        Assert.Contains("administrador", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsSuccess);
        Assert.False(viewModel.HasInfoMessage);
    }

    [Fact]
    public async Task ViewModel_Crear_AntesDeVerificar_NoLlamaAlServicioYMuestraError()
    {
        var fake = new FakeRestorePointService();
        var viewModel = new RestorePointViewModel(fake);

        await viewModel.CreateRestorePointCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.CreateCalls);
        Assert.True(viewModel.HasError);
        Assert.Contains("verificá", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ViewModel_Crear_Fallo_MuestraErrorMessageYAdvertencias()
    {
        var fake = new FakeRestorePointService
        {
            StatusFactory = () => AllowedStatus()
        };
        var viewModel = new RestorePointViewModel(fake);
        await viewModel.CheckStatusCommand.ExecuteAsync(null);
        Assert.True(viewModel.CanCreatePoint);

        fake.ResultFactory = () => new RestorePointResult
        {
            Success = false,
            ErrorCode = "PROTECTION_DISABLED",
            ErrorMessage = "Windows informó que la protección del sistema está deshabilitada.",
            Warnings = { "Revisá la protección de la unidad del sistema." },
            FinishedAt = DateTime.Now
        };

        await viewModel.CreateRestorePointCommand.ExecuteAsync(null);

        Assert.Equal(1, fake.CreateCalls);
        Assert.Equal(1, fake.SaveCalls);
        Assert.True(viewModel.HasError);
        Assert.Contains("protección", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PROTECTION_DISABLED", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsSuccess);
        Assert.False(viewModel.ResultSuccess);
        Assert.True(viewModel.HasResult);
        Assert.True(viewModel.HasWarnings);
        Assert.Single(viewModel.WarningMessages);
        Assert.Contains("No se pudo crear", viewModel.ResultSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ViewModel_Crear_Exito_MuestraExitoRealYDetalleDelMetodo()
    {
        var fake = new FakeRestorePointService
        {
            StatusFactory = () => AllowedStatus(),
            ResultFactory = () => new RestorePointResult
            {
                Success = true,
                Verified = true,
                Notes = "Punto de restauración creado y verificado: se detectó un punto nuevo.",
                MethodUsed = RestorePointMethod.PowerShellCheckpoint,
                FinishedAt = DateTime.Now
            }
        };
        var viewModel = new RestorePointViewModel(fake);
        await viewModel.CheckStatusCommand.ExecuteAsync(null);

        await viewModel.CreateRestorePointCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsSuccess);
        Assert.False(viewModel.HasError);
        Assert.False(viewModel.HasWarnings);
        Assert.Contains("verificado", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.ResultSuccess);
        Assert.Contains("Checkpoint-Computer", viewModel.ResultDetails, StringComparison.Ordinal);
        Assert.Equal(1, fake.SaveCalls);
        Assert.Equal("CATTECH - Prueba Generada", viewModel.RestorePointName);
    }

    [Fact]
    public async Task ViewModel_Crear_NombreVacio_MuestraErrorSinLlamarAlServicio()
    {
        var fake = new FakeRestorePointService
        {
            StatusFactory = () => AllowedStatus()
        };
        var viewModel = new RestorePointViewModel(fake);
        await viewModel.CheckStatusCommand.ExecuteAsync(null);
        viewModel.RestorePointName = "   ";

        await viewModel.CreateRestorePointCommand.ExecuteAsync(null);

        Assert.Equal(0, fake.CreateCalls);
        Assert.True(viewModel.HasError);
        Assert.Contains("nombre", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ViewModel_VerificarEstado_IgnoraDobleEjecucion()
    {
        var gate = NewGate();
        var fake = new FakeRestorePointService
        {
            StatusGate = gate,
            StatusFactory = () => AllowedStatus()
        };
        var viewModel = new RestorePointViewModel(fake);

        var firstExecution = viewModel.CheckStatusCommand.ExecuteAsync(null);
        var secondExecution = viewModel.CheckStatusCommand.ExecuteAsync(null);
        await secondExecution;

        Assert.Equal(1, fake.CheckStatusCalls);
        Assert.True(viewModel.IsRunning);

        gate.SetResult(true);
        await firstExecution;

        Assert.Equal(1, fake.CheckStatusCalls);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task ViewModel_Crear_IgnoraDobleEjecucion()
    {
        var fake = new FakeRestorePointService
        {
            StatusFactory = () => AllowedStatus()
        };
        var viewModel = new RestorePointViewModel(fake);
        await viewModel.CheckStatusCommand.ExecuteAsync(null);

        var gate = NewGate();
        fake.CreateGate = gate;
        fake.ResultFactory = () => new RestorePointResult
        {
            Success = true,
            Notes = "Punto de restauración creado.",
            FinishedAt = DateTime.Now
        };

        var firstExecution = viewModel.CreateRestorePointCommand.ExecuteAsync(null);
        var secondExecution = viewModel.CreateRestorePointCommand.ExecuteAsync(null);
        await secondExecution;

        Assert.Equal(1, fake.CreateCalls);

        gate.SetResult(true);
        await firstExecution;

        Assert.Equal(1, fake.CreateCalls);
        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task ViewModel_Historial_ErrorVisible()
    {
        var fake = new FakeRestorePointService
        {
            ListFailure = new InvalidOperationException("no se pudo leer el historial")
        };
        var viewModel = new RestorePointViewModel(fake);

        await viewModel.LoadHistoryCommand.ExecuteAsync(null);

        Assert.True(viewModel.ShowHistory);
        Assert.True(viewModel.HasError);
        Assert.Contains("historial", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsRunning);
    }
}
