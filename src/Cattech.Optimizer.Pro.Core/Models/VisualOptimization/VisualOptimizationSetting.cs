using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cattech.Optimizer.Pro.Core.Models.VisualOptimization;

/// <summary>
/// Nivel de riesgo de un ajuste visual.
/// </summary>
public enum VisualRiskLevel
{
    Low,
    Medium
}

/// <summary>
/// Ajuste visual que puede ser aplicado para mejorar rendimiento.
/// Solo lectura: no aplica cambios sin confirmación.
/// Implementa INotifyPropertyChanged para que los CheckBox enlazados en modo
/// TwoWay y los contadores de selección de la UI se mantengan sincronizados.
/// </summary>
public class VisualOptimizationSetting : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _displayName = string.Empty;
    private string _description = string.Empty;
    private string _registryPath = string.Empty;
    private string _registryValueName = string.Empty;
    private string? _currentValue;
    private string _recommendedValue = string.Empty;
    private string _valueType = "DWORD";
    private bool _requiresRestart;
    private bool _requiresSignOut;
    private VisualRiskLevel _riskLevel;
    private bool _isSelected;
    private bool _isSupported = true;
    private string _notes = string.Empty;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// ID único del ajuste.
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// Nombre para mostrar.
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    /// Descripción del ajuste.
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// Ruta del registro.
    /// </summary>
    public string RegistryPath
    {
        get => _registryPath;
        set => SetProperty(ref _registryPath, value);
    }

    /// <summary>
    /// Nombre del valor en el registro.
    /// </summary>
    public string RegistryValueName
    {
        get => _registryValueName;
        set => SetProperty(ref _registryValueName, value);
    }

    /// <summary>
    /// Valor actual leído del registro.
    /// </summary>
    public string? CurrentValue
    {
        get => _currentValue;
        set => SetProperty(ref _currentValue, value);
    }

    /// <summary>
    /// Valor recomendado para rendimiento.
    /// </summary>
    public string RecommendedValue
    {
        get => _recommendedValue;
        set => SetProperty(ref _recommendedValue, value);
    }

    /// <summary>
    /// Tipo de valor (DWORD, String, etc.).
    /// </summary>
    public string ValueType
    {
        get => _valueType;
        set => SetProperty(ref _valueType, value);
    }

    /// <summary>
    /// Si requiere reinicio para aplicarse.
    /// </summary>
    public bool RequiresRestart
    {
        get => _requiresRestart;
        set => SetProperty(ref _requiresRestart, value);
    }

    /// <summary>
    /// Si requiere cierre de sesión.
    /// </summary>
    public bool RequiresSignOut
    {
        get => _requiresSignOut;
        set => SetProperty(ref _requiresSignOut, value);
    }

    /// <summary>
    /// Nivel de riesgo.
    /// </summary>
    public VisualRiskLevel RiskLevel
    {
        get => _riskLevel;
        set => SetProperty(ref _riskLevel, value);
    }

    /// <summary>
    /// Si está seleccionado para aplicar.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Si es compatible con la versión actual de Windows.
    /// </summary>
    public bool IsSupported
    {
        get => _isSupported;
        set => SetProperty(ref _isSupported, value);
    }

    /// <summary>
    /// Si el valor actual ya es el recomendado.
    /// </summary>
    public bool IsAlreadyOptimized => CurrentValue == RecommendedValue;

    /// <summary>
    /// Notas adicionales.
    /// </summary>
    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Propiedad calculada: cualquier cambio en sus entradas debe notificarse.
        if (propertyName is nameof(CurrentValue) or nameof(RecommendedValue))
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAlreadyOptimized)));
    }
}

/// <summary>
/// Ajustes visuales predefinidos para v0.1.
/// </summary>
public static class VisualOptimizationPresets
{
    public static List<VisualOptimizationSetting> GetDefaultSettings()
    {
        return
        [
            new VisualOptimizationSetting
            {
                Id = "ANIMATIONS",
                DisplayName = "Desactivar animaciones de ventanas",
                Description = "Reduce el uso de CPU al desactivar animaciones de apertura/cierre de ventanas.",
                RegistryPath = @"Control Panel\Desktop",
                RegistryValueName = "UserPreferencesMask",
                RecommendedValue = "9012038010000000",
                ValueType = "BINARY",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Mejora rendimiento en equipos con baja RAM o HDD"
            },
            new VisualOptimizationSetting
            {
                Id = "MENU_ANIMATIONS",
                DisplayName = "Desactivar animaciones de menús",
                Description = "Desactiva las animaciones al abrir menús contextuales.",
                RegistryPath = @"Control Panel\Desktop",
                RegistryValueName = "MenuShowDelay",
                RecommendedValue = "0",
                ValueType = "String",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Menús aparecen inmediatamente"
            },
            new VisualOptimizationSetting
            {
                Id = "MOUSE_SHADOW",
                DisplayName = "Desactivar sombras del mouse",
                Description = "Desactiva la sombra que sigue al cursor del mouse.",
                RegistryPath = @"Control Panel\Desktop",
                RegistryValueName = "UserPreferencesMask",
                RecommendedValue = "9012038010000000",
                ValueType = "BINARY",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Reduce uso de GPU"
            },
            new VisualOptimizationSetting
            {
                Id = "DRAG_FULLWindows",
                DisplayName = "Mostrar contenido al arrastrar ventanas",
                Description = "Muestra el contenido de la ventana mientras se arrastra (en lugar de solo el contorno).",
                RegistryPath = @"Control Panel\Desktop",
                RegistryValueName = "DragFullWindows",
                RecommendedValue = "0",
                ValueType = "String",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Desactivado: solo muestra contorno al arrastrar"
            },
            new VisualOptimizationSetting
            {
                Id = "FONT_SMOOTHING",
                DisplayName = "Suavizado de fuentes (ClearType)",
                Description = "Mantiene el suavizado de fuentes para legibilidad. Recomendado: activado.",
                RegistryPath = @"Control Panel\Desktop",
                RegistryValueName = "FontSmoothing",
                RecommendedValue = "2",
                ValueType = "String",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Mantener activado para legibilidad"
            },
            new VisualOptimizationSetting
            {
                Id = "COMPOSITION",
                DisplayName = "Composición de escritorio",
                Description = "Activa la composición del escritorio (requerido para efectos modernos). Recomendado: activado.",
                RegistryPath = @"SOFTWARE\Microsoft\Windows\Dwm",
                RegistryValueName = "EnableAeroPeek",
                RecommendedValue = "0",
                ValueType = "DWORD",
                RiskLevel = VisualRiskLevel.Medium,
                Notes = "Desactivar Aero Peek puede mejorar rendimiento en GPU débil"
            },
            new VisualOptimizationSetting
            {
                Id = "ANIMATE_MINMAX",
                DisplayName = "Animaciones de minimizar/maximizar",
                Description = "Desactiva las animaciones al minimizar y maximizar ventanas.",
                RegistryPath = @"Control Panel\Desktop\WindowMetrics",
                RegistryValueName = "MinAnimate",
                RecommendedValue = "0",
                ValueType = "String",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Ventanas aparecen sin animación"
            },
            new VisualOptimizationSetting
            {
                Id = "SMOOTH_SCREEN_FONT",
                DisplayName = "Suavizado de fuentes en pantalla",
                Description = "Controla el suavizado de fuentes en todo el sistema.",
                RegistryPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes",
                RegistryValueName = "Segoe UI",
                RecommendedValue = "Segoe UI",
                ValueType = "String",
                RiskLevel = VisualRiskLevel.Low,
                Notes = "Mantener fuente estándar para legibilidad"
            }
        ];
    }
}
