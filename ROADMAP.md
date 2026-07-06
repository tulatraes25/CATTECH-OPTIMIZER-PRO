# Roadmap - CATTECH OPTIMIZER PRO

**Versión actual**: 0.1 (En planificación)  
**Última actualización**: 2024  
**Objetivo**: Suite completa de diagnóstico, optimización y reparación para técnicos informáticos.

---

## Visión General

```
v0.1 ──► v0.2 ──► v0.3 ──► v0.4 ──► v0.5 ──► v1.0
  │        │        │        │        │        │
  ▼        ▼        ▼        ▼        ▼        ▼
MVP    SMART &   BSOD &  Preserve  Rescue  Suite
Basic  Hardware  Repair            USB     Completa
```

---

## v0.1 - MVP Básico (6-8 semanas)

**Objetivo**: Versión funcional mínima para pruebas internas.

### Funcionalidades Incluidas

#### 1. Configuración de Empresa/Técnico
- [ ] Formulario para datos de empresa
- [ ] Subida de logo (JPG/PNG)
- [ ] Datos de técnico (nombre, ID, email)
- [ ] Persistencia en `company.json`
- [ ] Preview del logo en UI

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.UI/Views/Settings/CompanySettingsView.xaml
src/Cattech.Optimizer.Pro.UI/ViewModels/SettingsViewModel.cs
src/Cattech.Optimizer.Pro.Infrastructure/Data/SettingsRepository.cs
config/company.json
```

#### 2. Datos de Cliente/Equipo
- [ ] Formulario de cliente (nombre, email, teléfono)
- [ ] Formulario de equipo (marca, modelo, serial, OS)
- [ ] Campo de motivo de visita
- [ ] Campo de notas del técnico
- [ ] Asociación cliente-equipo-report

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.UI/Views/Client/ClientInfoView.xaml
src/Cattech.Optimizer.Pro.UI/ViewModels/ClientViewModel.cs
src/Cattech.Optimizer.Pro.Core/Models/Reports/ClientInfo.cs
src/Cattech.Optimizer.Pro.Core/Models/Reports/EquipmentInfo.cs
```

#### 3. Diagnóstico Básico
- [ ] Detección de hardware (CPU, RAM, GPU, Discos)
- [ ] Información del OS (versión, build, arquitectura)
- [ ] Información de discos: espacio libre, tipo HDD/SSD (si Windows lo informa)
- [ ] Uso de CPU/RAM en tiempo real
- [ ] Temperaturas (si disponible via LibreHardwareMonitor)
- [ ] **NO** implementar SMART completo (se posterga a v0.2)

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Hardware/HardwareProvider.cs
src/Cattech.Optimizer.Pro.UI/Views/Diagnostics/DiagnosticsView.xaml
src/Cattech.Optimizer.Pro.UI/ViewModels/DiagnosticsViewModel.cs
```

#### 4. Programas de Inicio (Solo lectura)
- [ ] Lista de programas que inician con Windows
- [ ] Información de cada programa
- [ ] **NO** permite deshabilitar (solo informativo)
- [ ] Indicador de impacto en arranque

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Hardware/StartupProvider.cs
src/Cattech.Optimizer.Pro.UI/Views/Optimization/OptimizationView.xaml
```

#### 5. Limpieza Segura de Temporales
- [ ] Limpieza de `%TEMP%` del usuario
- [ ] Limpieza de `%TEMP%` del sistema
- [ ] Limpieza de `C:\Windows\Temp`
- [ ] Resumen antes de eliminar
- [ ] Conteo de archivos y espacio a liberar
- [ ] Punto de restauración previo

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/CleanupService.cs
src/Cattech.Optimizer.Pro.UI/Views/Optimization/OptimizationView.xaml
```

#### 6. Optimización Visual Segura
- [ ] Desactivar animaciones
- [ ] Desactivar transparencias
- [ ] Ajustar rendimiento visual
- [ ] Desactivar sombras
- [ ] Revertir cambios (deshacer)

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/VisualOptimizationService.cs
src/Cattech.Optimizer.Pro.UI/Views/Optimization/OptimizationView.xaml
```

#### 7. Punto de Restauración
- [ ] Crear punto de restauración manual
- [ ] Lista de puntos existentes
- [ ] Información de cada punto
- [ ] Verificación de espacio disponible

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/RestorePointService.cs
src/Cattech.Optimizer.Pro.UI/Views/Optimization/OptimizationView.xaml
```

#### 8. Generación de Informe
- [ ] Generación de HTML con datos completos
- [ ] Conversión a PDF (QuestPDF)
- [ ] Logo de empresa en encabezado
- [ ] Datos del técnico y cliente
- [ ] Resumen ejecutivo
- [ ] Detalles de hardware
- [ ] Acciones realizadas
- [ ] Botón de guardar/abrir

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Templates/ReportHtmlTemplate.cs
src/Cattech.Optimizer.Pro.Infrastructure/Templates/ReportPdfTemplate.cs
src/Cattech.Optimizer.Pro.UI/Views/Reports/ReportsView.xaml
templates/report-html.html
```

### Criterios de Aceptación v0.1

- [ ] La aplicación compila sin errores
- [ ] Se ejecuta como administrador sin crash
- [ ] Detección de hardware funciona en 3+ máquinas diferentes
- [ ] Limpieza de temporales libera espacio sin eliminar archivos del usuario
- [ ] Punto de restauración se crea exitosamente
- [ ] Informe HTML se genera correctamente
- [ ] Informe PDF se genera correctamente
- [ ] Logging funciona sin saturar disco
- [ ] No ejecuta cambios destructivos sin confirmación

### Dependencias v0.1

```xml
<ItemGroup>
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  <PackageReference Include="LibreHardwareMonitorLib" Version="0.9.6" />
  <PackageReference Include="QuestPDF" Version="2024.1.0" />
  <PackageReference Include="Serilog" Version="3.1.1" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
</ItemGroup>
```

---

## v0.2 - SMART y Hardware (4 semanas)

**Objetivo**: Diagnóstico profundo de hardware y salud de discos.

### Funcionalidades Incluidas

#### 1. SMART Completo
- [ ] Ejecución de smartctl para cada disco
- [ ] Parseo de salida JSON
- [ ] Todos los atributos SMART
- [ ] Historial de temperaturas
- [ ] Predicción de fallos
- [ ] Comparación con valores umbrales

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Hardware/SmartProvider.cs
src/Cattech.Optimizer.Pro.Core/Models/Diagnostics/SmartResult.cs
src/Cattech.Optimizer.Pro.Core/Models/Diagnostics/SmartAttribute.cs
```

#### 2. Monitoreo en Tiempo Real
- [ ] Dashboard con sensores en vivo
- [ ] Gráficos de temperatura CPU/GPU/Discos
- [ ] Uso de CPU/RAM/Disco
- [ ] Velocidades de ventiladores
- [ ] Voltajes (si disponible)

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.UI/Views/Dashboard/DashboardView.xaml
src/Cattech.Optimizer.Pro.UI/ViewModels/DashboardViewModel.cs
src/Cattech.Optimizer.Pro.Infrastructure/Hardware/SensorProvider.cs
```

#### 3. Reportes Comparativos
- [ ] Comparación antes/después de optimización
- [ ] Historial de diagnósticos por equipo
- [ ] Gráficos de tendencias
- [ ] Exportación de históricos

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/HistoryRepository.cs
src/Cattech.Optimizer.Pro.UI/Views/Reports/ReportHistoryView.xaml
```

#### 4. Exportación Adicional
- [ ] Exportación a JSON
- [ ] Exportación a CSV
- [ ] Exportación a XML
- [ ] Copia al portapapeles

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/ExportService.cs
src/Cattech.Optimizer.Pro.UI/ViewModels/ReportsViewModel.cs
```

#### 5. Modo Oscuro
- [ ] Tema oscuro completo
- [ ] Toggle en settings
- [ ] Persistencia de preferencia
- [ ] Todos los controles adaptados

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.UI/Resources/Themes/DarkTheme.xaml
src/Cattech.Optimizer.Pro.UI/Resources/Themes/LightTheme.xaml
src/Cattech.Optimizer.Pro.UI/App.xaml
```

### Criterios de Aceptación v0.2

- [ ] SMART funciona con HDD, SSD y NVMe
- [ ] Monitoreo en tiempo real sin lag significativo
- [ ] Gráficos se actualizan cada 2 segundos
- [ ] Reportes comparativos muestran diferencias
- [ ] Modo oscuro funciona en todos los formularios
- [ ] Exportación JSON/CSV contiene todos los datos

---

## v0.3 - BSOD y Reparaciones Comunes (4 semanas)

**Objetivo**: Análisis de crashes y reparación de problemas comunes.

### Funcionalidades Incluidas

#### 1. Análisis Básico de BSOD
- [ ] Lectura de minidumps (`C:\Windows\Minidump`)
- [ ] Decodificación de Bug Check Codes
- [ ] Identificación de driver problemático
- [ ] Historial de crashes
- [ ] Reporte de crashes

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Diagnostics/MinidumpParser.cs
src/Cattech.Optimizer.Pro.Infrastructure/Diagnostics/BugCheckDecoder.cs
src/Cattech.Optimizer.Pro.Core/Models/Diagnostics/CrashInfo.cs
config/bugcheckcodes.json
```

#### 2. Reparación de Windows Update
- [ ] Diagnóstico de Windows Update
- [ ] Reseteo de servicios (con confirmación)
- [ ] Limpieza de caché
- [ ] Verificación post-reparación

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/WindowsUpdateRepairService.cs
src/Cattech.Optimizer.Pro.UI/Views/Repair/WindowsUpdateRepairView.xaml
```

#### 3. Reparación de Archivos del Sistema
- [ ] Ejecución de `sfc /scannow`
- [ ] Ejecución de `DISM /RestoreHealth`
- [ ] Interpretación de resultados
- [ ] Historial de reparaciones

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/SystemFileRepairService.cs
src/Cattech.Optimizer.Pro.UI/Views/Repair/SystemFileRepairView.xaml
```

#### 4. Historial de Servicios
- [ ] Log de servicios por cliente/equipo
- [ ] Comparación entre servicios
- [ ] Estadísticas de problemas comunes
- [ ] Base de datos local de incidentes

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/ServiceHistoryRepository.cs
src/Cattech.Optimizer.Pro.UI/Views/Reports/ServiceHistoryView.xaml
```

### Criterios de Aceptación v0.3

- [ ] Lee minidumps de Windows 10/11
- [ ] Identifica drivers problemáticos en 80% de los casos
- [ ] Reparación de Windows Update funciona en 70% de intentos
- [ ] SFC/DISM ejecutan correctamente
- [ ] Historial se persiste correctamente

---

## v0.4 - CATTECH Preserve (6 semanas)

**Objetivo**: Backup y restauración de configuración del sistema.

### Funcionalidades Incluidas

#### 1. Backup de Configuración
- [ ] Backup de configuración de red
- [ ] Backup de programas instalados
- [ ] Backup de configuraciones de usuario
- [ ] Backup de drivers críticos
- [ ] Backup de registro (subset seleccionado)

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/BackupService.cs
src/Cattech.Optimizer.Pro.Core/Models/Backup/BackupManifest.cs
src/Cattech.Optimizer.Pro.UI/Views/Backup/BackupView.xaml
```

#### 2. Restauración de Configuración
- [ ] Restauración selectiva
- [ ] Vista previa antes de restaurar
- [ ] Confirmación obligatoria
- [ ] Punto de restauración previo

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/RestoreService.cs
src/Cattech.Optimizer.Pro.UI/Views/Backup/RestoreView.xaml
```

#### 3. Sincronización
- [ ] Exportar backup a USB
- [ ] Importar backup desde USB
- [ ] Sincronización entre equipos del mismo cliente
- [ ] Validación de integridad

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/SyncService.cs
src/Cattech.Optimizer.Pro.UI/Views/Backup/SyncView.xaml
```

### Criterios de Aceptación v0.4

- [ ] Backup completo en menos de 5 minutos
- [ ] Restauración funciona sin errores
- [ ] Sincronización USB funciona correctamente
- [ ] Integridad verificada con hash

---

## v0.5 - CATTECH Rescue USB (8 semanas)

**Objetivo**: Creación de USB bootable con herramientas de rescate.

### Funcionalidades Incluidas

#### 1. Creación de USB Bootable
- [ ] Interfaz para seleccionar USB
- [ ] Creación de particiones
- [ ] Formato FAT32/NTFS
- [ ] Instalación de WinPE
- [ ] Configuración de arranque

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.Infrastructure/Data/UsbCreatorService.cs
src/Cattech.Optimizer.Pro.UI/Views/Rescue/UsbCreatorView.xaml
tools/winpe/ (archivos WinPE)
```

#### 2. Herramientas de Rescate
- [ ] Memtest86+ integrado
- [ ] Herramientas de diagnóstico offline
- [ ] Recuperación de archivos básica
- [ ] Reparación de arranque

**Archivos afectados**:
```
tools/memtest86plus/
tools/rescue/
src/Cattech.Optimizer.Pro.Infrastructure/Data/RescueToolsService.cs
```

#### 3. Personalización
- [ ] Logo de empresa en USB
- [ ] Menú de inicio personalizado
- [ ] Herramientas seleccionables
- [ ] Documentación integrada

**Archivos afectados**:
```
tools/winpe/customization/
templates/rescue-menu.html
```

### Criterios de Aceptación v0.5

- [ ] USB bootea en UEFI y Legacy
- [ ] Memtest86+ funciona correctamente
- [ ] Herramientas de diagnóstico offline operativas
- [ ] Recuperación de archivos funciona
- [ ] USB es bootable en 90% de PCs modernos

---

## v1.0 - Suite Completa (4 semanas)

**Objetivo**: Estabilización, documentación y distribución.

### Funcionalidades Incluidas

#### 1. Estabilización
- [ ] Testing completo de todas las funciones
- [ ] Corrección de bugs conocidos
- [ ] Optimización de rendimiento
- [ ] Pruebas en múltiples configuraciones

#### 2. Documentación
- [ ] Guía de usuario completa
- [ ] Guía del técnico
- [ ] Documentación de API (para extensiones futuras)
- [ ] FAQ y troubleshooting

**Archivos afectados**:
```
docs/guia-usuario.md
docs/guia-tecnico.md
docs/api-reference.md
docs/faq.md
```

#### 3. Distribución
- [ ] Instalador profesional (Inno Setup)
- [ ] Versión portable
- [ ] Actualizaciones automáticas (opcional)
- [ ] Registro de licencia (si aplica)

**Archivos afectados**:
```
installer/setup.iss
scripts/build.ps1
scripts/publish.ps1
```

#### 4. Multi-idioma
- [ ] Soporte Español
- [ ] Soporte Portugués
- [ ] Soporte Inglés
- [ ] Framework de localización

**Archivos afectados**:
```
src/Cattech.Optimizer.Pro.UI/Resources/Languages/
src/Cattech.Optimizer.Pro.UI/Converters/LocalizationConverter.cs
```

### Criterios de Aceptación v1.0

- [ ] 0 bugs críticos conocidos
- [ ] Funciona en Windows 10/11 sin issues
- [ ] Documentación completa y precisa
- [ ] Instalador funciona correctamente
- [ ] Actualizaciones automáticas operativas

---

## Fases Futuras (Post v1.0)

### v1.1 - Soporte Multi-Técnico
- [ ] Múltiples perfiles de técnico
- [ ] Base de datos compartida
- [ ] Sincronización en la nube
- [ ] Dashboard administrativo

### v1.2 - Integración con Herramientas Externas
- [ ] Plugin system
- [ ] Integración con antivirus
- [ ] Integración con herramientas de monitoreo
- [ ] API para terceros

### v1.3 - Versiones Especiales
- [ ] CATTECH Enterprise (red)
- [ ] CATTECH Mobile (companion app)
- [ ] CATTECH Cloud (dashboard web)

---

## Decisiones de Arquitectura por Versión

### v0.1 - Decisiones Clave

1. **Framework UI**: WPF
   - Justificación: Compatibilidad, estabilidad, ecosistema

2. **Patrón**: MVVM con CommunityToolkit
   - Justificación: Separación de capas, testabilidad

3. **Hardware Monitoring**: LibreHardwareMonitorLib
   - Justificación: Licencia MPL 2.0 compatible, NuGet

4. **SMART**: smartctl.exe externo
   - Justificación: Estándar de la industria, licencia GPL

5. **PDF**: QuestPDF
   - Justificación: Open source, MIT, sin dependencias externas

6. **Logging**: Serilog
   - Justificación: Estructurado, múltiples sinks, alto rendimiento

7. **Persistencia**: JSON files
   - Justificación: Simple, portable, suficiente para MVP

---

## Milestones y Fechas Estimadas

| Versión | Duración | Fecha Inicio | Fecha Fin | Estado |
|---------|----------|--------------|-----------|--------|
| v0.1 | 6-8 semanas | - | - | 📋 Planificación |
| v0.2 | 4 semanas | - | - | ⏳ Pendiente |
| v0.3 | 4 semanas | - | - | ⏳ Pendiente |
| v0.4 | 6 semanas | - | - | ⏳ Pendiente |
| v0.5 | 8 semanas | - | - | ⏳ Pendiente |
| v1.0 | 4 semanas | - | - | ⏳ Pendiente |

---

## Cómo Contribuir

1. **Leer** SEGURIDAD.md antes de cualquier cambio
2. **Revisar** el ROADMAP para ver tareas disponibles
3. **Crear** issue-discusión antes de PRs grandes
4. **Seguir** convenciones de código existentes
5. **Tests** obligatorios para código nuevo
6. **Documentar** cambios en CHANGELOG.md

---

## Tracking de Progreso

### v0.1 - Progreso Actual

```
Setup de Proyecto y Estructura    [▶] En progreso
Configuración Empresa/Técnico     [ ] 0%
Datos de Cliente/Equipo           [ ] 0%
Diagnóstico Básico                [ ] 0%
Programas de Inicio               [ ] 0%
Limpieza Temporales               [ ] 0%
Optimización Visual               [ ] 0%
Punto de Restauración             [ ] 0%
Generación de Informe             [ ] 0%
```

**Siguiente tarea**: Crear solución .NET 8 y proyectos iniciales.

---

*Roadmap de CATTECH OPTIMIZER PRO*  
*Documento vivo - se actualiza con el progreso del proyecto*
