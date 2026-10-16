namespace Cattech.Optimizer.Pro.Infrastructure.Diagnostics;

/// <summary>
/// Localiza DumpChk solo en directorios estándar del Windows SDK.
/// Nunca busca ejecutables en PATH, carpeta actual o descargas del usuario.
/// No prueba autenticidad criptográfica: el técnico debe confiar en su SDK.
/// </summary>
internal sealed class DumpCheckToolLocator
{
    private readonly Func<string?> _programFiles;
    private readonly Func<string?> _programFilesX86;
    private readonly Func<string, bool> _exists;
    private readonly bool _isWindows;
    private readonly bool _is64Bit;

    public DumpCheckToolLocator()
        : this(
            () => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            () => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            File.Exists,
            OperatingSystem.IsWindows(),
            Environment.Is64BitOperatingSystem)
    {
    }

    internal DumpCheckToolLocator(
        Func<string?> programFiles,
        Func<string?> programFilesX86,
        Func<string, bool> exists,
        bool isWindows,
        bool is64Bit)
    {
        _programFiles = programFiles ?? throw new ArgumentNullException(nameof(programFiles));
        _programFilesX86 = programFilesX86 ?? throw new ArgumentNullException(nameof(programFilesX86));
        _exists = exists ?? throw new ArgumentNullException(nameof(exists));
        _isWindows = isWindows;
        _is64Bit = is64Bit;
    }

    public string? Locate()
    {
        if (!_isWindows)
            return null;

        var architecture = _is64Bit ? "x64" : "x86";
        var roots = new[] { _programFilesX86(), _programFiles() };
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            var file = Path.Combine(root, "Windows Kits", "10", "Debuggers",
                architecture, "dumpchk.exe");

            try
            {
                if (_exists(file))
                    return file;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Instalación SDK inaccesible: seguir o declarar herramienta ausente.
            }
        }
        return null;
    }
}

/// <summary>Valida un basename de minidump y lo restringe al directorio elegido.</summary>
internal static class DumpCheckPathPolicy
{
    public static bool TryResolve(
        string directory, string? dumpFileName, Func<string, bool> exists,
        out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(directory)
            || string.IsNullOrWhiteSpace(dumpFileName)
            || dumpFileName.Length > 128
            || dumpFileName.Contains('/')
            || dumpFileName.Contains('\\')
            || dumpFileName.Contains(':')
            || dumpFileName.Contains("..", StringComparison.Ordinal)
            || !dumpFileName.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var root = Path.GetFullPath(directory);
            var resolved = Path.GetFullPath(Path.Combine(root, dumpFileName));
            var parent = Path.GetDirectoryName(resolved);
            if (!string.Equals(parent, root.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase)
                || !exists(resolved))
                return false;

            fullPath = resolved;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
