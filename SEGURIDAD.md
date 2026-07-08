# Seguridad - CATTECH OPTIMIZER PRO

**ESTRICTAMENTE OBLIGATORIO**  
Este documento contiene reglas de seguridad que TODOS los desarrolladores y colaboradores deben seguir sin excepción.

---

## Reglas Fundamentales

### 1. Nunca Ejecutar Cambios Sin Confirmación

```text
❌ PROHIBIDO
- Ejecutar cualquier optimización/reparación automáticamente
- Aplicar tweaks sin mostrar al usuario qué se va a cambiar
- Continuar con una operación destructiva tras un error

✅ CORRECTO
- SIEMPRE mostrar resumen de cambios antes de ejecutar
- SIEMPRE pedir confirmación explícita del usuario
- SIEMPRE permitir cancelar en cualquier momento
- Incluir botón "Volver" en cada paso crítico
```

**Implementación obligatoria**:
```csharp
public async Task<bool> ExecuteWithConfirmationAsync(ChangeOperation operation)
{
    // 1. Mostrar qué se va a cambiar
    var summary = operation.GetSummary();
    bool confirmed = await ShowConfirmationDialogAsync(summary);
    
    if (!confirmed)
    {
        Logger.LogInformation("Operación cancelada por el usuario: {Operation}", operation.Name);
        return false;
    }
    
    // 2. Crear punto de restauración si aplica
    if (operation.RequiresRestorePoint)
    {
        await CreateRestorePointAsync($"Pre-{operation.Name}");
    }
    
    // 3. Ejecutar con manejo de errores
    try
    {
        return await operation.ExecuteAsync();
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "Error ejecutando {Operation}", operation.Name);
        await ShowErrorDialogAsync(ex);
        return false;
    }
}
```

---

### 2. No Aplicar Tweaks Destructivos

```text
❌ PROHIBIDO EN MVP
- Desactivar servicios de Windows críticos
- Modificar el registro del sistema directamente
- Eliminar archivos del sistema operativo
- Desactivar funciones de seguridad de Windows
- Modificar permisos de archivos del sistema
- Alterar configuración de arranque (BCD)
- Desactivar UAC permanentemente
- Eliminar actualizaciones de Windows

✅ PERMITIDO CON CONFIRMACIÓN
- Limpiar archivos temporales del usuario
- Limpiar caché de Windows Update (post-instalación)
- Optimizar ajustes visuales de rendimiento
- Crear puntos de restauración
- Desactivar animaciones innecesarias
```

**Lista blanca de tweaks seguros (MVP)**:
```json
{
  "safeOptimizations": [
    {
      "id": "visual-animations",
      "name": "Desactivar animaciones",
      "risk": "low",
      "reversible": true,
      "requiresConfirmation": true
    },
    {
      "id": "visual-transparency",
      "name": "Desactivar transparencias",
      "risk": "low",
      "reversible": true,
      "requiresConfirmation": true
    },
    {
      "id": "visual-shadows",
      "name": "Desactivar sombras",
      "risk": "low",
      "reversible": true,
      "requiresConfirmation": true
    },
    {
      "id": "cleanup-temp",
      "name": "Limpiar temporales",
      "risk": "low",
      "reversible": false,
      "requiresConfirmation": true,
      "createsRestorePoint": true
    }
  ]
}
```

---

### 3. No Desactivar Defender por Defecto

```text
❌ PROHIBIDO
- Incluir opción para desactivar Windows Defender
- Desactivar protección en tiempo real
- Agregar exclusiones de escaneo permanentemente
- Desactivar protección contra amenazas y ransomware
- Modificar políticas de Defender sin consentimiento

✅ PERMITIDO
- Mostrar estado actual de Defender
- Explicar si Defender está desactivado (informativo)
- Sugerir configuración recomendada
- Nunca modificar configuración de Defender
```

**Excepción futura (v0.4+)**:
- Solo si el técnico lo solicita explícitamente
- Requiere autenticación de administrador
- Crea punto de restauración antes
- Revierte automáticamente después de X minutos
- Registra la acción en el log

---

### 4. No Desactivar Windows Update por Defecto

```text
❌ PROHIBIDO
- Desactivar el servicio de Windows Update
- Bloquear instalación de actualizaciones
- Modificar políticas de actualización
- Eliminar actualizaciones instaladas
- Configurar Windows Update para no descargar

✅ PERMITIDO
- Mostrar estado de Windows Update (informativo)
- Reparar Windows Update si está roto (con confirmación)
- Limpiar caché de Windows Update (con confirmación)
- Mostrar historial de actualizaciones
```

**Reparación de Windows Update (v0.3+)**:
```csharp
public async Task<bool> RepairWindowsUpdateAsync()
{
    // 1. Verificar que Windows Update realmente tiene problemas
    bool isBroken = await DiagnoseWindowsUpdateAsync();
    if (!isBroken)
    {
        ShowMessage("Windows Update parece estar funcionando correctamente.");
        return false;
    }
    
    // 2. Mostrar qué se va a hacer
    var steps = new[]
    {
        "Detener servicios de Windows Update",
        "Eliminar archivos de caché temporales",
        "Reiniciar servicios",
        "Verificar funcionamiento"
    };
    
    bool confirmed = await ShowConfirmationDialogAsync(
        "Reparar Windows Update", 
        "Este proceso intentará reparar Windows Update. ¿Continuar?",
        steps);
    
    if (!confirmed) return false;
    
    // 3. Crear punto de restauración
    await CreateRestorePointAsync("Pre-WindowsUpdate-Repair");
    
    // 4. Ejecutar reparación con logging detallado
    return await ExecuteWindowsUpdateRepairAsync();
}
```

---

### 5. No Tocar Servicios Críticos Sin Explicación

```text
❌ PROHIBIDO
- Detener servicios sin explicar por qué
- Cambiar tipo de inicio de servicios
- Eliminar servicios del sistema
- Modificar permisos de servicios
- Desactivar servicios de seguridad

✅ PERMITIDO
- Mostrar lista de servicios (informativo)
- Explicar función de cada servicio
- Sugerir configuración óptima (solo sugerencia)
- Nunca modificar sin aprobación explícita
```

**Servicios críticos que NUNCA se deben tocar**:
| Servicio | Nombre | Razón |
|----------|--------|-------|
| Windows Update | wuauserv | Seguridad del sistema |
| Windows Defender | WinDefend | Protección antivirus |
| Firewall | MpsSvc | Protección de red |
| Cryptographic | cryptSvc | Seguridad de certificados |
| Windows Search | WSearch | Funcionalidad del sistema |
| Windows Audio | Audiosrv | Funcionalidad básica |
| RPC | RpcSs | Comunicación interna |
| DCOM | DcomLaunch | Comunicación entre procesos |

**Servicios que SÍ se pueden sugerir desactivar (v0.2+)**:
- SysMain (Superfetch) en SSD
- DiagTrack (Telemetry)
- RetailDemo
- MapsBroker

---

### 6. Crear Punto de Restauración Cuando Corresponda

```text
✅ REGLA: Siempre crear punto de restauración antes de:
- Cambios en el registro del sistema
- Modificación de servicios
- Limpieza de archivos del sistema
- Optimizaciones que modifican configuración
- Cualquier operación que no se pueda deshacer fácilmente
```

**Cuándo crear puntos de restauración**:
```csharp
public static class RestorePointRequirements
{
    public static bool RequiresRestorePoint(OptimizationType type)
    {
        return type switch
        {
            // Siempre crear punto
            OptimizationType.RegistryChange => true,
            OptimizationType.ServiceModification => true,
            OptimizationType.SystemCleanup => true,
            OptimizationType.DriverUpdate => true,
            
            // No requiere punto
            OptimizationType.VisualOptimization => false,
            OptimizationType.TempCleanup => false,
            OptimizationType.ReportGeneration => false,
            
            // Default: crear punto por seguridad
            _ => true
        };
    }
}
```

**Política de puntos de restauración**:
- Máximo 3 puntos de CATTECH por sistema
- Eliminar puntos antiguos al crear nuevos
- Nombrar descriptivamente: `CATTECH - Pre-[Operación] - [Fecha]`
- Verificar que el sistema tiene suficiente espacio

---

### 7. Registrar Todas las Acciones

```text
✅ OBLIGATORIO: Loggear:
- Cada optimización aplicada
- Cada archivo eliminado
- Cada cambio de configuración
- Cada punto de restauración creado
- Cada error encontrado
- Cada operación cancelada por el usuario
- Timestamp de cada acción
- Usuario que ejecutó la acción
```

**Formato de log**:
```json
{
  "timestamp": "2024-01-15T14:30:25.123Z",
  "level": "Information",
  "action": "CleanupTemp",
  "target": "C:\\Users\\Usuario\\AppData\\Local\\Temp",
  "filesDeleted": 142,
  "spaceFreed": "256 MB",
  "user": "Técnico Juan",
  "client": "Cliente ABC",
  "equipment": "PC-001",
  "restorePointCreated": true,
  "restorePointName": "CATTECH - Pre-CleanupTemp - 20240115"
}
```

**Ubicación de logs**:
```
%APPDATA%\CATTECH\logs\
├── cattech-2024-01-15.log      # Log diario
├── cattech-errors-2024-01.log  # Log de errores mensual
└── actions/                     # Logs de acciones específicas
    ├── cleanup-20240115-143025.json
    └── optimization-20240115-150000.json
```

---

### 8. Mantener Posibilidad de Reversión

```text
✅ OBLIGATORIO: Cada operación modificable debe poder revertirse
- Mantener registro de valores originales
- Ofrecer botón "Deshacer" cuando sea posible
- Documentar cómo revertir manualmente
- Crear backups antes de cambios estructurales
```

**Implementación de reversión**:
```csharp
public interface IReversibleOperation
{
    string OperationId { get; }
    DateTime ExecutedAt { get; }
    Dictionary<string, object> OriginalValues { get; }
    Dictionary<string, object> NewValues { get; }
    
    Task<bool> RevertAsync();
    Task<string> GetRevertInstructionsAsync();
}

public class OptimizationOperation : IReversibleOperation
{
    public async Task<bool> RevertAsync()
    {
        try
        {
            // Restaurar valores originales
            foreach (var kvp in OriginalValues)
            {
                await SetValueAsync(kvp.Key, kvp.Value);
            }
            
            Logger.LogInformation("Operación {OperationId} revertida exitosamente", OperationId);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error revirtiendo operación {OperationId}", OperationId);
            return false;
        }
    }
    
    public Task<string> GetRevertInstructionsAsync()
    {
        var instructions = "Para revertir manualmente:\n";
        foreach (var kvp in OriginalValues)
        {
            instructions += $"- Restaurar {kvp.Key} a: {kvp.Value}\n";
        }
        return Task.FromResult(instructions);
    }
}
```

---

### 9. No Incluir Activadores, Bypasses Ni Herramientas Ilegales

```text
❌ PROHIBIDO ABSOLUTAMENTE
- Activadores de Windows o Office
- Keygens o generadores de licencias
- Bypass de verificación de licencia
- Herramientas de cracking
- Software pirateado
- Bypass de restricciones de hardware
- Tools para evadir restricciones de Microsoft
- Cualquier cosa que viole términos de servicio

✅ PERMITIDO
- Verificar licencia actual (informativo)
- Mostrar estado de activación
- Sugerir购买 licencia oficial
- Herramientas de diagnóstico legítimas
```

**Política estricta**:
- Si se encuentra código ilegal en PRs → Rechazar inmediatamente
- Si se detecta uso de herramientas ilegales → Deshabilitar cuenta
- Incluir disclaimer en About/Settings
- Reportar incidentes a maintainer principal

---

### 10. No Copiar Código de Terceros Sin Revisar Licencia

```text
❌ PROHIBIDO
- Copiar código sin verificar licencia
- Usar código GPL en proyecto MIT sin cumplir requisitos
- No incluir atribución requerida
- Modificar licencias existentes
- Ignorar restricciones de copyleft

✅ PROCEDIMIENTO CORRECTO
1. Verificar licencia del código fuente
2. Verificar compatibilidad con MIT
3. Incluir atribución requerida
4. Documentar origen en comentarios
5. Respetar restricciones de distribución
```

**Tabla de licencias y compatibilidad**:

| Licencia | Compatível con MIT | Requisitos |
|----------|---------------------|------------|
| MIT | ✅ Sí | Mantener copyright |
| Apache 2.0 | ✅ Sí | Mantener copyright, notice |
| BSD 2/3 | ✅ Sí | Mantener copyright |
| LGPL | ⚠️ Parcial | Solo binario externo |
| MPL 2.0 | ⚠️ Parcial | Attribution, compartir cambios |
| GPL 2.0/3.0 | ❌ No | No compatible con MIT |

**Checklist antes de usar código**:
- [ ] ¿Qué licencia tiene?
- [ ] ¿Es compatible con MIT?
- [ ] ¿Requiere attribution?
- [ ] ¿Requiere compartir cambios?
- [ ] ¿Puedo usarlo como binario externo?
- [ ] ¿Está documentado en THIRD-PARTY-NOTICES?

---

## Reglas Adicionales para Contribuidores

### Código

- **Nunca** hardcodear credenciales o tokens
- **Nunca** incluir archivos de configuración con datos sensibles
- **Siempre** usar variables de entorno para secrets
- **Siempre** sanitizar input de usuario
- **Siempre** validar permisos antes de ejecutar

### Testing

- **Nunca** testear directamente en sistema productivo
- **Siempre** usar VM o ambiente de prueba
- **Siempre** testear operaciones reversibles
- **Siempre** testear manejo de errores

### Documentación

- **Siempre** documentar cambios de seguridad
- **Siempre** actualizar CHANGELOG
- **Siempre** explicar por qué se rechaza un PR de seguridad

---

## Reporte de Vulnerabilidades

Si encontrás una vulnerabilidad de seguridad:

1. **NO** la publiques en Issues públicos
2. **NO** la compartas en redes sociales
3. **SÍ** envía un email a: security@cattech.com (futuro)
4. **SÍ** espera confirmación antes de publicar
5. **SÍ** permite tiempo razonable para parchear

---

## Auditorías de Seguridad

### Revisión de Código Obligatoria
- Cada PR que modifique seguridad requiere 2 approvals
- Revisión explícita de manejo de permisos
- Verificación de logging de acciones

### Dependencias
- Ejecutar `dotnet list package --vulnerable` semanalmente
- Actualizar dependencias con vulnerabilidades críticas inmediatamente
- Documentar todas las dependencias en README

---

## Disclaimer

```text
CATTECH OPTIMIZER PRO se proporciona "TAL CUAL", sin garantía de ningún tipo.

El uso de esta herramienta es bajo responsabilidad del usuario.

El desarrollador no se hace responsable de:
- Daños al sistema operativo
- Pérdida de datos
- Problemas de compatibilidad
- Uso indebido de la herramienta

Siempre crear puntos de restauración antes de usar la herramienta.
Siempre backups antes de cualquier operación crítica.
```

---

## Desactivación de Programas de Inicio

### Reglas específicas para desactivación

1. **No eliminar entradas**: Solo mover a ubicación de backup
2. **Microsoft bloqueado**: No se permite desactivar entradas de Microsoft
3. **Solo fuentes soportadas**: Registry Run y carpetas de inicio
4. **Backup obligatorio**: Cada desactivación crea un registro de backup
5. **Reversión posible**: Todas las desactivaciones son reversibles
6. **Confirmación obligatoria**: Resumen antes de ejecutar
7. **Registro de acciones**: Cada cambio queda registrado con timestamp y técnico

### Fuentes NO desactivables (v0.1)
- RunOnce
- Tareas programadas
- Servicios
- Drivers
- Windows Defender

### Estrategia de backup
- **Registro**: Se copia a `HKCU/HKLM\Software\CATTECH\OptimizerPro\DisabledStartup\Run`
- **Archivos**: Se mueven a `backups/startup/YYYYMMDD-HHMMSS/`
- **Metadata**: Se guarda en `backups/startup/startup-backups.json`

---

## Limpieza de Temporales

### Ubicaciones permitidas (v0.1)

| Ubicación | Riesgo | Notas |
|-----------|--------|-------|
| `%TEMP%` del usuario | Bajo | Seleccionado por defecto |
| `C:\Windows\Temp` | Bajo | Requiere permisos de admin |
| Miniaturas de Explorer | Bajo | Opcional, solo `thumbcache_*.db` |
| Papelera de reciclaje | Medio | Opcional, no seleccionada por defecto |

### Ubicaciones PROHIBIDAS (v0.1)

- ❌ Descargas
- ❌ Documentos
- ❌ Escritorio
- ❌ AppData completo
- ❌ Perfiles de navegador
- ❌ WinSxS
- ❌ System32
- ❌ Program Files
- ❌ ProgramData completo
- ❌ Carpetas de drivers
- ❌ Carpetas de usuario completas

### Reglas de seguridad

1. **No limpiar automáticamente**: Siempre escanear primero
2. **Confirmación obligatoria**: Resumen antes de borrar
3. **No forzar borrado**: Omitir archivos bloqueados
4. **Registro completo**: Todo queda registrado
5. **No limpieza agresiva**: Solo archivos temporales seguros
6. **Protección de archivos recientes**: No borrar archivos de los últimos 60 segundos

---

## Optimización Visual

### Reglas de seguridad

1. **Backup obligatorio**: Cada valor se guarda antes de modificar
2. **Reversión posible**: Todos los cambios son reversibles desde backups
3. **No cambiar resolución**: Nunca se modifica la resolución de pantalla
4. **No modificar drivers**: Nunca se tocan drivers de video
5. **No tocar servicios**: Nunca se desactivan servicios
6. **No tocar accesibilidad**: Se mantiene soporte de accesibilidad
7. **Confirmación obligatoria**: Resumen antes de aplicar
8. **Solo ajustes documentados**: Cada registro está documentado en código

### Ajustes permitidos (v0.1)
- Animaciones de ventanas/menús
- Sombras del mouse
- Contenido al arrastrar ventanas
- Suavizado de fuentes (ClearType)
- Aero Peek
- Animaciones de minimizar/maximizar

### Ajustes PROHIBIDOS
- ❌ Resolución de pantalla
- ❌ Escalado DPI
- ❌ Drivers de video
- ❌ Servicios del sistema
- ❌ Configuración de accesibilidad
- ❌ Temas personales del usuario
- ❌ Configuración de Windows Defender
- ❌ Configuración de Windows Update

---

## Puntos de Restauración

### Reglas de seguridad

1. **No crear automáticamente**: Siempre requiere acción explícita del técnico
2. **Confirmación obligatoria**: Resumen antes de crear
3. **No habilitar protección**: No se modifica la configuración de Restaurar sistema
4. **No eliminar puntos**: Nunca se borran puntos de restauración existentes
5. **Verificar permisos**: Requiere administrador
6. **Registrar todo**: Cada intento queda registrado con resultado
7. **No bloquear la app**: Si falla, la app continúa funcionando

### Errores manejados
- Permisos insuficientes
- Protección del sistema deshabilitada
- Frecuencia limitada por Windows (máximo 1 por día)
- Servicio de Restaurar sistema no disponible

### Nombre del punto
Formato estándar: `CATTECH Optimizer Pro - Antes de mantenimiento - yyyy-MM-dd HH:mm`

---

*Documento de seguridad - CATTECH OPTIMIZER PRO*  
*Última actualización: 2026*
