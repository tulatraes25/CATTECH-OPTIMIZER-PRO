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
    public async Task<PdfExporterInfo> CanExportAsync()
    {
        var info = new PdfExporterInfo
        {
            Name = "Microsoft Edge PDF Export",
            Version = "Edge Headless (--print-to-pdf)"
        };

        try
        {
            var edgePath = await GetEdgeExecutablePathWithWhereAsync();

            if (!string.IsNullOrEmpty(edgePath) && File.Exists(edgePath))
            {
                info.IsAvailable = true;
                info.StatusMessage = "Microsoft Edge detectado. Exportación PDF disponible.";
            }
            else
            {
                info.IsAvailable = false;
                info.StatusMessage = "Microsoft Edge no detectado. Se requiere Edge para exportar PDF.";
            }
        }
        catch (Exception ex)
        {
            info.IsAvailable = false;
            info.StatusMessage = $"Error al verificar: {ex.Message}";
        }

        return info;
    }

    /// <inheritdoc/>
    public async Task<bool> ExportHtmlToPdfAsync(string htmlPath, string outputPdfPath)
    {
        if (!File.Exists(htmlPath))
            throw new FileNotFoundException($"No se encontró el archivo HTML: {htmlPath}");

        // Crear directorio de salida
        var outputDir = Path.GetDirectoryName(outputPdfPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var edgePath = await GetEdgeExecutablePathWithWhereAsync();

        if (string.IsNullOrEmpty(edgePath) || !File.Exists(edgePath))
        {
            throw new InvalidOperationException(
                "Microsoft Edge no está instalado. " +
                "Se requiere Microsoft Edge para exportar PDF. " +
                "Instale Microsoft Edge o use la exportación HTML.");
        }

        // Usar Edge en modo headless con --print-to-pdf
        return await ExportViaEdgeHeadlessAsync(htmlPath, outputPdfPath, edgePath);
    }

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

    private static async Task<bool> ExportViaEdgeHeadlessAsync(string htmlPath, string outputPdfPath, string edgePath)
    {
        // Convertir ruta a formato file:// URI para Edge
        var fileUri = new Uri(htmlPath).AbsoluteUri;

        // Usar Edge en modo headless con --print-to-pdf
        // Nota: --no-pdf-header-footer evita agregar numeración de página predeterminada
        var arguments = $"--headless --disable-gpu --no-sandbox " +
                        $"--print-to-pdf=\"{outputPdfPath}\" " +
                        $"--no-pdf-header-footer " +
                        $"\"{fileUri}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = edgePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Esperar con timeout de 30 segundos
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (TimeoutException)
        {
            try { process.Kill(); } catch { }
            throw new TimeoutException("La exportación a PDF tomó demasiado tiempo (30s).");
        }

        // Verificar que el archivo PDF se creó y tiene cabecera válida
        if (File.Exists(outputPdfPath))
        {
            return await ValidatePdfFileAsync(outputPdfPath);
        }

        return false;
    }

    /// <summary>
    /// Valida que un archivo PDF tenga cabecera válida (%PDF).
    /// </summary>
    public static async Task<bool> ValidatePdfFileAsync(string pdfPath)
    {
        try
        {
            using var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[5];

            if (await stream.ReadAsync(buffer.AsMemory(0, 5)) >= 5)
            {
                var header = Encoding.ASCII.GetString(buffer);
                return header.StartsWith("%PDF");
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> GetEdgeExecutablePathWithWhereAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "msedge.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstLine = output.Split('\n')[0].Trim();
                if (File.Exists(firstLine))
                    return firstLine;
            }
        }
        catch { }

        return null;
    }
}
