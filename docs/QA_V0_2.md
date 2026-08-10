# QA - CATTECH OPTIMIZER PRO v0.2.0

**Fecha**: 2026  
**Commit base evaluado**: dee93d3 (S.2) + correcciones del gate (S.3.1)  
**Objetivo**: Release gate técnico previo al versionado v0.2.0 (S.3.2)

---

## Entorno

| Ítem | Valor |
|------|-------|
| Plataforma objetivo | Windows 10 (1809+) / Windows 11 |
| .NET | 8.0 (net8.0 / net8.0-windows) |
| Configuración | Release |
| Ejecución | Local (CLI dotnet). CI GitHub no configurado: validación ejecutada localmente |

---

## Build / Tests / Publish

- [x] `dotnet restore` → sin error
- [x] `dotnet build --configuration Release` → **0 errores** (warnings preexistentes no blockers: CA1416 en StartupService, CS8601 en ClientEquipmentViewModel)
- [x] `dotnet test --configuration Release` — ejecución 1 → **936 passed / 0 failed / 0 skipped**
- [x] `dotnet test --configuration Release` — ejecución 2 (detección flaky) → **936 passed / 0 failed / 0 skipped**
- [x] `dotnet publish --configuration Release --runtime win-x64 --self-contained true` → **0 errores**
- [x] Artefacto: `Cattech.Optimizer.Pro.UI.exe` (AssemblyName real del proyecto UI) + 289 archivos self-contained
- [x] `LibreHardwareMonitorLib.dll` incluida en el publish
- [x] `smartctl.exe` **NO incluido** en el paquete (no está versionado); CATTECH lo autodetecta desde: ruta programática → `config/herramientas.json` → rutas junto a la app (`tools/smartmontools/`) → instalación estándar → PATH
- [x] Ausencia de smartctl: SMART queda no disponible con mensaje claro; resto de la app sigue funcional (tests con fakes/ruta inexistente)

## Criterios SMART

| Criterio | Evidencia |
|----------|-----------|
| Detección HDD/SSD/NVMe (scan) | SmartctlParser.ParseScanJson/ParseScanText + SmartctlTests |
| Lectura de atributos relevantes | SmartctlParser.ExtractImportantAttributes + SmartDiskReportTests |
| Estados Good/Warning/Critical/NotAvailable/Unknown | SmartHealthPolicyTests (86 casos) |
| Test corto | SmartTestServiceTests (inicio/estado/persistencia) |
| Test extendido con advertencia | SmartTestServiceTests |
| Resultados SMART en HTML/PDF | HtmlReportService (sección SMART + self-tests) + ReportGenerationTests |
| Disco sin SMART/no accesible no rompe análisis | SmartctlExitStatusTests.Analyze_InaccessibleDisk_DoesNotAbortOthers |
| No ejecutar tests destructivos | Auditoría de comandos (solo --version, --scan, -a -j, -t short/long -j, -l selftest -j, -d) + ReleaseGateTests.CommandBuilder_* |
| Exit status bitmask | SmartctlExitStatusTests (bits 0-7, combinaciones, -1) |
| -d TYPE preservado | SmartctlExitStatusTests (análisis/short/long/consulta/fallback VM) |
| Good requiere evidencia positiva | SmartHealthPolicyTests (passed=true/null) |
| CRC no implica fallo físico crítico | SmartHealthPolicyTests (ID199 warning, sin backup) |
| NVMe real-shaped JSON | SmartHealthPolicyTests (nvme_smart_health_information_log, critical_warning numérico) |
| Persistencia v0.2 (SmartctlDeviceType, OverallHealthPassed null, IsPrefailure, NVMe) | ReleaseGateTests (round-trip + legacy) |

## Criterios Hardware

| Criterio | Evidencia |
|----------|-----------|
| Temperaturas | LibreHardwareSensorServiceTests + HardwareViewModelTests |
| CPU/GPU Load + Clock | LibreHardwareSensorServiceTests (B.2.1) |
| Memoria GPU (SmallData) | LibreHardwareSensorServiceTests (B.2.2) |
| Batería | LibreHardwareSensorServiceTests (B.2.3) |
| RAM inventario WMI/SMBIOS | WmiMemoryInventoryTests (40) |
| SPD timings | LibreHardwareSensorServiceTests (B.3.2: SensorType.Timing) + HardwareViewModelTests (pestaña RAM SPD) |
| CPU/GPU/placa/BIOS inventario | WmiMemoryInventoryTests + HardwareViewModelTests (B.4.2) |
| Una sola sesión live | LibreHardwareSensorServiceTests (Create=1/Refresh=1/Dispose=1) |
| Cancelación al navegar | HardwareViewModelTests.LeavingHardwareSection + ReturnToHardware |
| Sin thresholds/health inventados | HardwareViewModelTests (sin IsHot/HealthStatus/Severity) |
| WMI fuera del hilo UI | HardwareViewModelTests (Task.Run único, sin WMI en stream) |
| Estados N/D | HardwareViewModelTests (converters) |
| Sin hardware real en tests | Todos los tests usan fakes (IWmiMemoryReader, IHardwareMonitorFactory, IHardwareSensorService, IHardwareService) |

## Seguridad

- [x] SMART read-only por defecto; self-tests no destructivos (firmware interno del disco)
- [x] Disco Critical bloquea self-tests adicionales (SmartDiskViewModel)
- [x] Backup recomendado antes de acciones críticas
- [x] Sin comandos de escritura/configuración de discos (`-s on`, security erase, sanitize, format) — auditoría de comandos sin coincidencias
- [x] Backup/reversión en limpieza, optimización visual y desactivación de inicio (v0.1, sin cambios)
- [x] Sin secretos/tokens en config versionado (`config/herramientas.json` solo paths)

## Persistencia

- [x] SmartAnalysisResult / SmartDiskReport / SmartTestSession round-trip con campos v0.2
- [x] Legacy sin SmartctlDeviceType / NvmeCriticalWarning / IsPrefailure → defaults correctos, sin migración destructiva
- [x] Filename determinístico de sesiones de self-test (misma sesión sobrescribe el mismo archivo)

## Informes

- [x] HTML usa HealthStatus/RequiresBackupRecommendation ya calculados (sin reinterpretar raw)
- [x] PDF deriva del HTML (Edge headless)
- [x] Unknown → no concluyente; NotAvailable → no disponible; Critical → backup prioritario; CRC-only → sin backup inmediato
- [x] Self-tests en informe desde estado persistido

## Dependencias y licencias

- CATTECH: MIT (LICENSE)
- LibreHardwareMonitorLib 0.9.6: MPL 2.0 (NuGet) — sensores read-only
- RAMSPDToolkit-NDD 1.4.2: dependencia transitiva de LHM (SPD/SMBus), verificada en nuspec y project.assets.json
- smartmontools/smartctl: GPL-2.0 — invocado como **proceso externo**, no se integra código fuente; no se distribuye en el paquete v0.2 (autodetección)

## Limitaciones conocidas

- Disponibilidad de sensores depende del hardware y permisos (admin)
- SPD puede no estar disponible (chipset/SMBus/driver LHM)
- Batería no aplica a desktops (estado neutral, no error)
- smartctl es dependencia externa en v0.2 (no empaquetado)
- Sin correlación automática WMI ↔ SPD
- Windows 7/8/8.1 no soportados (.NET 8)
- CI GitHub no configurado (validación local)
- Smoke test visual WPF: **no ejecutado por limitación del entorno** (CLI sin interacción visual); navegación cubierta por tests STA que instancian las Views reales

## Versionado final (S.3.2)

- [x] `src/Cattech.Optimizer.Pro.UI/Cattech.Optimizer.Pro.UI.csproj`: Version 0.2.0, AssemblyVersion 0.2.0.0, FileVersion 0.2.0.0
- [x] `README.md`: cabecera v0.2.0 + seccion de novedades
- [x] `MainWindow.xaml`: footer v0.2.0
- [x] `CHANGELOG.md`: sección [0.2.0] - 2026-08-09
- [x] `HtmlReportService.cs`: footer informe con v0.2.0
- [x] `docs/RELEASE_NOTES_V0_2.md`: finalizadas para v0.2.0

## Resultado final\n\n- Build Release final: 0 errores\n- Tests Release x2 finales\n- Publish final win-x64 self-contained\n- ZIP CATTECH-Optimizer-Pro-v0.2.0-win-x64.zip + SHA-256\n- ProductVersion/FileVersion del EXE verificados (0.2.0 / 0.2.0.0)\n- Tag v0.2.0 creado\n- GitHub Release: segun disponibilidad de GitHub CLI\n\n## Resultado

**RELEASE GATE: PASS** — sin blockers abiertos. Publicación (tag/release) pendiente de S.3.2 tras verificar el commit en GitHub.
