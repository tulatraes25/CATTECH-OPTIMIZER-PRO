using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

internal sealed class DumpCheckProcessResult
{
    public bool Started { get; init; }
    public bool TimedOut { get; init; }
    public bool Cancelled { get; init; }
    public bool OutputTruncated { get; init; }
    public int? ExitCode { get; init; }
    public string Stdout { get; init; } = string.Empty;
    public string Stderr { get; init; } = string.Empty;
}

internal interface IDumpCheckProcessRunner
{
    Task<DumpCheckProcessResult> RunAsync(
        string toolPath, string dumpPath, TimeSpan timeout, CancellationToken token);
}

/// <summary>
/// Ejecuta DumpChk con argumentos separados, sin shell ni símbolos remotos,
/// con stdout/stderr acotados y límite duro de tiempo.
/// </summary>
internal sealed class DumpCheckProcessRunner : IDumpCheckProcessRunner
{
    private const int MaxOutputChars = 256_000;

    internal static ProcessStartInfo BuildStartInfo(string toolPath, string dumpPath)
    {
        var start = new ProcessStartInfo
        {
            FileName = toolPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        start.ArgumentList.Add(dumpPath); // Sin command shell ni concatenación de parámetros.
        start.Environment["_NT_SYMBOL_PATH"] = string.Empty;
        start.Environment["_NT_ALT_SYMBOL_PATH"] = string.Empty;
        start.Environment["_NT_SOURCE_PATH"] = string.Empty;
        return start;
    }

    public async Task<DumpCheckProcessResult> RunAsync(
        string toolPath, string dumpPath, TimeSpan timeout, CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return new DumpCheckProcessResult { Cancelled = true };

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(timeout));

        using var proc = new Process
        {
            StartInfo = BuildStartInfo(toolPath, dumpPath)
        };

        try
        {
            if (!proc.Start())
                return new DumpCheckProcessResult();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new DumpCheckProcessResult();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);

        var output = CaptureLimitedAsync(proc.StandardOutput);
        var errors = CaptureLimitedAsync(proc.StandardError);

        try
        {
            await proc.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                // Terminó entre HasExited y Kill; no se reintenta.
            }

            // Consumir u observar ambas tareas de salida antes de disponer el
            // proceso; jamás dejarlas sin observar si el hijo fue detenido.
            try
            {
                await Task.WhenAll(output, errors)
                    .WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Un pipe puede cerrarse abruptamente al terminar el proceso.
            }

            return new DumpCheckProcessResult
            {
                Started = true,
                Cancelled = token.IsCancellationRequested,
                TimedOut = !token.IsCancellationRequested
            };
        }

        var outResult = await output.ConfigureAwait(false);
        var errResult = await errors.ConfigureAwait(false);

        return new DumpCheckProcessResult
        {
            Started = true,
            ExitCode = proc.ExitCode,
            Stdout = outResult.Content,
            Stderr = errResult.Content,
            OutputTruncated = outResult.Truncated || errResult.Truncated
        };
    }

    private static async Task<(string Content, bool Truncated)> CaptureLimitedAsync(StreamReader stream)
    {
        var buffer = new char[4096];
        var text = new StringBuilder();
        var truncated = false;
        int count;

        while ((count = await stream.ReadAsync(buffer.AsMemory()).ConfigureAwait(false)) > 0)
        {
            var remaining = MaxOutputChars - text.Length;
            if (remaining > 0)
                text.Append(buffer, 0, Math.Min(count, remaining));
            if (count > remaining)
                truncated = true;
        }

        return (text.ToString(), truncated);
    }
}
