using System.Security;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.Diagnostics;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Verifica un dump solo tras confirmación explícita. Requiere DumpChk preinstalado
/// en Windows SDK y nunca activa descarga o elevación automáticamente.
/// </summary>
public sealed class DumpCheckService : IDumpCheckService
{
    private readonly DumpCheckToolLocator _locator;
    private readonly IDumpCheckProcessRunner _runner;
    private readonly string _dumpDirectory;
    private readonly Func<string, bool> _exists;
    private readonly Func<string, FileAttributes> _attributes;
    private readonly bool _isWindows;

    public DumpCheckService()
        : this(
            new DumpCheckToolLocator(), new DumpCheckProcessRunner(),
            GetWindowsDumpDirectory(), File.Exists, File.GetAttributes, OperatingSystem.IsWindows())
    {
    }

    internal DumpCheckService(
        DumpCheckToolLocator locator,
        IDumpCheckProcessRunner runner,
        string dumpDirectory,
        Func<string, bool> exists,
        Func<string, FileAttributes> attributes,
        bool isWindows)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _dumpDirectory = dumpDirectory ?? string.Empty;
        _exists = exists ?? throw new ArgumentNullException(nameof(exists));
        _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        _isWindows = isWindows;
    }

    public async Task<DumpCheckExecutionResult> VerifyDumpAsync(
        string dumpFileName, bool technicianConfirmed, CancellationToken cancellationToken = default)
    {
        if (!technicianConfirmed)
            return Result(DumpCheckExecutionStatus.ConfirmationRequired,
                "El técnico debe confirmar expresamente antes de ejecutar DumpChk.");

        if (cancellationToken.IsCancellationRequested)
            return Result(DumpCheckExecutionStatus.Cancelled, "La consulta fue cancelada.");

        if (!_isWindows || string.IsNullOrWhiteSpace(_dumpDirectory))
            return Result(DumpCheckExecutionStatus.ToolUnavailable, "Requiere Windows y la carpeta Minidump.");

        if (!DumpCheckPathPolicy.TryResolve(_dumpDirectory, dumpFileName, _ => true, out var dumpPath))
            return Result(DumpCheckExecutionStatus.InvalidFileName, "Nombre de dump no permitido.");

        if (!_exists(dumpPath))
            return Result(DumpCheckExecutionStatus.DumpMissing, "No se encontró ese archivo en Minidump.");

        try
        {
            if ((_attributes(dumpPath) & FileAttributes.ReparsePoint) != 0)
                return Result(DumpCheckExecutionStatus.InvalidFileName,
                    "No se permiten enlaces o reparse points.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Result(DumpCheckExecutionStatus.AccessDenied, "No se pudo comprobar el archivo.");
        }

        var toolPath = _locator.Locate();
        if (toolPath == null)
            return Result(DumpCheckExecutionStatus.ToolUnavailable,
                "DumpChk no está instalado en una ubicación conocida del Windows SDK.");

        DumpCheckProcessResult output;
        try
        {
            output = await _runner.RunAsync(
                toolPath, dumpPath, TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Result(DumpCheckExecutionStatus.Cancelled, "La consulta fue cancelada.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Result(DumpCheckExecutionStatus.Failed, "No pudo iniciarse o completarse DumpChk.");
        }

        if (output.Cancelled)
            return Result(DumpCheckExecutionStatus.Cancelled, "La consulta fue cancelada.");
        if (output.TimedOut)
            return Result(DumpCheckExecutionStatus.TimedOut, "DumpChk excedió el tiempo máximo permitido.");
        if (!output.Started || output.OutputTruncated || output.ExitCode != 0)
            return Result(DumpCheckExecutionStatus.Failed,
                "DumpChk no finalizó correctamente o la salida fue incompleta.");

        var summary = DumpCheckOutputParser.Parse(output.Stdout);
        return new DumpCheckExecutionResult
        {
            Status = summary.Readability == DumpCheckReadability.Indeterminate
                ? DumpCheckExecutionStatus.Failed
                : DumpCheckExecutionStatus.Completed,
            Message = summary.Message,
            ExitCode = output.ExitCode,
            Summary = summary
        };
    }

    private static string GetWindowsDumpDirectory()
    {
        if (!OperatingSystem.IsWindows())
            return string.Empty;

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return string.IsNullOrWhiteSpace(windows)
            ? string.Empty : Path.Combine(windows, "Minidump");
    }

    private static DumpCheckExecutionResult Result(DumpCheckExecutionStatus status, string message)
        => new() { Status = status, Message = message };
}
