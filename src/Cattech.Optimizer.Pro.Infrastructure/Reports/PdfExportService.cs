using System.Diagnostics;
using System.IO;
using System.Text;
using Cattech.Optimizer.Pro.Core.Interfaces;

namespace Cattech.Optimizer.Pro.Infrastructure.Reports;

/// <summary>
/// Implementación de IPdfExportService.
/// Genera PDFs reales usando Microsoft Edge en modo headless (--print-to-pdf).
/// Requiere Microsoft Edge instalado (pre-instalado en Windows 10/11).
/// </summary>
public class PdfExportService : IPdfExportService
{
    private readonly string _baseDirectory;
    private readonly string _pdfDirectory;

    public PdfExportService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _pdfDirectory = Path.Combine(_baseDirectory, "reports", "pdf");
    }

    /// <inheritdoc/>
    public Task<PdfExporterInfo> CanExportAsync()
    {
        var info = new PdfExporterInfo
        {
            Name = "Microsoft Edge PDF Export",
            Version = "Edge Headless (--print-to-pdf)"
        };

        try
        {
            var edgePath = FindEdgeExecutablePath();
            info.IsAvailable = edgePath is not null;
            info.StatusMessage = info.IsAvailable
                ? "Microsoft Edge detectado. Exportación PDF disponible."
                : "Microsoft Edge no detectado. Se requiere Edge para exportar PDF.";
        }
        catch (Exception)
        {
            info.IsAvailable = false;
            info.StatusMessage = "No se pudo verificar la instalación de Microsoft Edge.";
        }

        return Task.FromResult(info);
    }

    /// <inheritdoc/>
    public async Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);

        var fullHtmlPath = Path.GetFullPath(htmlPath);
        if (!File.Exists(fullHtmlPath))
            throw new FileNotFoundException("No se encontró el archivo HTML solicitado.", fullHtmlPath);

        var fullOutputPath = Path.GetFullPath(outputPdfPath);
        var outputDir = Path.GetDirectoryName(fullOutputPath)
            ?? throw new ArgumentException("Ruta de destino PDF inválida.", nameof(outputPdfPath));
        Directory.CreateDirectory(outputDir);

        // Only use Edge from a known installation directory. Never launch a
        // same-named executable found in the current directory or PATH.
        var edgePath = FindEdgeExecutablePath();
        if (edgePath is null)
            throw new InvalidOperationException(
                "Microsoft Edge no está instalado en una ubicación conocida. " +
                "Se requiere Edge para exportar PDF; utilice el informe HTML.");

        var token = Guid.NewGuid().ToString("N");
        var stagedPdfPath = Path.Combine(outputDir, $".cattech-pdf-{token}.pdf");
        try
        {
            if (!await ExportViaEdgeHeadlessAsync(
                fullHtmlPath, stagedPdfPath, edgePath).ConfigureAwait(false))
                return false;

            // Atomic same-directory publication, never overwrite an earlier PDF.
            PublishPdfWithoutOverwrite(stagedPdfPath, fullOutputPath);
            return true;
        }
        finally
        {
            try
            {
                if (File.Exists(stagedPdfPath))
                    File.Delete(stagedPdfPath);
            }
            catch (IOException) { /* Preserve the original failure. */ }
            catch (UnauthorizedAccessException) { /* Preserve the original failure. */ }
        }
    }

    /// <summary>
    /// Verifica si Microsoft Edge está disponible de forma síncrona.
    /// Busca únicamente en directorios de instalación conocidos.
    /// </summary>
    public static bool IsEdgeAvailable()
        => FindEdgeExecutablePath() is not null;

    /// <inheritdoc/>
    public string GetPdfOutputPath(string htmlPath)
    {
        var directory = Path.GetDirectoryName(htmlPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(htmlPath);
        var pdfDirectory = Path.Combine(directory, "..", "pdf");

        return Path.Combine(pdfDirectory, $"{fileNameWithoutExt}.pdf");
    }

    /// <inheritdoc/>
    public Task OpenPdfAsync(string pdfPath)
    {
        if (File.Exists(pdfPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = pdfPath,
                UseShellExecute = true
            });
        }
        return Task.CompletedTask;
    }

    // --- Métodos internos ---

    /// <summary>
    /// Build isolated Edge arguments without shell quoting or disabling sandbox.
    /// The file URI is a single argument, even when the path contains spaces.
    /// </summary>
    internal static ProcessStartInfo BuildEdgeStartInfo(
        string edgePath, string htmlPath, string stagedPdfPath)
    {
        var start = new ProcessStartInfo
        {
            FileName = edgePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false
        };
        start.ArgumentList.Add("--headless");
        start.ArgumentList.Add("--disable-gpu");
        start.ArgumentList.Add("--print-to-pdf=" + stagedPdfPath);
        start.ArgumentList.Add("--no-pdf-header-footer");
        start.ArgumentList.Add(new Uri(Path.GetFullPath(htmlPath)).AbsoluteUri);
        return start;
    }

    internal static void PublishPdfWithoutOverwrite(string stagedPdfPath, string outputPdfPath)
        => File.Move(stagedPdfPath, outputPdfPath);

    private static async Task<bool> ExportViaEdgeHeadlessAsync(
        string htmlPath, string stagedPdfPath, string edgePath)
    {
        using var process = new Process
        {
            StartInfo = BuildEdgeStartInfo(edgePath, htmlPath, stagedPdfPath)
        };

        if (!process.Start())
            return false;

        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30))
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException
                or System.ComponentModel.Win32Exception or TimeoutException)
            {
                // Keep the original timeout while best-effort reaping the child.
            }

            throw new TimeoutException("La exportación a PDF tomó demasiado tiempo (30s).");
        }

        if (process.ExitCode != 0 || !File.Exists(stagedPdfPath))
            return false;

        return await ValidatePdfFileAsync(stagedPdfPath).ConfigureAwait(false);
    }

    /// <summary>
    /// Basic PDF envelope/truncation guard (header + terminal EOF marker).
    /// This does NOT replace a full structural PDF parser or visual Edge QA.
    /// </summary>
    public static async Task<bool> ValidatePdfFileAsync(string pdfPath)
    {
        try
        {
            await using var stream = new FileStream(pdfPath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 8192, useAsync: true);
            if (stream.Length < 12)
                return false;

            var header = new byte[5];
            await stream.ReadExactlyAsync(header.AsMemory()).ConfigureAwait(false);
            if (Encoding.ASCII.GetString(header) != "%PDF-")
                return false;

            // A truncated export can retain its PDF header; require a final
            // %%EOF marker near the end, with only whitespace afterwards.
            var tailSize = (int)Math.Min(4096L, stream.Length);
            stream.Seek(-tailSize, SeekOrigin.End);
            var tail = new byte[tailSize];
            await stream.ReadExactlyAsync(tail.AsMemory()).ConfigureAwait(false);
            var trailer = Encoding.ASCII.GetString(tail);
            var end = trailer.LastIndexOf("%%EOF", StringComparison.Ordinal);
            return end >= 0 && trailer.AsSpan(end + 5).Trim().IsEmpty;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindEdgeExecutablePath()
    {
        // Do not execute where.exe and do not trust executable paths supplied by
        // the caller's PATH or current directory.
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
                continue;

            var candidate = Path.Combine(root, "Microsoft", "Edge",
                "Application", "msedge.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

}
