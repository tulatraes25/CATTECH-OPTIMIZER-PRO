# Changelog - CATTECH OPTIMIZER PRO

Todos los cambios notables en este proyecto serán documentados en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- **Desactivación segura de programas de inicio** (StartupAnalysisView)
  - Checkboxes de selección con verificación de Microsoft
  - Botón "Desactivar seleccionados" con confirmación
  - Botón "Seleccionar posibles desactivar" para selects rápidos
  - Campo de motivo de desactivación (opcional)
  - Backup automático: registro a HKCU/HKLM\Software\CATTECH\OptimizerPro\DisabledStartup\Run
  - Backup automático: archivos a backups/startup/YYYYMMDD-HHMMSS/
  - Persistencia de backups en backups/startup/startup-backups.json
  - Panel de backups con listado, detalle y botón de restauración
  - Bloqueo de entradas de Microsoft (checkbox deshabilitado)
  - Bloqueo de fuentes no soportadas (RunOnce, tareas programadas)
  - Resultado parcial: exitosas, fallidas, omitidas (Microsoft/soporte)
  - Reversión desde backup con un click
- StartupBackupRecord: modelo completo con Id, EntryId, CanRestore, RestoredAt
- StartupActionResult: enum Success/Failed/SkippedMicrosoft/SkippedUnsupported/AlreadyDisabled/NotFound
- StartupDisableResult y StartupDisableSummary para resultados parciales
- SelectableStartupEntry: wrapper observable para selección en UI
- IStartupService: extendido con CanDisable/DisableSelected/Restore/ListBackups
- StartupService: implementación con Registry backup y FileSystem backup
- 10 tests nuevos (79 total)
- **Módulo de análisis de programas de inicio** (StartupAnalysisView)
  - Análisis de 6 fuentes: Registry Run/RunOnce (HKCU/HKLM), carpetas de inicio, tareas programadas
  - Clasificación automática: Microsoft vs terceros, riesgo, recomendación
  - Detección de editor por nombre/ruta (Microsoft, Google, NVIDIA, Intel, etc.)
  - Detección de rutas inexistentes, comandos sospechosos en Temp/AppData, duplicados
  - Filtros: Todos, No Microsoft, Revisar, Posible desactivar, Alertas
  - Búsqueda por nombre/ruta/editor
  - Panel de resumen: total, Microsoft, terceros, posibles desactivar, alertas
  - Panel de detalle al seleccionar entrada
  - Guardado en `data/startup-analysis/startup-analysis-YYYYMMDD-HHMMSS.json`
  - Preparado para futura desactivación con backup y reversión
- StartupEntry: modelo detallado con Status, Risk, Recommendation, Publisher, Notes
- StartupAnalysis: modelo con controles calculados (TotalCount, MicrosoftCount, etc.)
- IStartupService: interfaz con Analyze/Save/Load/List/Delete
- StartupService: implementación con Registry, FileSystem, WMI, clasificación
- StartupAnalysisViewModel con filtros, búsqueda, selección
- StartupAnalysisView con DataGrid, filtros, resumen, detalle
- StringToVisibilityConverter
- 14 tests nuevos (69 total)
- **Módulo de diagnóstico rápido** (QuickDiagnosticView)
  - Análisis no invasivo del sistema: SO, CPU, RAM, disco, inicio, seguridad, temporales
  - Detección de programas de inicio via Registry (HKCU/HKLM) y carpetas de inicio
  - Cálculo de tamaño de temporales (%TEMP%, C:\Windows\Temp) con timeout de 3s
  - Detección de antivirus via WMI (AntiVirusProduct), firewall, Windows Update
  - Detección de memoria virtual (Win32_PageFileUsage)
  - Alertas automáticas: RAM baja/justa, poco espacio, HDD, muchos startups, temporales altos
  - Guardado de diagnósticos en `data/diagnostics/diagnostic-YYYYMMDD-HHMMSS.json`
  - UI con grupos: Sistema, Hardware, Disco, Inicio, Seguridad, Temporales, Alertas
  - Barra de progreso durante el análisis
  - Colores por severidad: Info (azul), Warning (amarillo), Critical (rojo)
- DiagnosticReport: modelo completo con AlertSeverity, StartupInfo, TempFilesInfo, SecurityInfo
- IDiagnosticService: interfaz con Run/Save/Load/List/Delete
- DiagnosticService: implementación con WMI, Registry, FileSystem
- QuickDiagnosticViewModel con CommunityToolkit.Mvvm
- InverseBoolToVisibilityConverter
- 11 tests nuevos (55 total)
- **Módulo de cliente y equipo** (ClientEquipmentView)
  - Formulario de cliente: nombre, teléfono, email, empresa, dirección, observaciones
  - Formulario de equipo: marca, modelo, serie, tipo, motivo, observaciones
  - Detección automática no invasiva de hardware via WMI (SO, CPU, RAM, disco, etc.)
  - Estado de detección: Sin detectar / Detectando… / Detectado / Error
  - Persistencia de reportes en `data/service-reports/service-report-YYYYMMDD-HHMMSS.json`
  - CRUD: guardar, listar, cargar, eliminar reportes
  - Botón "Nuevo registro" para limpiar formulario
  - Validaciones: cliente obligatorio, motivo obligatorio, tipo de equipo, email
- JsonServiceReportService: servicio de persistencia para reportes de servicio
- IServiceReportService: interfaz con Save/Load/List/Delete
- ClientEquipmentViewModel con CommunityToolkit.Mvvm
- InvertBoolConverter para botón de detección
- 17 tests nuevos (44 total): ServiceReport serialization, CRUD de reportes
- **Módulo de configuración de empresa/técnico** (CompanySettingsView)
  - Formulario completo con 11 campos: nombre, técnico, CUIT/DNI, tel, WhatsApp, email, dirección, ciudad, logo, color, leyenda
  - Selección de logo con diálogo de archivos (PNG/JPG/JPEG)
  - Vista previa del logo y preview del color principal
  - Validaciones: nombre y técnico obligatorios, formato de email
  - Persistencia en `config/empresa.json` con camelCase
  - Detección de cambios sin guardar
  - Mensajes de éxito/error en la UI
- CompanySettingsViewModel con CommunityToolkit.Mvvm
- Converters: BoolToVisibility, LogoPathColor, HexToColor
- Tests unitarios: 27 tests (serialización, deserialización, validación email, persistencia JSON)
- Documentación inicial del proyecto
- AUDITORIA_REFERENCIAS.md - Análisis de herramientas de referencia
- ARQUITECTURA_INICIAL.md - Estructura y tecnologías del proyecto
- SEGURIDAD.md - Reglas de seguridad obligatorias
- ROADMAP.md - Plan de desarrollo por versiones
- CHANGELOG.md - Este archivo
- .gitignore - Archivos a ignorar en git
- Solución .NET 8 con proyectos Core, Infrastructure, UI y Tests
- Modelo de hardware (CPU, RAM, GPU, Disk, System, Motherboard)
- Modelo de reportes (Client, Equipment, Service)
- Modelo de configuración (Company, Technician, AppSettings)
- Interfaz IHardwareService con implementación WMI
- Interfaz IReportService e ISettingsService
- Servicio de persistencia JSON (JsonSettingsService)
- MainWindow WPF con navegación MVVM
- CommunityToolkit.Mvvm para patrón MVVM

### Fixed
- Corrección de rutas relativas en .sln (proyectos no se encontraban)
- Corrección de comandos de publicación en README.md (rutas incorrectas)
- Corrección de URL de clonado en README.md (tu-usuario → tulatraes25)
- Corrección de ManagementDateTime → ManagementDateTimeConverter en WmiHardwareService
- Corrección de variable no usada (ex) en JsonSettingsService
- Remoción de ApplicationIcon inexistente del .csproj
- JsonSettingsService: alineación de PropertyNamingPolicy (camelCase) en save/load

### Changed
- Compatibilidad documentada: solo Windows 10/11 para .NET 8
- SMART completo postergado a v0.2 (v0.1 solo detección básica)
- CompanyInfo extendido con campos: TechnicianName, TaxId, WhatsApp, City, LogoPath, PrimaryColor, FooterLegend
- Ruta de configuración cambiada de appsettings.json a empresa.json
- Build verificado: dotnet restore, build y test exitosos

---

## [0.1.0] - 2024-XX-XX (Futuro)

### Added
- Configuración de empresa/técnico con logo
- Formulario de datos de cliente/equipo
- Diagnóstico básico de hardware
- Lista de programas de inicio (solo lectura)
- Limpieza segura de archivos temporales
- Optimización visual segura
- Creación de puntos de restauración
- Generación de informes HTML
- Generación de informes PDF
- Versión portable (sin instalador)

### Security
- Confirmación obligatoria antes de cambios
- Puntos de restauración pre-operación
- Logging detallado de acciones
- Lista blanca de optimizaciones seguras

---

## [0.2.0] - 2024-XX-XX (Futuro)

### Added
- SMART completo con análisis de atributos
- Monitoreo en tiempo real de sensores
- Dashboard con gráficos de temperatura
- Reportes comparativos (antes/después)
- Exportación a JSON/CSV/XML
- Modo oscuro

### Changed
- Mejora en rendimiento de detección de hardware
- Optimización de uso de memoria

---

## [0.3.0] - 2024-XX-XX (Futuro)

### Added
- Análisis básico de minidumps (BSOD)
- Decodificación de Bug Check Codes
- Reparación de Windows Update
- Reparación de archivos del sistema (SFC/DISM)
- Historial de servicios por cliente

### Security
- Validación adicional en reparaciones
- Logging extendido para auditoría

---

## [0.4.0] - 2024-XX-XX (Futuro)

### Added
- CATTECH Preserve - Backup de configuración
- Restauración selectiva de configuración
- Backup de drivers críticos
- Sincronización entre equipos
- Exportación/Importación desde USB

---

## [0.5.0] - 2024-XX-XX (Futuro)

### Added
- CATTECH Rescue USB
- Creación de USB bootable con WinPE
- Integración de Memtest86+
- Herramientas de diagnóstico offline
- Recuperación de archivos básica
- Reparación de arranque

---

## [1.0.0] - 2024-XX-XX (Futuro)

### Added
- Suite completa estable
- Instalador profesional (Inno Setup)
- Versión portable
- Actualizaciones automáticas
- Documentación completa
- Guía de usuario
- Guía del técnico
- Soporte multi-idioma (ES, PT, EN)

### Changed
- Optimización final de rendimiento
- Corrección de todos los bugs conocidos

---

## Convenciones

### Tipos de Cambios

- **Added**: Para nuevas funcionalidades.
- **Changed**: Para cambios en funcionalidades existentes.
- **Deprecated**: Para funcionalidades que serán removidas.
- **Removed**: Para funcionalidades removidas.
- **Fixed**: Para bugs corregidos.
- **Security**: Para vulnerabilidades de seguridad.

### Versionado

- **Major** (X.0.0): Cambios incompatibles con versiones anteriores.
- **Minor** (0.X.0): Nuevas funcionalidades compatibles.
- **Patch** (0.0.X): Corrección de bugs compatibles.

---

*Registro de cambios de CATTECH OPTIMIZER PRO*
