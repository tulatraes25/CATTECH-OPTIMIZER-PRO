# Changelog - CATTECH OPTIMIZER PRO

Todos los cambios notables en este proyecto serán documentados en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
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

### Changed
- Compatibilidad documentada: solo Windows 10/11 para .NET 8
- SMART completo postergado a v0.2 (v0.1 solo detección básica)
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
