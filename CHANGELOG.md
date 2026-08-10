# Changelog - CATTECH OPTIMIZER PRO

Todos los cambios notables en este proyecto serán documentados en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.1.1] - 2026

**MVP estabilizado** - CATTECH OPTIMIZER PRO v0.1.1

### Funcionalidades incluidas

- **Configuración de empresa/técnico**: Formulario con 11 campos, logo, persistencia JSON
- **Cliente y equipo**: Formulario completo, detección automática de hardware via WMI
- **Diagnóstico rápido**: Análisis no invasivo del sistema con alertas automáticas
- **Programas de inicio**: Análisis de 6 fuentes, clasificación Microsoft/terceros
- **Desactivación segura de inicio**: Backup, reversión, solo fuentes soportadas
- **Limpieza de temporales**: Escaneo previo, selección, borrado seguro
- **Optimización visual**: 8 ajustes predefinidos con backup y reversión
- **Punto de restauración**: Verificación de permisos, creación via PowerShell/WMI
- **Informe HTML profesional**: 9 secciones, logo embebido, recomendaciones automáticas
- **Exportación a PDF**: Via Microsoft Edge headless, fallback si no disponible
- **164 tests unitarios pasando**

### Cambios técnicos

- Refactorización: GetTechnicianNameAsync extraído a SettingsHelper compartido
- Eliminación de duplicación de código en 5 servicios
- Eliminación de carpetas vacías (Infrastructure/External, Infrastructure/Templates)
- Documentación QA completa en docs/QA_V0_1_1.md

### Correcciones

- Corregida referencia a AppSettings en SettingsHelper
- Verificada compilación sin errores (0 errores)
- Verificados 164 tests pasando (100%)

### Fixed (PDF)
- **PDF ahora genera archivos PDF reales** (no HTML renombrado)
- Implementación corregida: Edge headless `--print-to-pdf` en lugar de fallback a HTML
- Validación de cabecera `%PDF` en archivos generados
- Detección robusta de Microsoft Edge: rutas comunes + where.exe
- Mensajes de error corregidos: Edge, no WebView2
- 16 tests de validación PDF y Edge (164 total)

### Documentation
- Eliminadas todas las menciones a QuestPDF (no es dependencia actual)
- Corregida documentación de PDF: Edge headless, no WebView2
- README, CHANGELOG, QA, RELEASE_NOTES, ARQUITECTURA actualizados
- Fechas corregidas de 2024 a 2026 en documentación principal
- Versión actualizada de v0.1.0 a v0.1.1 en UI e informes
- README: versión actualizada a "v0.1.1 MVP estabilizado"
- Detección de Edge unificada (rutas comunes + PATH)
- Duplicado en release notes eliminado

---

## [Unreleased]

Cambios posteriores a v0.2.1.

---

## [0.2.1] - 2026-08-10

### Fixed (SMOKE-B1-001 — XAML StaticResource)
- Corregido crash al navegar a Cliente/equipo: `ClientEquipmentView.xaml` usaba `{StaticResource InvertBoolConverter}` sin registrarlo en sus Resources → XamlParseException al instanciar la vista (detectado en smoke real v0.2.0, 2/2 reproducible)
- Auditoría StaticResource de las vistas: se corrigieron además QuickDiagnosticView (InvertBoolConverter), VisualOptimizationView (RestoreColorConverter/RestoreTextConverter) y CompanySettingsView (HexToColorConverter/LogoPathColorConverter) — mismo defecto latente
- Agregados smoke tests WPF STA (`XamlViewSmokeTests`): carga de ClientEquipmentView, navegación MainViewModel → Cliente/equipo, y carga de las 10 vistas del sidebar sin DataContext/comandos — previenen regresiones de StaticResource en CI
- 3 tests nuevos (948 total)

### Fixed (QA-ENC-001)
- Corregida regresión de encoding UTF-8 en tres vistas (Configuración, Diagnóstico, Optimización) introducida durante la auditoría XAML del fix SMOKE-B1-001; textos y emojis restaurados sin perder los registros StaticResource corregidos
- Diferencias vs la base limpia limitadas exclusivamente a los 6 registros de converters (ClientEquipment 1, CompanySettings 2, QuickDiagnostic 1, VisualOptimization 2); sin mojibake restante en los 12 XAML del proyecto

### Quality / QA
- Smoke tests WPF STA (`XamlViewSmokeTests`): carga de ClientEquipmentView, navegación MainViewModel → Cliente/equipo y carga de las 10 vistas del sidebar
- 948 tests (0 failed, 0 skipped); CI Windows en cada push a main
- El smoke real sobre v0.2.0 detectó el blocker SMOKE-B1-001 antes de preparar esta versión
- Provenance guard del release pipeline: candidate exige `ProductVersion == {version}+{source_sha}` exacto

### Infrastructure
- CI de Windows mediante GitHub Actions (`CATTECH CI`): restore/build/tests Release automáticos en push a main, pull requests contra main y ejecución manual; resultados TRX conservados como artifact
- Workflow reutilizable de release packaging (`CATTECH Release Package`): validación de consistencia (csproj, versiones visibles, semver), doble ejecución de tests, publish win-x64 self-contained, verificación EXE/config/smartctl, ZIP + SHA-256 como artifacts temporales
- Workflow de publicación controlada (`CATTECH Release`): dry-run seguro por defecto, tag anotado vía API, reutilización del release package, checksums revalidados, build provenance mediante artifact attestation, permisos write limitados al job publish
- Kit de smoke/QA real en Windows: `scripts/qa/Collect-SmokeEvidence.ps1` (collector read-only sin PII, schema v2), `docs/QA_REAL_SMOKE.md`, `docs/QA_SMOKE_RESULT_TEMPLATE.md`
- Documentación: `docs/RELEASE_AUTOMATION.md`

---

## [0.2.0] - 2026-08-09

### Added
- **Integración base smartctl** (Fase A.1)
  - `SmartctlRunner`: ejecuta smartctl como binario externo (no integra GPL)
  - `SmartctlParser`: parseo de salida JSON y texto de smartctl
  - Detección de ubicación: config, tools/, PATH, rutas comunes
  - Verificación de versión y soporte JSON
  - Listado de dispositivos con detección de tipo (HDD/SSD/NVMe/USB)
  - Configuración en `config/herramientas.json`
- **Análisis SMART completo read-only** (Fase A.2/A.3)
  - `SmartDiskService`: ejecuta `smartctl -a -j` por disco
  - `SmartDiskReport`: reporte completo con estado Good/Warning/Critical
  - `SmartAttribute`: atributos SMART relevantes con severidad
  - `SmartAnalysisResult`: resultado del análisis de todos los discos
  - Parseo de atributos ATA (Reallocated, Pending, CRC, Temperature)
  - Parseo de atributos NVMe (critical_warning, percentage_used, media_errors)
  - Cálculo automático de estado: Good/Warning/Critical/NotAvailable
  - Persistencia en `data/smart-reports/`
- **Pantalla Discos SMART** (Fase A.4)
  - Verificación de smartctl con estado y ruta
  - Detección de dispositivos de almacenamiento
  - Análisis SMART completo con tabla de resultados
  - Panel de detalle con atributos, advertencias y errores
  - Colores semáforo: Bueno/Precaución/Crítico/No disponible
  - Guardado de análisis en data/smart-reports/
  - Tabla de dispositivos detectados (Dispositivo, Tipo, Protocolo, Modelo, Serie)
  - Advertencia de solo lectura y "No disponible no significa sano"
  - Ayuda si smartctl no está disponible

### Fixed (SMART UI)
- **Métricas SMART corregidas**: Reasign./Pendientes ahora muestran RawValue real de IDs 5/197, no ImportantAttributes.Count
- **Propiedades calculadas**: ReallocatedSectorCount, PendingSectorCount, OfflineUncorrectableCount, UDMACrcErrorCount
- **Métricas NVMe estructuradas**: NvmePercentageUsed, NvmeAvailableSpare, NvmeMediaErrors, NvmeUnsafeShutdowns
- **Estados corregidos**: NotAvailable/Unknown ya no muestran "Todos los discos en buen estado"
- **SummaryUnknown**: contador agregado al resumen
- **Preservación de resultado**: SaveAnalysisAsync conserva SmartAnalysisResult original (timestamps, errors, warnings)
- **HasDevices vs HasResults**: conceptos separados correctamente
- 15 tests nuevos (243 total)

### Added (SMART Test Corto - Fase A.5)
- **SmartTestService**: inicia self-test SMART corto via `smartctl -t short -j`
- **SmartTestSession**: sesión con estado, duración estimada, progreso
- **SmartTestResult**: resultado final con estado y mensaje legible
- **SmartTestStatus**: NotStarted/Starting/InProgress/Completed/Aborted/Interrupted/Unsupported/FailedToStart/Unknown
- **Consulta de estado**: via `smartctl -l selftest -j` (solo lectura)
- **Reglas de seguridad**: disco crítico bloqueado, no segundo test InProgress, verificación de soporte
- **UI**: panel "Test SMART" con botones Ejecutar/Consultar estado
- **Persistencia**: sesiones en data/smart-tests/
- 31 tests nuevos (274 total)

### Fixed (Arquitectura test SMART)
- **Eliminada dependencia UI → SmartctlParser**: SmartDiskViewModel ahora usa modelos Core (ToDisplayMessage)
- **Parseo estructurado**: SmartTestStartParseResult tipado en lugar de tupla con texto localizado
- **Detección por exit_status**: Unsupported (4), permisos (2), fallo (3) sin depender de texto inglés
- **Capacidad self-test**: SupportsSelfTest, SupportsShortSelfTest, SupportsExtendedSelfTest, SelfTestSupportKnown en SmartDiskReport
- **Errores temporales de consulta**: LastCheckSucceeded/LastCheckError; timeout NO finaliza el test
- **Conservación de datos**: StartedAt, EstimatedCompletionAt y warnings se preservan tras errores temporales
- 13 tests nuevos (287 total)

### Added (SMART Extended Self-Test - Fase A.6)
- **StartExtendedTestAsync**: inicia self-test extendido via `smartctl -t long -j`
- **Refactor StartTestAsync común**: Short y Extended reutilizan la misma lógica interna
- **Seguridad extendida**: Critical bloqueado, soporte verificado, un test a la vez
- **Confirmación fuerte en UI**: advertencia de duración prolongada y backup
- **Tipo de test preservado**: sesiones Extended no se convierten en Short
- **GetLatestResultAsync**: detecta tipo desde el log (Extended offline)
- **Bloqueo mutuo**: Short bloquea Extended y viceversa mientras hay test activo
- 17 tests nuevos (304 total)

### Added (SMART en Informe Técnico - Fase A.7.1)
- **Sección "Estado SMART de Discos"** en informe HTML: usa resultados persistidos, NO ejecuta smartctl
- **ReportGenerationOptions**: SmartAnalysis + IncludeSmart
- **ReportViewModel**: carga análisis SMART persistidos via ListResultsAsync (sin AnalyzeAllDisksAsync)
- **BuildReportOptionsAsync único**: GenerateReportAsync y ExportPdfAsync usan la misma fuente lógica
- **UI**: checkbox "Estado SMART de discos" + selector "Análisis SMART" con DisplayName
- **Resumen general**: fecha, smartctl version, discos, buenos/precaución/críticos/no disponibles/desconocidos
- **Detalle por disco**: info básica, estado con clase CSS, métricas ATA y NVMe estructuradas
- **NotAvailable explícito**: "SMART no disponible. Esto no permite confirmar que el disco esté sano."
- **Backup recomendado**: indicador claro en discos con RequiresBackupRecommendation
- **IncludedSections**: agrega "SMART" cuando corresponde
- 19 tests nuevos (323 total)

### Fixed (SMART en Informe)
- **Métricas ATA/NVMe corregidas**: NVMe ya no muestra métricas ATA irrelevantes (sectores reasignados, pendientes, offline, CRC)
- **Detección de tipo**: NVMe tiene prioridad sobre ATA; un NVMe con temperatura/horas no se clasifica como ATA
- **Métricas comunes sin duplicación**: temperatura y horas de uso se muestran una sola vez
- **Backup para NotAvailable/Unknown**: "Backup recomendado: No determinado" con aclaración, nunca "Backup: No"
- **Clase visual neutra**: smart-backup-unknown para estados no determinados
- 14 tests nuevos (337 total)

### Fixed (Persistencia SmartTestSession)
- **Filename determinístico**: `smart-test-{type}-{RequestedAt}-{Id}.json` siempre, la misma sesión sobrescribe el mismo archivo
- **Compatibilidad legacy**: ListSessionsAsync sigue leyendo formatos antiguos sin Id (solo lectura)
- **Deduplicación por Id**: se agrupa por SmartTestSession.Id ANTES de aplicar maxResults
- **Snapshot más reciente**: se elige por fecha efectiva (LastCheckedAt → CompletedAt → StartedAt → RequestedAt)
- **Orden descendente**: más reciente → más antiguo
- **JSON corrupto**: se omite sin romper el listado
- 17 tests nuevos (354 total)

### Added (Self-Tests SMART en Informe - Fase A.7.2a)
- **Sección "Pruebas SMART (Self-Test)"**: sesiones Short/Extended persistidas en el informe HTML/PDF
- **Selección manual**: wrapper SmartTestSessionSelectionItem (solo UI) con IsSelected y DisplayName
- **Instancia compartida**: SmartTestService creado UNA vez en MainViewModel, usado por SmartDisk y Report
- **Carga**: solo `ListSessionsAsync(20)`, sin ejecutar smartctl ni consultar estado
- **Builder único**: BuildReportOptionsAsync filtra solo sesiones seleccionadas
- **Mensajes de incertidumbre**: InProgress/Unsupported/FailedToStart/Aborted/Interrupted/Unknown con aclaraciones
- **Última consulta fallida**: LastCheckSucceeded=false genera advertencia
- **Orden**: más reciente → más antigua en el HTML
- 29 tests nuevos (383 total)

### Added (Recomendaciones SMART/self-test - Fase A.7.2b)
- **ReportRecommendationEngine extendido**: genera recomendaciones automáticas desde el análisis SMART persistido y los self-tests seleccionados
- **SMART por estado**: Critical → backup prioritario y evaluación de reemplazo; RequiresBackupRecommendation → backup sin duplicar; Warning → revisar indicadores; NotAvailable/Unknown → estado no concluyente (no asume salud); Good → sin recomendación
- **Self-test por estado**: CompletedWithError → Critical (backup/evaluación); InProgress/Starting → Info (esperar resultado final); Unsupported → Info (no determina salud); FailedToStart → Warning (sin afirmar falla física); Aborted/Interrupted/Unknown/NotStarted → Info no concluyente; CompletedWithoutError → sin recomendación
- **Última consulta fallida**: LastCheckSucceeded=false genera Warning de estado posiblemente desactualizado (sin filtrar el error técnico interno)
- **Fuentes respetadas**: recomendaciones SMART solo si IncludeSmart+SmartAnalysis; self-tests solo si IncludeSmartTests+sesiones seleccionadas
- **Sin thresholds nuevos**: el engine confía en HealthStatus/RequiresBackupRecommendation/Status; no reinterpreta atributos raw
- **Etiquetas legibles**: ModelName → DeviceName → Device
- 35 tests nuevos (418 total)

### Added (Fundación sensores de temperatura - Fase B.1.1)
- **Dependencia**: LibreHardwareMonitorLib 0.9.6 (NuGet, MPL 2.0); System.Management actualizado de 8.0.0 a 10.0.2 (requisito mínimo de LHM 0.9.6, sin NU1605/downgrade)
- **IHardwareSensorService** (Core): `GetTemperatureSnapshotAsync(CancellationToken)` — capa separada de IHardwareService/WMI
- **Modelos Core sin dependencia de LHM**: HardwareTemperatureSensor (HardwareName, HardwareType string propio, SensorName, SensorIdentifier, Value/Min/Max nullable) y HardwareTemperatureSnapshot (CapturedAt, IsAvailable, IsElevated, Sensors, Warnings, Errors, HasSensors, ValidSensorCount)
- **LibreHardwareSensorService**: read-only, CPU/GPU/Memory/Motherboard/Storage/Controller habilitados, recorrido recursivo de SubHardware, filtro exclusivo por SensorType.Temperature
- **Normalización**: null se conserva como null (nunca 0 °C); NaN/Infinity → null
- **Deduplicación por SensorIdentifier** (no por nombre); fallback id hardware + nombre
- **Permisos**: IsElevated detectado; sin elevación → warning informativo, no error fatal
- **Tolerancia a errores**: fallo de un hardware no pierde sensores válidos de otros; Open fallido → IsAvailable=false con errores controlados
- **Ciclo de vida**: Computer → Open → Accept(visitor) → lectura → Close en finally; HardwareUpdateVisitor propio (UpdateVisitor no es público en 0.9.6)
- **Async-compatible**: lectura sincrónica en background (Task.Run), respeta CancellationToken
- **Testabilidad**: abstracción interna IHardwareMonitorFactory/IHardwareMonitorSession/IHardwareNode/ISensorNode + InternalsVisibleTo; tests con fakes, sin acceso a hardware real
- 22 tests nuevos (440 total)

### Added (Muestreo repetido - Fase B.1.2)
- **WatchTemperatureSnapshotsAsync(interval, ct)**: async stream que reutiliza UNA sola sesión abierta; primera muestra inmediata, Refresh por muestra, espera el intervalo entre muestras
- **IHardwareMonitorSession.Refresh()**: responsabilidades separadas — Create() abre, Refresh() actualiza valores, Dispose() cierra Computer
- **LibreHardwareMonitorSession.Refresh**: `_computer.Accept(HardwareUpdateVisitor)` reutilizando el mismo visitor; no recrea ni reabre Computer
- **Leak corregido en Create**: si Open() o la construcción de la sesión falla, se intenta Close() seguro antes de relanzar
- **Ciclo de vida del stream**: try/finally dispone la sesión exactamente una vez al cancelar, interrumpir (break), fallar o agotar la enumeración
- **Validación de intervalo**: interval <= 0 lanza ArgumentOutOfRangeException (sin inventar mínimos/máximos ni default)
- **Fallos tolerantes**: Create fallido → UNA muestra IsAvailable=false y el stream termina (sin retry); Refresh fallido → muestra IsAvailable=false sin sensores stale, el siguiente intento puede recuperarse sobre la misma sesión
- **Muestras independientes**: objeto nuevo por muestra, lista Sensors nueva, deduplicación por SensorIdentifier reiniciada por snapshot
- **Min/Max reflejados tal cual informa el proveedor** (sin agregados propios)
- **Sin concurrencia**: muestras estrictamente secuenciales (Refresh N → Read N → Delay → Refresh N+1), sin Parallel
- **Delay testeable**: IHardwareMonitorDelay interno (TaskDelay en prod, fake inmediato en tests); sin paquete NuGet adicional
- **Error parcial por nodo**: sensores y subhardware encapsulados de forma tolerante; IsAvailable=true con error parcial si el monitor sigue abierto
- **Sin hot-plug**: la lista raíz de hardware se mantiene estable durante la sesión
- 25 tests nuevos (465 total)

### Added (UI de temperaturas - Fase B.1.3)
- **HardwareView funcional**: reemplaza el placeholder "Información de Hardware"; encabezado con subtítulo y nota "Lectura mediante LibreHardwareMonitor — solo lectura"
- **HardwareViewModel**: depende solo de IHardwareSensorService (Core), sin referencia a LibreHardwareMonitorLib
- **Actualizar una vez**: GetTemperatureSnapshotAsync con IsBusy; aplica el snapshot a la UI
- **Iniciar monitoreo / Detener**: WatchTemperatureSnapshotsAsync con cadencia visual de 2 segundos; una sola enumeración activa (CTS propio); Stop idempotente
- **Cancelación al navegar**: MainViewModel detiene el monitoreo si se abandona la sección Hardware (no queda LHM abierto oculto)
- **ApplySnapshot único**: Clear de sensores por muestra (sin datos stale); orden estable Tipo → Hardware → Sensor; deduplicación respetada
- **Valores N/D**: NullableTemperatureConverter — null → "N/D" (nunca 0 °C); valor → "48,2 °C"
- **Resumen**: sensores detectados, con lectura válida, proveedor, modo administrador
- **Estados**: Sin lectura / Leyendo... / Monitoreando / Lectura disponible / Lectura no disponible / Sin sensores disponibles
- **Warnings y Errors** del snapshot visibles; sin elevación → aviso informativo, sin UAC, sin cambiar app.manifest
- **Recuperación**: muestra IsAvailable=false vacía la tabla sin detener el stream; la siguiente muestra disponible recupera la UI
- **Sin colores de salud**: diseño neutral; sin thresholds ni estados Hot/Critical
- **Sin inicio automático**: al entrar a Hardware se pide acción explícita del técnico
- Tests UI: proyecto de tests migrado a net8.0-windows con referencia a UI + InternalsVisibleTo
- 27 tests nuevos (492 total)

### Added (Métricas dinámicas CPU/GPU - Fase B.2.1)
- **HardwareLiveSnapshot** (Core): captura única con TemperatureSensors + PerformanceSensors de UN mismo Refresh (coherencia temporal); HasTemperatureSensors/HasPerformanceSensors/Valid*Count calculados
- **HardwarePerformanceSensor** (Core): HardwareName, HardwareType, SensorName, SensorIdentifier, MetricType, Value/Min/Max, Unit
- **HardwarePerformanceMetricType** (Core): Load y Clock — sin exponer enums de LibreHardwareMonitor
- **Unidades**: Load → "%", Clock → "MHz" (sin convertir a GHz en Core)
- **InternalSensorType extendido**: Temperature, Load, Clock, Other — mapeo exclusivo por SensorType (sin heurística por nombre)
- **Filtro CPU/GPU**: PerformanceSensors solo de hardware Cpu/Gpu; Load/Clock de memoria/almacenamiento/placa/controladores se ignoran; temperaturas con alcance sin cambios
- **API pública**: GetLiveSnapshotAsync + WatchLiveSnapshotsAsync (futuras APIs de B.4, una sola sesión)
- **Refactor del núcleo**: un único ciclo Create → Refresh → captura completa → Delay → Dispose; las APIs de temperatura proyectan desde el LiveSnapshot conservando warnings/errors y semántica de fallos
- **Una sola pasada**: CollectSensors recorre el hardware una vez construyendo ambas listas; dedup por SensorIdentifier reiniciado por snapshot
- **Política null/NaN/Infinity** idéntica a B.1 para Value/Min/Max
- **Sin interpretación**: sin thresholds, sin estados de rendimiento, sin elección de sensor principal, sin selección por nombre
- **Sin cambios de UI**: HardwareView sigue mostrando solo temperaturas
- 40 tests nuevos (532 total)

### Added (Memoria GPU SmallData - Fase B.2.2)
- **HardwareGpuMemorySensor** (Core): HardwareName, HardwareType, SensorName, SensorIdentifier, ValueMB/MinMB/MaxMB nullable, Unit => "MB"
- **Sin semántica inferida**: no hay UsedMB/FreeMB/TotalMB/DedicatedMB/SharedMB/UsagePercent — CATTECH conserva lo que informa el proveedor
- **HardwareLiveSnapshot**: + GpuMemorySensors, HasGpuMemorySensors, ValidGpuMemorySensorCount
- **InternalSensorType**: + SmallData (mapeo exclusivo por SensorType, sin heurística por nombre)
- **Filtro GPU**: SmallData solo de hardware Gpu; CPU/Memoria/Almacenamiento/Placa Madre se ignoran; SensorType.Data no se incorpora (B.3 tratará RAM)
- **Mismo Refresh**: temp + load + clock + SmallData con Create=1, Refresh=1, Dispose=1
- **Sin conversiones**: el valor SmallData se conserva tal cual (no MB→GB, no redondeos, no clamps)
- **Política null/NaN/Infinity** idéntica para ValueMB/MinMB/MaxMB
- **Nombres preservados literalmente**: "GPU Memory Used/Free/Total", "D3D Dedicated/Shared Memory Used" se capturan sin lógica especial
- **Stream**: WatchLiveSnapshotsAsync incluye GpuMemorySensors por muestra; fallos vacían las tres listas y se recuperan
- 37 tests nuevos (569 total)

### Added (Telemetría de batería - Fase B.2.3)
- **Batería habilitada en LHM**: IsBatteryEnabled=true en la MISMA instancia Computer (configuración testeable: EnabledHardwareConfiguration)
- **HardwareBatteryMetricType** (Core): Level, Energy, Voltage, Current, Power, TimeSpan
- **HardwareBatterySensor** (Core): HardwareName, HardwareType, SensorName, SensorIdentifier, MetricType, Value/Min/Max nullable, Unit
- **Unidades**: Level → "%", Energy → "mWh", Voltage → "V", Current → "A", Power → "W", TimeSpan → "s" (sin conversiones mWh→Wh ni s→min)
- **HardwareLiveSnapshot**: + BatterySensors, HasBatterySensors, ValidBatterySensorCount
- **InternalSensorType**: + Level/Energy/Voltage/Current/Power/TimeSpan (mapeo exclusivo por SensorType)
- **Filtro Battery**: telemetría no térmica solo de hardware Battery; CPU Power, GPU Power, Motherboard Voltage se ignoran
- **Battery Temperature**: entra en TemperatureSensors con HardwareType "Batería" (no se duplica en BatterySensors)
- **Mismo Refresh**: CPU temp/load/clock + GPU temp/load/clock/smalldata + battery temp/level/energy/power con Create=1, Refresh=1, Dispose=1
- **Sin heurísticas por nombre**: "Charge Level", "Degradation Level", "Charge/Discharge Rate", "Remaining Time" preservados literalmente; sin IsCharging/IsDischarging/salud/degradación calculada
- **Multibatería**: dos baterías coexisten (dedup por SensorIdentifier, nunca por SensorName)
- **PC sin batería**: IsAvailable=true, BatterySensors vacío, sin error (caso normal desktop)
- **Stream**: WatchLiveSnapshotsAsync incluye BatterySensors por muestra; fallos vacían las CUATRO listas y se recuperan
- **Backend B.2 completado**: adquisición de datos de CPU/GPU/Batería cerrada; presentación avanzada en B.4
- 51 tests nuevos (620 total)

### Added (Inventario RAM avanzado - Fase B.3.1)
- **MemoryModuleInfo** (Core): DeviceLocator, BankLabel, Manufacturer, PartNumber, SerialNumber, CapacityBytes (ulong exacto), ConfiguredClockSpeedMHz, SMBIOSMemoryTypeCode, MemoryType, DataWidthBits, TotalWidthBits, Rank + CapacityGB calculada (1024^3)
- **MemoryInfo extendido**: + Modules y HasModuleDetails; SpeedMHz/Type/SlotsUsed/SlotsTotal conservan firma (sin romper consumidores)
- **WMI/SMBIOS**: Win32_PhysicalMemory (11 campos), Win32_PhysicalMemoryArray (MemoryDevices, Use), Win32_ComputerSystem (TotalPhysicalMemory), Win32_OperatingSystem (FreePhysicalMemory) — solo campos requeridos, namespace root\CIMV2
- **Tipos SMBIOS verificados contra spec DMTF DSP0134 §7.18.2** (confirmado por EDK2 SmBios.h y dmidecode): DDR=18, DDR2=19, DDR3=24, DDR4=26, LPDDR=27, LPDDR2=28, LPDDR3=29, LPDDR4=30, DDR5=34, LPDDR5=35; código desconocido → "Desconocida" conservando el raw
- **Sin inventar por velocidad**: el tipo nunca se deduce de la velocidad; la velocidad configurada se guarda tal cual (0/null → null)
- **Resumen SpeedMHz**: valor único si todos los válidos coinciden, 0 si distintos o ausentes (nunca máx/mín/promedio)
- **Resumen Type**: uniforme, "Mixta" si hay tipos reconocidos distintos, "Desconocida" sin información
- **SlotsUsed**: solo módulos con CapacityBytes > 0; **SlotsTotal**: suma de MemoryDevices de arrays Use==3 (System Memory); inconsistencia SlotsUsed > SlotsTotal NO se corrige
- **Strings con Trim**; sin normalización de fabricantes; **Rank** desde Attributes (>0 → valor, 0/null → null); sin inferencia ECC
- **Testabilidad**: IWmiMemoryReader + WmiMemorySnapshot + constructores internal en WmiHardwareService; tests con fake reader, sin WMI real
- **Tolerancia**: módulos parciales conservados; reader fallido → MemoryInfo vacío sin propagar excepción
- 40 tests nuevos (660 total)

### Added (Timings SPD - Fase B.3.2)
- **HardwareMemoryTimingSensor** (Core): HardwareName, HardwareIdentifier, HardwareType, SensorName, SensorIdentifier, ValueNanoseconds/MinNanoseconds/MaxNanoseconds nullable, Unit = "ns"
- **Sin semántica inventada**: no hay CASLatency/CasLatencyCycles/CL/TRCD/TRP/TRAS/TRC/TRFC/TimingProfile/XmpProfile/ExpoProfile; los nombres se conservan literalmente ("tAA (CAS Latency Time)", "tRCD...", etc.)
- **HardwareLiveSnapshot**: + MemoryTimingSensors, HasMemoryTimingSensors, ValidMemoryTimingSensorCount
- **InternalSensorType**: + Timing mapeado exclusivamente por SensorType (sin heurística por nombre); SensorType.Data NO se incorpora (queda Other — el inventario RAM ya lo cubre WMI/SMBIOS)
- **Filtro Memory**: timings solo de hardware Memory; CPU/GPU/Battery Timing se ignoran; Temperature de DIMM sigue en TemperatureSensors (no duplicada)
- **Valores en ns preservados**: sin conversión a ciclos, sin calcular CL (tAA=14.0 significa 14.0 ns, no CL14), sin redondeo, sin clamps
- **Vista dinámica de sesión real**: LibreHardwareMonitorSession.Hardware ahora proyecta `_computer.Hardware` en cada consulta (antes: lista congelada en Create) — permite observar DIMM que MemoryGroup agrega tras Open(); no es hot-plug general de CATTECH, sin rescan ni eventos propios
- **SPD tardío**: sin sleeps/reintentos propios; WatchLive puede incorporar timings en snapshots futuros con CreateCount=1
- **SPD ausente**: lista vacía sin error específico (RAM defectuosa no se asume); sin UAC, sin instalar drivers — solo el comportamiento de LHM 0.9.6; auditoría de dependencia: RAMSPDToolkit-NDD 1.4.2 ya es transitiva del paquete instalado (verificado en nuspec y project.assets.json)
- **Mismo Refresh**: CPU/GPU/Battery/MemoryTiming con Create=1, Refresh=1, Dispose=1; fallos vacían las cinco listas y se recuperan; error parcial de DIMM conserva las demás familias
- **Sin correlación WMI ↔ SPD**: HardwareIdentifier preservado sin matching por slot/part number/serial
- 39 tests nuevos (699 total)

### Added (UI live avanzada - Fase B.4.1)
- **HardwareViewModel migrado a LiveSnapshot**: RefreshCommand usa GetLiveSnapshotAsync; RunMonitoringAsync usa WatchLiveSnapshotsAsync (2 s) — UNA sola sesión/captura por muestra alimenta las 5 secciones; sin streams paralelos
- **5 colecciones live** (modelos Core directos): TemperatureSensors, PerformanceSensors, GpuMemorySensors, BatterySensors, MemoryTimingSensors
- **ApplyLiveSnapshot**: limpia las 5 familias por muestra (sin datos stale); IsAvailable=false → las 5 vacías; warnings/errors reemplazados por los del snapshot actual
- **Órdenes estables**: temp (Tipo→Hardware→Sensor), performance (Tipo→Hardware→Métrica→Sensor), GPU mem (Hardware→Sensor), battery (Hardware→Métrica→Sensor), timing (Hardware→Sensor); sin ordenar por valor
- **Estado global**: HasLiveData (cualquier familia con datos); StatusText: Lectura no disponible / Lectura disponible / Sin sensores disponibles / Monitoreando / Leyendo...; HasSensors y ValidSensorCount siguen siendo específicos de TEMPERATURA
- **Contadores por familia**: Performance/GpuMemory/Battery/MemoryTiming (totales y válidos) sin porcentajes de disponibilidad
- **HardwareView rediseñada**: subtítulo "Monitoreo de hardware en tiempo real"; TabControl con 5 pestañas (Temperaturas, CPU / GPU, Memoria GPU, Batería, RAM SPD); tarjetas compactas de resumen por familia (solo cantidad de sensores); warnings/errors fuera del TabControl
- **Converters**: NullableNumberConverter (null/NaN/Infinity → N/D, hasta 2 decimales según culture, sin unidad), PerformanceMetricTypeTextConverter (Load→Carga, Clock→Frecuencia), BatteryMetricTypeTextConverter (Level→Nivel, Energy→Energía, Voltage→Voltaje, Current→Corriente, Power→Potencia, TimeSpan→Tiempo) — solo presentación, nunca por SensorName
- **Pestañas vacías neutrales**: "No se detectaron métricas dinámicas disponibles.", "No se detectó telemetría de batería aplicable.", "No se detectaron timings SPD disponibles." etc.; batería vacía en desktop y SPD vacío NO son errores
- **Timings mostrados en ns** (14,00 | ns — nunca CL14); TimeSpan de batería en segundos sin convertir
- **Sin interpretación**: sin thresholds, sin colores de salud, sin CPU Total/GPU Core seleccionado, sin VRAM usage, sin salud de batería, sin CL calculado; sin WMI (IHardwareService NO inyectado)
- Tests ViewModel adaptados a la migración live + 19 tests netos nuevos (718 total)

### Added (Inventario estático WMI/SMBIOS en UI - Fase B.4.2)
- **HardwareViewModel recibe IHardwareService** (instancia compartida de MainViewModel, sin DI global); toda la lógica live de B.4.1 intacta
- **RefreshInventoryCommand** ("Actualizar inventario"): consulta manual e independiente; IsInventoryBusy propio (bloquea doble ejecución); no auto-carga al entrar ni durante monitoreo
- **Sin WMI durante el stream live**: WatchLiveSnapshotsAsync/RefreshAsync nunca llaman IHardwareService (tests de separación con contadores); inventario puede ejecutarse mientras monitorea sin crear segundo stream
- **WMI fuera del hilo UI**: las 4 consultas (CPU/GPU/RAM/placa) se ejecutan secuencialmente en UN Task.Run; sin Parallel ni WhenAll
- **Solo las 4 consultas**: GetCpuInfoAsync/GetGpuInfoAsync/GetMemoryInfoAsync/GetMotherboardInfoAsync; NO GetHardwareReportAsync/GetSystemInfoAsync/GetDiskInfoAsync (tests)
- **Tolerancia parcial**: cada sección protegida individualmente; CPU falla → GPU/RAM/placa continúan; estados: "Inventario actualizado" / "Inventario parcial" / "Inventario no disponible"
- **Datos aplicados**: Cpu, Gpus (orden Name→Manufacturer), Memory, MemoryModules (orden DeviceLocator→BankLabel→Manufacturer→PartNumber), Motherboard; IsInventoryLoaded/LastUpdated/InventoryErrors separados de live
- **Sexta pestaña "Inventario"** con ScrollViewer: cabecera + Actualizar inventario, CPU (nombre, fabricante, núcleos/hilos, velocidad reportada — no "frecuencia base"), GPU (nombre, fabricante, memoria reportada GB), RAM resumen (total, tipo, velocidad configurada, slots), Módulos RAM (slot, banco, fabricante, part number, serie, capacidad, velocidad, tipo, widths, rank), Placa madre/BIOS (fabricante, modelo, BIOS, fecha BIOS)
- **N/D**: EmptyStringToNdConverter (null/vacío/espacios → N/D; "Unknown"/"No detectado" se preservan) y PositiveNumberOrNdConverter (null/NaN/Infinity/<=0 → N/D; 0 no se muestra como dato válido); SpeedMHz=0 → N/D; BiosDate null → N/D (TargetNullValue)
- **Estados independientes**: live (StatusText) vs inventario (InventoryStatusText); IsBusy vs IsInventoryBusy; Errors vs InventoryErrors
- **Sin correlación WMI↔LHM/SPD**: dos fuentes separadas (Inventario WMI/SMBIOS, RAM SPD LHM); sin matching por slot/part number/serial; sin CPU/GPU usage/temperature estáticos en la UI
- Tests: FakeHardwareService con contadores y errores por sección; +25 tests netos (743 total)

### Fixed (Integración/pulido final Hardware - Fase B.4.3)
- **"Monitoreando" ya no se pisa**: ApplyLiveSnapshot deja StatusText en "Monitoreando" mientras IsMonitoring=true; la disponibilidad se refleja en ProviderStatusText/warnings/errors/tablas
- **Estado al detener derivado de la última muestra**: UpdateIdleLiveStatus → "Sin lectura" (nunca se leyó) / "Lectura no disponible" / "Lectura disponible" / "Sin sensores disponibles"; nunca se inventa disponibilidad
- **HasLiveReading**: distingue "sin leer" de "leído vacío" y "lectura no disponible"; inicial false; true tras cualquier snapshot (inclusive no disponible)
- **Hint inicial live** solo antes de la primera lectura (ShowInitialLiveHint); **tarjetas de resumen** ocultas hasta la primera lectura (ShowSummaryCards) — no se muestran "0 sensores" como si ya se hubiera inspeccionado
- **ApplyLiveFailure**: excepción inesperada de Refresh/stream limpia las 5 familias (sin datos stale), contadores 0, IsAvailable=false, HasLiveData=false, errors reemplazados, ProviderStatusText "No disponible"; StatusText "Lectura no disponible" (Refresh) o "Error de monitoreo" (stream, no sobrescrito por finally)
- **Cancelación normal**: conserva la última muestra; sin error; estado derivado por UpdateIdleLiveStatus
- **Hints de pestañas vacías** solo con lectura disponible + colección vacía (ShowEmpty*Hint): un fallo de lectura no afirma "no se detectó" hardware
- **Flags sincronizados**: helpers ReplaceWarnings/ReplaceErrors/ReplaceInventoryErrors garantizan HasErrors/HasWarnings/HasInventoryErrors coherentes en todos los caminos
- **Inventario**: catch exterior no acumula errores previos y no falsea InventoryLastUpdatedAt; errores de cada ejecución reemplazan a los anteriores
- **Proveedor visible en cabecera**: "Proveedor: Sin lectura / Disponible / No disponible" junto a "Modo administrador"
- **Textos corregidos**: Batería, Métrica, Módulo, Núcleos, Mín., Máx., información, dinámicas, telemetría, "— solo lectura" (UTF-8)
- **Independencia live/inventario** verificada en 5 casos (A-E) y navegación: salir cancela monitoring, volver reutiliza el mismo HardwareViewModel y permite reiniciar
- 34 tests netos nuevos/adaptados (777 total)

### Fixed (Estabilización v0.2 - S.1: exit status + transporte smartctl)
- **SmartctlExitFlags** [Flags] en Core: bits 0-7 según spec smartmontools (CommandLineOrInternalError=1, DeviceOpenOrIdentityFailed=2, SmartCommandOrChecksumError=4, SmartStatusFailed=8, PrefailAttributeThreshold=16, PastOrUsageAttributeFailure=32, ErrorLogContainsErrors=64, SelfTestLogContainsErrors=128)
- **SmartctlCommandResult**: ExitFlags calculada (ExitCode<0 → None), HasInvocationFailure (timeout/exit<0/bits 0-1), HasSmartCommandFailure (bit 2), HasHealthOrLogFindings (bits 3-7); IsSuccess corregido ("sin bits operativos 0-2 ni timeout" — ya NO significa "disco sano" ni trata exit 1 como éxito con warning)
- **ExitCode -1** (proceso no ejecutado) nunca se convierte a bits
- **TryGetSmartctlExitStatus**: formato principal numérico; legacy { "value": N } tolerado; ausente/inválido → null sin excepción
- **ParseStartShortTestJson**: sin switch 0/1/2/3/4; bits 0-2 → FailedToStart; bit 4 ya NO infiere Unsupported; bits 3-7 no impiden inicio; sin exit status → FailedToStart (no se asume éxito)
- **Inicio de self-tests**: timeout/exit<0/bits 0-2 → FailedToStart (nunca InProgress); SmartctlExitCode asignado SIEMPRE (inclusive fallos); sin análisis de frases localizadas para Unsupported
- **Consulta/GetLatestResult**: solo fallos operativos bloquean el parseo; exit 128 con self-test log válido se parsea (CompletedWithError detectable)
- **Análisis**: bits 3-7 con JSON válido se parsean (no descartados por IsSuccess); bit 2 con JSON parcial → IsAnalysisSuccessful=false + HealthStatus=Unknown + "Análisis SMART parcial/no concluyente" (sin marcar Critical ni contaminar report.Errors); bits 0-1 sin JSON → NotAvailable
- **Transporte -d TYPE**: SmartctlCommandBuilder centraliza argumentos; análisis/short/long/consulta usan `-d {device.Type}` (sat/nvme/sntjmicron...); Type vacío o "auto" omite -d; ApproximateDiskType (clasificación visual CATTECH) NUNCA se usa como transporte
- **Persistencia**: SmartDiskReport.SmartctlDeviceType y SmartTestSession.SmartctlDeviceType; legacy sin propiedad → "" → autodetección smartctl
- **Fallback ViewModel**: reconstruye el dispositivo con SelectedReport.SmartctlDeviceType (no DeviceType)
- **ListDevicesAsync**: salida parcial parseable no se descarta por exit status no cero; CheckAvailabilityAsync sin dependencia de exit 0/1
- 54 tests nuevos (831 total)

### Fixed (Estabilización v0.2 - S.2: semántica de salud SMART)
- **HealthStatus default = Unknown**: `new SmartDiskReport()` ya no es Good sin análisis
- **OverallHealthPassed → bool?**: true/false/null (null NO equivale a false); reportes legacy siguen deserializando
- **Good requiere evidencia positiva**: solo con smart_status.passed=true y sin hallazgos; passed=null sin evidencia → Unknown ("no se informó self-assessment general"); passed=false → Critical real con backup
- **SmartHealthPolicy** (Infrastructure/Smart): separa extracción (parser) de interpretación (política); constantes CATTECH documentadas como política conservadora, NO umbrales del estándar
- **Eliminada comparación RawValue > Threshold** (THRESH aplica al valor normalizado); fallback correcto: VALUE <= THRESH (+prefailure → Critical, usage → Warning); when_failed=now/past; Worst <= THRESH histórico → Warning como máximo
- **SmartAttribute.IsPrefailure** parseado desde flags.prefailure (bool JSON), no del string de flags
- **Política ATA corregida (crítico primero)**: ID5 >10 Critical / >0 Warning; ID197 >5 / >0; ID198 >5 / >0; ID199 CRC → Warning siempre (interfaz, nunca Critical por contador, sin backup); ID187/188 >0 Warning; ID1/3/9/12 informativos
- **SSD vendor-specific (173/175/176/177/231/233)**: sin thresholds raw universales → Info salvo reglas oficiales (when_failed/threshold normalizado)
- **Temperatura**: usa temperature.current (protocolo); eliminado el fallback ID194 raw (empaquetado/vendor); política CATTECH >65 Critical / >55 Warning (solo diagnóstico SMART de discos)
- **NVMe**: objeto principal `nvme_smart_health_information_log` + legacy tolerado; critical_warning NUMÉRICO (string legacy tolerado) → NvmeCriticalWarning; NvmeAvailableSpareThreshold preservado; spare <= umbral → Warning; percentage_used >=80 → Warning (100% NO es Critical por sí solo, sin clamp); media_errors >0 → Critical con backup; unsafe_shutdowns solo informativo; passed=true + critical_warning activo → Critical conservador con aviso de discrepancia
- **Exit bits 3-7 como evidencia**: bit3/bit4 → Critical con backup; bit5/bit6/bit7 → Warning (sin backup solo por logs); bit2 mantiene precedencia S.1 (Unknown + unsuccessful)
- **RequiresBackupRecommendation** solo por señales críticas reales; no por CRC/logs/percentage/spare/unsafe shutdowns
- **SmartDiskService entrega ExitFlags siempre** (ExitCode >= 0) al parser
- **Errors técnicos ya no convierten a Critical**: la política usa señales específicas, no Errors.Count
- 85 tests nuevos (916 total)

### Added (Estabilización v0.2 - S.3.1: Release Gate técnico)
- **Gate técnico PASS**: restore, build Release (0 errores), tests Release x2 (936/936, sin flaky), publish win-x64 self-contained OK
- **config/herramientas.json ahora funcional**: SmartctlRunner lee `smartctlPath` y `smartctlAutoDetect` desde la base de la app (ruta programática tiene prioridad); `smartctlAutoDetect=false` sin ruta válida → SMART no disponible; los mensajes de la UI que referenciaban el config ahora son verdaderos
- **Persistencia v0.2 verificada**: round-trip de SmartDiskReport (SmartctlDeviceType, OverallHealthPassed null, NvmeCriticalWarning, NvmeAvailableSpareThreshold, IsPrefailure) y SmartTestSession (SmartctlDeviceType); legacy sin campos nuevos → defaults correctos
- **Seguridad de comandos auditada**: solo flags permitidos (--version, --scan, -a -j, -t short/long -j, -l selftest -j, -d); tests de builder sin flags destructivos
- **Textos obsoletos corregidos**: footer ventana "v0.1.1 MVP" → "v0.2 pre-release"; footer informe sin número de versión; placeholder de Home sin "futuras iteraciones del MVP"; README con versión v0.2 en desarrollo y nota de smartctl externo; V0_2_PLAN con Fase B ✅
- **ZIP v0.1.1 versionado eliminado** del repositorio (artefacto obsoleto)
- **docs/QA_V0_2.md** y **docs/RELEASE_NOTES_V0_2.md** creados
- 20 tests nuevos (936 total)

### Release (S.3.2)
- Release Gate S.3.1 PASS
- Versionado 0.2.0 (Version, AssemblyVersion 0.2.0.0, FileVersion 0.2.0.0)
- Build/publish final win-x64 self-contained
- Artefacto portable `CATTECH-Optimizer-Pro-v0.2.0-win-x64.zip` + checksum SHA-256
- Configuración `config/herramientas.json` incluida en el publish
- Tag `v0.2.0`
- GitHub Release v0.2.0 (según disponibilidad de GitHub CLI)
- SmartctlAvailability, SmartDiskDevice, SmartctlCommandResult
- ISmartctlRunner, ISmartDiskService interfaces
- 32 tests de smartctl + 18 tests de SMART (214 total)

### Documentation
- `docs/V0_2_PLAN_SMART_HARDWARE.md`: plan detallado v0.2
- `docs/SMART_INTEGRATION_DECISION.md`: decisión de integración smartctl
- AUDITORIA_REFERENCIAS.md: notas de planificación v0.2

---

## [Unreleased]

### Added
- **Exportación de informes a PDF** (ReportView)
  - Botón "Exportar PDF" en pantalla de informes
  - Exportación via Microsoft Edge headless (`--print-to-pdf`)
  - Verificación de disponibilidad de Microsoft Edge
  - Guardado en `reports/pdf/Informe_Tecnico_CATTECH_Cliente_YYYYMMDD-HHMMSS.pdf`
  - Si falla PDF, se conserva el HTML generado
  - Botón "Abrir PDF" para visor predeterminado
  - Actualización de GeneratedReportInfo con PdfPath
- IPdfExportService: interfaz CanExport/ExportHtmlToPdf/GetPdfOutputPath/OpenPdf
- PdfExportService: implementación con verificación de Edge y fallback
- PdfExporterInfo: modelo con IsAvailable, StatusMessage
- 20 tests nuevos (164 total)
- **Informe técnico HTML profesional** (ReportView)
  - Generación de informe HTML con CSS embebido (portátil, sin internet)
  - 9 secciones: portada, cliente, equipo, diagnóstico, acciones, resultados, recomendaciones, observaciones, firma
  - Selección de datos desde todos los módulos (cliente, diagnóstico, inicio, limpieza, optimización, restauración)
  - Checkboxes para incluir/excluir secciones
  - Logo embebido como base64 para portabilidad
  - Recomendaciones automáticas basadas en datos (RAM, disco, inicio, temporales)
  - Diseño profesional preparado para A4
  - Persistencia en `reports/html/Informe_Tecnico_CATTECH_Cliente_YYYYMMDD-HHMMSS.json`
  - Abrir informe en navegador y abrir carpeta de informes
- ReportGenerationOptions: modelo con Settings, ServiceReport, DiagnosticReport, secciones
- GeneratedReportInfo: modelo con Id, ClientName, HtmlPath, IncludedSections
- ReportRecommendation: modelo para recomendaciones automáticas
- IReportGenerationService: interfaz Generate/SaveInfo/List/Open
- HtmlReportService: implementación con plantilla HTML embebida
- ReportRecommendationEngine: motor de recomendaciones automáticas
- ReportViewModel con carga de datos, selección, generación
- 12 tests nuevos (139 total)
- **Punto de restauración** (RestorePointView)
  - Verificación de estado: permisos, servicio, protección del sistema
  - Creación de puntos de restauración via PowerShell (Checkpoint-Computer) o WMI
  - Nombre estándar: "CATTECH Optimizer Pro - Antes de mantenimiento - yyyy-MM-dd HH:mm"
  - Manejo de errores: permisos insuficientes, protección deshabilitada, frecuencia limitada
  - Historial de intentos con resultado
  - Persistencia en `data/restore-points/restore-point-result-YYYYMMDD-HHMMSS.json`
  - Panel de estado con indicadores de color
  - Advertencias de seguridad sobre limitaciones de Windows
- RestorePointStatus: modelo con IsAdministrator, IsSystemRestoreAvailable, IsProtectionEnabled
- RestorePointResult: modelo con Success, ErrorMessage, ErrorCode, MethodUsed
- RestorePointMethod: enum Unknown/PowerShellCheckpoint/WmiSystemRestore
- IRestorePointService: interfaz CheckStatus/Create/SaveResult/ListResults
- RestorePointService: implementación con PowerShell y WMI
- RestorePointViewModel con check status, create, history
- 6 convertidores nuevos: AdminColor, StatusColor, CanCreateColor, CanCreateText, ResultColor, SuccessText
- 15 tests nuevos (127 total)
- **Optimización visual segura** (VisualOptimizationView)
  - 8 ajustes visuales predefinidos (animaciones, sombras, transparencias, fuentes)
  - Análisis del estado actual de cada ajuste
  - Selección individual con checkboxes
  - Botón "Seleccionar seguros" para selects rápidos
  - Backup automático de cada valor antes de modificar
  - Reversión desde backups con un click
  - Persistencia en backups/visual/visual-backups.json
  - Detección de compatibilidad con Windows
  - Indicadores de "Ya optimizado", "Requiere reinicio", "Requiere cerrar sesión"
  - Resultado: aplicados, omitidos, fallidos, requiere reinicio
  - NO modifica: resolución, drivers, servicios, accesibilidad, Defender
- VisualOptimizationSetting: modelo con RegistryPath, CurrentValue, RecommendedValue
- VisualOptimizationBackup: modelo con CanRestore, RestoredAt
- VisualOptimizationResult: modelo con AppliedCount, RequiresRestart, Errors
- IVisualOptimizationService: interfaz Analyze/Apply/Restore/SaveResult
- VisualOptimizationService: implementación con Registry read/write
- VisualOptimizationViewModel con scan, select, apply, backup, restore
- VisualRiskColorConverter, BoolToVisYesNoConverter
- 16 tests nuevos (112 total)
- **Limpieza segura de temporales** (TempCleanupView)
  - Escaneo previo de ubicaciones con tamaño estimado
  - 4 targets predefinidos: %TEMP%, Windows\Temp, Miniaturas, Papelera
  - Selección individual con checkboxes
  - Botón "Seleccionar bajos riesgo" para selects rápidos
  - Confirmación antes de limpiar
  - Borrado seguro: archivos primero, carpetas vacías después
  - Protección de archivos bloqueados, recientes (<60s) y protegidos
  - Timeout de 30s por ubicación para evitar bloqueos
  - Resultado: espacio liberado, archivos eliminados, omitidos, errores
  - Persistencia en `data/cleanup-results/cleanup-result-YYYYMMDD-HHMMSS.json`
  - Advertencias de seguridad en UI
- TempCleanupTarget: modelo con Id, DisplayName, Path, RiskLevel, IsSystemLocation
- TempCleanupResult: modelo con DeletedBytes, SkippedFiles, FailedFiles, Errors
- CleanupTargets: targets predefinidos con validación de ubicaciones permitidas
- ITempCleanupService: interfaz Scan/Cleanup/SaveResult/ListResults
- TempCleanupService: implementación con EnumerationOptions, timeout, protección
- TempCleanupViewModel con scan, select, clean, result display
- RiskColorConverter
- 17 tests nuevos (96 total)
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

## [0.1.0] - 2026-XX-XX (Futuro)

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

## [0.2.0] - 2026-XX-XX (Futuro)

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

## [0.3.0] - 2026-XX-XX (Futuro)

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

## [0.4.0] - 2026-XX-XX (Futuro)

### Added
- CATTECH Preserve - Backup de configuración
- Restauración selectiva de configuración
- Backup de drivers críticos
- Sincronización entre equipos
- Exportación/Importación desde USB

---

## [0.5.0] - 2026-XX-XX (Futuro)

### Added
- CATTECH Rescue USB
- Creación de USB bootable con WinPE
- Integración de Memtest86+
- Herramientas de diagnóstico offline
- Recuperación de archivos básica
- Reparación de arranque

---

## [1.0.0] - 2026-XX-XX (Futuro)

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
