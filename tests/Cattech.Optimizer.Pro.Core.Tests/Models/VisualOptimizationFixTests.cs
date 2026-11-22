using System.Text.Json;
using Cattech.Optimizer.Pro.Core.Interfaces;
using Cattech.Optimizer.Pro.Core.Models.VisualOptimization;
using Cattech.Optimizer.Pro.Infrastructure.VisualOptimization;
using Cattech.Optimizer.Pro.UI.ViewModels;
using Microsoft.Win32;

namespace Cattech.Optimizer.Pro.Core.Tests.Models;

/// <summary>
/// Regresiones de la pantalla "Optimización visual":
/// - selección observable sin doble toggle (CheckBox TwoWay + INotifyPropertyChanged);
/// - "Aplicar seleccionados" habilitado solo con selección real y guardas de ejecución;
/// - "Restaurar" usa el backup recibido como parámetro y recarga sin esconder el panel;
/// - binarios persistidos/restaurados en hexadecimal (nunca "System.Byte[]");
/// - errores reales de restauración reportados en la UI (no un bool mudo);
/// - JSON de backups dañado reportado, nunca presentado como "No hay backups".
///
/// Seguridad: los tests de registro usan una clave única de QA bajo
/// HKCU\Software\CATTECH\QA\{guid} y la eliminan en finally.
/// No se modifican ajustes reales del usuario ni rutas del sistema.
/// </summary>
public sealed class VisualOptimizationFixTests
{
    private static readonly JsonSerializerOptions BackupsJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ===================== Selección observable =====================

    [Fact]
    public async Task CheckBoxTwoWayToggle_SelectsSetting_AndUpdatesSelectedCount()
    {
        var (vm, _) = CreateViewModel(
            Setting("A", VisualRiskLevel.Low, current: "1", recommended: "0"),
            Setting("B", VisualRiskLevel.Low, current: "1", recommended: "0"));

        await vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Settings.Count);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.ApplyCommand.CanExecute(null));

        var first = vm.Settings[0];
        string? notifiedProperty = null;
        first.PropertyChanged += (_, e) => notifiedProperty = e.PropertyName;

        // Simula el click del CheckBox: WPF hace OnToggle y el binding TwoWay
        // escribe true. Antes el Command adicional invertía otra vez el valor.
        first.IsSelected = !first.IsSelected;

        Assert.True(first.IsSelected);
        Assert.Equal(nameof(VisualOptimizationSetting.IsSelected), notifiedProperty);
        Assert.Equal(1, vm.SelectedCount);
        Assert.True(vm.ApplyCommand.CanExecute(null));

        first.IsSelected = !first.IsSelected;

        Assert.False(first.IsSelected);
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public async Task SelectAllSafe_SelectsOnlyLowRiskNotOptimized_AndDeselectAllClears()
    {
        var (vm, _) = CreateViewModel(
            Setting("LOW", VisualRiskLevel.Low, current: "1", recommended: "0"),
            Setting("ALREADY", VisualRiskLevel.Low, current: "0", recommended: "0"),
            Setting("MEDIUM", VisualRiskLevel.Medium, current: "1", recommended: "0"));

        await vm.AnalyzeCommand.ExecuteAsync(null);

        vm.SelectAllSafeCommand.Execute(null);

        Assert.True(vm.Settings.Single(s => s.Id == "LOW").IsSelected);
        Assert.False(vm.Settings.Single(s => s.Id == "ALREADY").IsSelected);
        Assert.False(vm.Settings.Single(s => s.Id == "MEDIUM").IsSelected);
        Assert.Equal(1, vm.SelectedCount);
        Assert.Equal(1, vm.AlreadyOptimizedCount);

        vm.DeselectAllCommand.Execute(null);

        Assert.All(vm.Settings, s => Assert.False(s.IsSelected));
        Assert.Equal(0, vm.SelectedCount);
    }

    [Fact]
    public async Task ApplyCommand_WithoutSelection_DoesNotCallService_AndShowsError()
    {
        var (vm, fake) = CreateViewModel(
            Setting("A", VisualRiskLevel.Low, current: "1", recommended: "0"));

        await vm.AnalyzeCommand.ExecuteAsync(null);
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Equal(0, fake.ApplyCalls);
        Assert.True(vm.HasError);
        Assert.Contains("No hay ajustes seleccionados", vm.ErrorMessage);
    }

    [Fact]
    public async Task ApplyCommand_ReanalyzesBeforePublishing_AndKeepsSuccessMessage()
    {
        var (vm, fake) = CreateViewModel(
            Setting("A", VisualRiskLevel.Low, current: "1", recommended: "0"));

        await vm.AnalyzeCommand.ExecuteAsync(null);
        vm.Settings[0].IsSelected = true;
        fake.ApplyResult = new VisualOptimizationResult { AppliedCount = 1 };

        await vm.ApplyCommand.ExecuteAsync(null);

        // El re-análisis ocurre ANTES de publicar el mensaje: el banner no se borra.
        Assert.True(vm.HasResults);
        Assert.True(vm.HasResult);
        Assert.True(vm.IsSuccess);
        Assert.False(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.StatusMessage));
        Assert.Equal("Optimización completada", vm.StatusText);
        Assert.Equal(1, vm.ResultAppliedCount);
        Assert.Empty(vm.ResultErrors);
        Assert.False(vm.HasResultErrors);

        // El re-análisis limpia la selección: no se puede re-aplicar por accidente.
        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Single(fake.LastApplied!);
    }

    [Fact]
    public async Task ApplyCommand_ShowsRealErrorsFromResult()
    {
        var (vm, fake) = CreateViewModel(
            Setting("A", VisualRiskLevel.Low, current: "1", recommended: "0"));

        await vm.AnalyzeCommand.ExecuteAsync(null);
        vm.Settings[0].IsSelected = true;

        var applied = new VisualOptimizationResult { AppliedCount = 0, FailedCount = 1 };
        applied.Errors.Add(new VisualOptimizationError
        {
            SettingId = "A",
            SettingName = "Ajuste A",
            Message = "Acceso denegado al registro.",
            ErrorType = "RegistryWriteError"
        });
        fake.ApplyResult = applied;

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.True(vm.HasResult);
        Assert.True(vm.HasResultErrors);
        Assert.True(vm.HasError);
        Assert.False(vm.IsSuccess);
        Assert.Contains("Acceso denegado", Assert.Single(vm.ResultErrors).Message);
        Assert.Contains("Acceso denegado", vm.ErrorMessage);
        Assert.Equal("Optimización completada con advertencias", vm.StatusText);
    }

    // ===================== Restaurar desde backup =====================

    [Fact]
    public async Task RestoreBackupCommand_UsesBackupParameter_AndKeepsBackupsPanelVisible()
    {
        var backup = new VisualOptimizationBackup
        {
            SettingId = "A",
            SettingName = "Ajuste A",
            RegistryPath = @"Software\CATTECH\QA\Fake",
            RegistryValueName = "A",
            OriginalValue = "1",
            ValueType = "String",
            CanRestore = true
        };
        var (vm, fake) = CreateViewModel(
            Setting("A", VisualRiskLevel.Low, current: "0", recommended: "1"));
        fake.Backups.Add(backup);

        await vm.AnalyzeCommand.ExecuteAsync(null);
        await vm.LoadBackupsCommand.ExecuteAsync(null);

        Assert.True(vm.ShowBackups);
        var item = Assert.Single(vm.Backups);

        // No se asigna SelectedBackup: el backup llega como CommandParameter, como en el XAML.
        Assert.Null(vm.SelectedBackup);
        Assert.True(vm.RestoreBackupCommand.CanExecute(item));

        await vm.RestoreBackupCommand.ExecuteAsync(item);

        Assert.True(vm.IsSuccess);
        Assert.Contains("Ajuste A", vm.StatusMessage);
        Assert.Equal(1, fake.RestoreCalls);
        Assert.Same(backup, fake.LastRestoredBackup);
        Assert.True(vm.ShowBackups); // recargar no debe esconder el panel
    }

    [Fact]
    public async Task RestoreBackupCommand_IsDisabled_WhenBackupCannotRestore()
    {
        var (vm, _) = CreateViewModel(
            Setting("A", VisualRiskLevel.Low, current: "0", recommended: "1"));

        await vm.AnalyzeCommand.ExecuteAsync(null);

        Assert.False(vm.RestoreBackupCommand.CanExecute(null));
        Assert.True(vm.RestoreBackupCommand.CanExecute(
            new VisualOptimizationBackup { CanRestore = true }));
        Assert.False(vm.RestoreBackupCommand.CanExecute(
            new VisualOptimizationBackup { CanRestore = false }));
    }

    [Fact]
    public async Task LegacyImplementation_WithoutDetailedRestore_StillWorks()
    {
        // La implementación por defecto de la interfaz mantiene compatibles los
        // dobles de prueba existentes (solo implementan RestoreAsync).
        var fake = new FakeVisualOptimizationService { RestoreSucceeds = false };
        var backup = new VisualOptimizationBackup { SettingName = "Ajuste legado" };

        var result = await ((IVisualOptimizationService)fake).RestoreWithResultAsync(backup);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(1, fake.RestoreCalls);
    }

    // ===================== Persistencia hexadecimal y corrupción =====================

    [Fact]
    public async Task Service_ApplyAsync_BinaryValue_IsPersistedAsHex_AndRestored()
    {
        var keyPath = CreateTestRegistryKey();
        var temp = CreateTempDirectory();
        var originalBytes = new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x1C, 0x00 };
        var appliedBytes = new byte[] { 0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 };

        try
        {
            WriteBinaryTestValue(keyPath, "TestBinary", originalBytes);

            var service = new VisualOptimizationService(temp);
            var setting = Setting(
                "QA_BINARY",
                VisualRiskLevel.Low,
                current: Convert.ToHexString(originalBytes),
                recommended: Convert.ToHexString(appliedBytes));
            setting.DisplayName = "Valor binario de prueba";
            setting.RegistryPath = keyPath;
            setting.RegistryValueName = "TestBinary";
            setting.ValueType = "BINARY";
            setting.IsSelected = true;

            var apply = await service.ApplyAsync(new[] { setting });

            Assert.Equal(1, apply.AppliedCount);
            Assert.Empty(apply.Errors);
            Assert.Equal(appliedBytes, ReadBinaryTestValue(keyPath, "TestBinary"));

            var backups = await service.ListBackupsAsync();
            var backup = Assert.Single(backups);
            Assert.Equal(Convert.ToHexString(originalBytes), backup.OriginalValue);

            var backupsJson = await File.ReadAllTextAsync(
                Path.Combine(temp, "backups", "visual", "visual-backups.json"));
            Assert.DoesNotContain("System.Byte[]", backupsJson);

            var restore = await service.RestoreWithResultAsync(backup);

            Assert.True(restore.Success, restore.Message);
            Assert.Equal(originalBytes, ReadBinaryTestValue(keyPath, "TestBinary"));
            Assert.False(Assert.Single(await service.ListBackupsAsync()).CanRestore);
        }
        finally
        {
            DeleteTestRegistryKey(keyPath);
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task Service_ApplyAsync_StringValue_IsRestoredToOriginal()
    {
        var keyPath = CreateTestRegistryKey();
        var temp = CreateTempDirectory();

        try
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, true)!)
                key.SetValue("TestString", "1", RegistryValueKind.String);

            var service = new VisualOptimizationService(temp);
            var setting = Setting(
                "QA_STRING",
                VisualRiskLevel.Low,
                current: "1",
                recommended: "0");
            setting.DisplayName = "Valor de texto de prueba";
            setting.RegistryPath = keyPath;
            setting.RegistryValueName = "TestString";
            setting.IsSelected = true;

            var apply = await service.ApplyAsync(new[] { setting });

            Assert.Equal(1, apply.AppliedCount);
            Assert.Equal("0", ReadStringTestValue(keyPath, "TestString"));

            var backup = Assert.Single(await service.ListBackupsAsync());
            Assert.Equal("1", backup.OriginalValue);

            var restore = await service.RestoreWithResultAsync(backup);

            Assert.True(restore.Success, restore.Message);
            Assert.Equal("1", ReadStringTestValue(keyPath, "TestString"));
        }
        finally
        {
            DeleteTestRegistryKey(keyPath);
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_BinaryValues_AreHexNotByteArrayText()
    {
        var temp = CreateTempDirectory();
        try
        {
            var service = new VisualOptimizationService(temp);
            var settings = await service.AnalyzeAsync();
            var binarySettings = settings
                .Where(s => s.ValueType.Equals("BINARY", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.NotEmpty(binarySettings);
            foreach (var setting in binarySettings.Where(s => s.CurrentValue is not null))
            {
                var value = setting.CurrentValue!;
                Assert.DoesNotContain("System.Byte[]", value);
                Assert.Equal(0, value.Length % 2);
                Assert.All(value, c => Assert.True(Uri.IsHexDigit(c), $"'{c}' no es hex"));
            }
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task ListBackupsAsync_ReportsCorruptJson_InsteadOfSilentEmpty()
    {
        var temp = CreateTempDirectory();
        try
        {
            var backupsDirectory = Path.Combine(temp, "backups", "visual");
            Directory.CreateDirectory(backupsDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(backupsDirectory, "visual-backups.json"),
                "{ esto no es json válido ");

            var service = new VisualOptimizationService(temp);

            var ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.ListBackupsAsync());
            Assert.Contains("dañado", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task LoadBackups_ShowsError_WhenBackupFileIsCorrupt()
    {
        var temp = CreateTempDirectory();
        try
        {
            var backupsDirectory = Path.Combine(temp, "backups", "visual");
            Directory.CreateDirectory(backupsDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(backupsDirectory, "visual-backups.json"),
                "{ esto no es json válido ");

            var service = new VisualOptimizationService(temp);
            var vm = new VisualOptimizationViewModel(service);

            await vm.LoadBackupsCommand.ExecuteAsync(null);

            Assert.True(vm.ShowBackups); // el panel no se esconde
            Assert.True(vm.HasError);
            Assert.Contains("backups", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(vm.Backups);
            Assert.False(vm.IsSuccess);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task ApplyAsync_WithCorruptBackups_ReportsError_AndDoesNotOverwriteHistory()
    {
        var temp = CreateTempDirectory();
        try
        {
            var backupsDirectory = Path.Combine(temp, "backups", "visual");
            Directory.CreateDirectory(backupsDirectory);
            var backupsPath = Path.Combine(backupsDirectory, "visual-backups.json");
            const string corruptJson = "{ esto no es json válido ";
            await File.WriteAllTextAsync(backupsPath, corruptJson);

            var service = new VisualOptimizationService(temp);
            var setting = Setting("A", VisualRiskLevel.Low, current: "1", recommended: "0");
            setting.IsSelected = true;

            var result = await service.ApplyAsync(new[] { setting });

            Assert.False(result.IsSuccess);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal("BackupCorrupt", Assert.Single(result.Errors).ErrorType);
            Assert.Equal(corruptJson, await File.ReadAllTextAsync(backupsPath));
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task RestoreWithResult_ReportsLegacyCorruptBinary_WithoutSwallowingError()
    {
        var keyPath = CreateTestRegistryKey();
        var temp = CreateTempDirectory();
        var currentBytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        try
        {
            WriteBinaryTestValue(keyPath, "TestBinary", currentBytes);

            // Backup legado: antes los binarios se guardaban como "System.Byte[]".
            var legacyBackup = new VisualOptimizationBackup
            {
                SettingId = "QA_LEGACY",
                SettingName = "Binario legado",
                RegistryPath = keyPath,
                RegistryValueName = "TestBinary",
                OriginalValue = "System.Byte[]",
                ValueType = "BINARY",
                CanRestore = true
            };
            var backupsDirectory = Path.Combine(temp, "backups", "visual");
            Directory.CreateDirectory(backupsDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(backupsDirectory, "visual-backups.json"),
                JsonSerializer.Serialize(new List<VisualOptimizationBackup> { legacyBackup }, BackupsJsonOptions));

            var service = new VisualOptimizationService(temp);

            var result = await service.RestoreWithResultAsync(legacyBackup);

            Assert.False(result.Success);
            Assert.Equal("RegistryRestoreError", result.ErrorType);
            Assert.Contains("hexadecimal", result.Message, StringComparison.OrdinalIgnoreCase);

            // El valor actual no se tocó y el backup sigue disponible para diagnóstico.
            Assert.Equal(currentBytes, ReadBinaryTestValue(keyPath, "TestBinary"));
            Assert.True(Assert.Single(await service.ListBackupsAsync()).CanRestore);
        }
        finally
        {
            DeleteTestRegistryKey(keyPath);
            TryDeleteDirectory(temp);
        }
    }

    [Fact]
    public async Task RestoreWithResult_WhenBackupIsMissing_ReportsRealReason()
    {
        var temp = CreateTempDirectory();
        try
        {
            var service = new VisualOptimizationService(temp);
            var unknown = new VisualOptimizationBackup
            {
                SettingId = "NO_EXISTE",
                SettingName = "Backup inexistente"
            };

            var result = await service.RestoreWithResultAsync(unknown);

            Assert.False(result.Success);
            Assert.Equal("BackupNotFound", result.ErrorType);
            Assert.Contains("No se encontró", result.Message);
        }
        finally
        {
            TryDeleteDirectory(temp);
        }
    }

    // ===================== Helpers =====================

    private static (VisualOptimizationViewModel Vm, FakeVisualOptimizationService Fake) CreateViewModel(
        params VisualOptimizationSetting[] settings)
    {
        var fake = new FakeVisualOptimizationService
        {
            // Cada análisis devuelve instancias nuevas, como el servicio real.
            SettingsFactory = () => settings.Select(CloneSetting).ToList()
        };
        return (new VisualOptimizationViewModel(fake), fake);
    }

    private static VisualOptimizationSetting Setting(
        string id, VisualRiskLevel risk, string? current, string recommended)
    {
        return new VisualOptimizationSetting
        {
            Id = id,
            DisplayName = $"Ajuste {id}",
            Description = $"Descripción de {id}",
            RegistryPath = @"Software\CATTECH\QA\Fake",
            RegistryValueName = id,
            CurrentValue = current,
            RecommendedValue = recommended,
            ValueType = "String",
            RiskLevel = risk
        };
    }

    private static VisualOptimizationSetting CloneSetting(VisualOptimizationSetting source)
    {
        return new VisualOptimizationSetting
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Description = source.Description,
            RegistryPath = source.RegistryPath,
            RegistryValueName = source.RegistryValueName,
            CurrentValue = source.CurrentValue,
            RecommendedValue = source.RecommendedValue,
            ValueType = source.ValueType,
            RequiresRestart = source.RequiresRestart,
            RequiresSignOut = source.RequiresSignOut,
            RiskLevel = source.RiskLevel,
            IsSupported = source.IsSupported,
            Notes = source.Notes
        };
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "cattech-qa-visual-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (IOException)
        {
            // Limpieza best-effort: no debe enmascarar el resultado del test.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string CreateTestRegistryKey()
    {
        var path = @"Software\CATTECH\QA\" + Guid.NewGuid().ToString("N");
        using var key = Registry.CurrentUser.CreateSubKey(path, true)
            ?? throw new InvalidOperationException("No se pudo crear la clave de prueba de QA.");
        return path;
    }

    private static void DeleteTestRegistryKey(string path)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(path);
        }
        catch (ArgumentException)
        {
            // La clave de QA ya no existe: nada que limpiar.
        }
        catch (UnauthorizedAccessException)
        {
            // Nunca debe afectar ajustes reales del usuario: solo esta clave de QA.
        }
    }

    private static void WriteBinaryTestValue(string keyPath, string valueName, byte[] bytes)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, true)
            ?? throw new InvalidOperationException("No se pudo crear la clave de prueba de QA.");
        key.SetValue(valueName, bytes, RegistryValueKind.Binary);
    }

    private static byte[] ReadBinaryTestValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, false)
            ?? throw new InvalidOperationException("La clave de prueba de QA no existe.");
        return key.GetValue(valueName) as byte[]
            ?? throw new InvalidOperationException("El valor binario de prueba no existe.");
    }

    private static string? ReadStringTestValue(string keyPath, string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, false)
            ?? throw new InvalidOperationException("La clave de prueba de QA no existe.");
        return key.GetValue(valueName) as string;
    }

    private sealed class FakeVisualOptimizationService : IVisualOptimizationService
    {
        public Func<List<VisualOptimizationSetting>> SettingsFactory { get; set; } = () => [];

        public VisualOptimizationResult ApplyResult { get; set; } = new();

        public List<VisualOptimizationBackup> Backups { get; } = [];

        public bool RestoreSucceeds { get; set; } = true;

        public int ApplyCalls { get; private set; }

        public int RestoreCalls { get; private set; }

        public List<VisualOptimizationSetting>? LastApplied { get; private set; }

        public VisualOptimizationBackup? LastRestoredBackup { get; private set; }

        public Task<List<VisualOptimizationSetting>> AnalyzeAsync()
            => Task.FromResult(SettingsFactory());

        public Task<VisualOptimizationResult> ApplyAsync(
            IEnumerable<VisualOptimizationSetting> settings, string reason = "")
        {
            ApplyCalls++;
            LastApplied = settings.ToList();
            return Task.FromResult(ApplyResult);
        }

        public Task<List<VisualOptimizationBackup>> ListBackupsAsync()
            => Task.FromResult(Backups.ToList());

        public Task<bool> RestoreAsync(VisualOptimizationBackup backup)
        {
            RestoreCalls++;
            LastRestoredBackup = backup;
            return Task.FromResult(RestoreSucceeds);
        }

        public Task<string> SaveResultAsync(VisualOptimizationResult result)
            => Task.FromResult("synthetic-visual-result");
    }
}
