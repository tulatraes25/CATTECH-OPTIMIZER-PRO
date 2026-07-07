using System.Diagnostics;
using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.RestorePoint;

namespace Cattech.Optimizer.Pro.Infrastructure.RestorePoint;

/// <summary>
/// Implementación de IRestorePointService.
/// Crea puntos de restauración usando PowerShell o WMI.
/// </summary>
public class RestorePointService : IRestorePointService
{
    private readonly string _baseDirectory;
    private readonly string _resultsDirectory;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public RestorePointService(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
        _resultsDirectory = Path.Combine(_baseDirectory, "data", "restore-points");
    }

    /// <inheritdoc/>
    public async Task<RestorePointStatus> CheckStatusAsync()
    {
        var status = new RestorePointStatus
        {
            LastCheckedAt = DateTime.Now
        };

        // 1. Verificar permisos de administrador
        status.IsAdministrator = await CheckIsAdministratorAsync();

        if (!status.IsAdministrator)
        {
            status.Warnings.Add("La aplicación no está ejecutando como administrador");
        }

        // 2. Verificar servicio de Restaurar sistema
        status.IsSystemRestoreAvailable = await CheckSystemRestoreAvailableAsync();

        if (!status.IsSystemRestoreAvailable)
        {
            status.Warnings.Add("El servicio de Restaurar sistema no está disponible");
        }

        // 3. Verificar protección del sistema
        status.IsProtectionEnabled = await CheckProtectionEnabledAsync();

        if (!status.IsProtectionEnabled)
        {
            status.Warnings.Add("La protección del sistema parece estar deshabilitada");
        }

        // Generar mensaje de estado
        if (status.CanCreatePoint)
        {
            status.StatusMessage = "Sistema listo para crear puntos de restauración";
        }
        else
        {
            status.StatusMessage = $"No se puede crear punto: {status.CannotCreateReason}";
        }

        return status;
    }

    /// <inheritdoc/>
    public async Task<RestorePointResult> CreateRestorePointAsync(string name)
    {
        var result = new RestorePointResult
        {
            RequestedAt = DateTime.Now,
            RestorePointName = name,
            TechnicianName = await GetTechnicianNameAsync()
        };

        // Verificar permisos
        result.RequiresAdministrator = !await CheckIsAdministratorAsync();
        result.ProtectionEnabled = await CheckProtectionEnabledAsync();

        if (result.RequiresAdministrator)
        {
            result.Success = false;
            result.ErrorMessage = "Se requieren permisos de administrador para crear puntos de restauración";
            result.ErrorCode = "ACCESS_DENIED";
            result.FinishedAt = DateTime.Now;
            return result;
        }

        if (!result.ProtectionEnabled)
        {
            result.Success = false;
            result.ErrorMessage = "La protección del sistema está deshabilitada. No se puede crear punto de restauración.";
            result.ErrorCode = "PROTECTION_DISABLED";
            result.FinishedAt = DateTime.Now;
            return result;
        }

        // Intentar crear el punto
        try
        {
            var success = await CreateRestorePointViaPowerShellAsync(name, result);

            if (!success && result.ErrorCode != "FREQUENCY_LIMIT")
            {
                // Intentar método alternativo via WMI
                success = await CreateRestorePointViaWmiAsync(name, result);
            }

            result.FinishedAt = DateTime.Now;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error inesperado: {ex.Message}";
            result.ErrorCode = "UNEXPECTED_ERROR";
            result.FinishedAt = DateTime.Now;
            return result;
        }
    }

    /// <inheritdoc/>
    public string GenerateRestorePointName()
    {
        return $"CATTECH Optimizer Pro - Antes de mantenimiento - {DateTime.Now:yyyy-MM-dd HH:mm}";
    }

    /// <inheritdoc/>
    public async Task<string> SaveResultAsync(RestorePointResult result)
    {
        EnsureDirectoryExists(_resultsDirectory);

        var fileName = $"restore-point-result-{result.RequestedAt:yyyyMMdd-HHmmss}.json";
        var filePath = Path.Combine(_resultsDirectory, fileName);

        if (File.Exists(filePath))
        {
            fileName = $"restore-point-result-{result.RequestedAt:yyyyMMdd-HHmmss}-{result.Id}.json";
            filePath = Path.Combine(_resultsDirectory, fileName);
        }

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json);

        return fileName;
    }

    /// <inheritdoc/>
    public async Task<List<RestorePointResultSummary>> ListResultsAsync(int maxResults = 20)
    {
        var summaries = new List<RestorePointResultSummary>();

        if (!Directory.Exists(_resultsDirectory))
            return summaries;

        var files = Directory.GetFiles(_resultsDirectory, "restore-point-result-*.json")
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(maxResults);

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var result = JsonSerializer.Deserialize<RestorePointResult>(json, SerializerOptions);

                if (result != null)
                {
                    summaries.Add(new RestorePointResultSummary
                    {
                        Id = result.Id,
                        RequestedAt = result.RequestedAt,
                        RestorePointName = result.RestorePointName,
                        Success = result.Success,
                        ErrorMessage = result.ErrorMessage,
                        FileName = Path.GetFileName(file)
                    });
                }
            }
            catch { }
        }

        return summaries;
    }

    // --- Métodos internos ---

    private static async Task<bool> CheckIsAdministratorAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"([Security.Principal.WindowsPrincipal]([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckSystemRestoreAvailableAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"Get-Service -Name 'srservice' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Status\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var status = output.Trim();
            return status == "Running" || status == "Stopped";
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckProtectionEnabledAsync()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-Command \"(Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Select-Object -First 1).Count -gt 0\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // No se pudo determinar, asumir que sí
            return true;
        }
    }

    private static async Task<bool> CreateRestorePointViaPowerShellAsync(string name, RestorePointResult result)
    {
        try
        {
            // Método 1: Usar Checkpoint-Computer (requiere permisos y protección habilitada)
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"Checkpoint-Computer -Description '{name}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            result.StandardOutput = await process.StandardOutput.ReadToEndAsync();
            result.StandardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            result.MethodUsed = RestorePointMethod.PowerShellCheckpoint;

            if (process.ExitCode == 0)
            {
                result.Success = true;
                result.Notes = "Punto de restauración creado exitosamente via PowerShell";
                return true;
            }

            // Verificar si es error de frecuencia
            if (result.StandardError.Contains("frequency", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("14 dias", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("14 days", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.ErrorMessage = "Windows limita la creación de puntos de restauración. " +
                    "Ya se creó uno recientemente. intente nuevamente más tarde.";
                result.ErrorCode = "FREQUENCY_LIMIT";
                result.MethodUsed = RestorePointMethod.PowerShellCheckpoint;
                return false;
            }

            // Verificar si es error de protección
            if (result.StandardError.Contains("protection", StringComparison.OrdinalIgnoreCase) ||
                result.StandardError.Contains("disabled", StringComparison.OrdinalIgnoreCase))
            {
                result.Success = false;
                result.ErrorMessage = "La protección del sistema está deshabilitada.";
                result.ErrorCode = "PROTECTION_DISABLED";
                result.MethodUsed = RestorePointMethod.PowerShellCheckpoint;
                return false;
            }

            result.Success = false;
            result.ErrorMessage = $"Error de PowerShell: {result.StandardError}";
            result.ErrorCode = "POWERSHELL_ERROR";
            return false;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error al ejecutar PowerShell: {ex.Message}";
            result.ErrorCode = "POWERSHELL_EXCEPTION";
            result.MethodUsed = RestorePointMethod.PowerShellCheckpoint;
            return false;
        }
    }

    private static async Task<bool> CreateRestorePointViaWmiAsync(string name, RestorePointResult result)
    {
        try
        {
            // Método 2: Usar WMI SystemRestore
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"" +
                        "$class = Get-WmiObject -List -Namespace 'root\\default' | Where-Object {{ $_.Name -eq 'SystemRestore' }}; " +
                        "$class.InvokeMethod('CreateRestorePoint', @('{name}', 12, 100))\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            result.StandardOutput = await process.StandardOutput.ReadToEndAsync();
            result.StandardError = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            result.MethodUsed = RestorePointMethod.WmiSystemRestore;

            if (process.ExitCode == 0)
            {
                result.Success = true;
                result.Notes = "Punto de restauración creado exitosamente via WMI";
                return true;
            }

            result.Success = false;
            result.ErrorMessage = $"Error de WMI: {result.StandardError}";
            result.ErrorCode = "WMI_ERROR";
            return false;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error al ejecutar WMI: {ex.Message}";
            result.ErrorCode = "WMI_EXCEPTION";
            result.MethodUsed = RestorePointMethod.WmiSystemRestore;
            return false;
        }
    }

    private static async Task<string> GetTechnicianNameAsync()
    {
        return await Helpers.SettingsHelper.GetTechnicianNameAsync(new Data.JsonSettingsService());
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
