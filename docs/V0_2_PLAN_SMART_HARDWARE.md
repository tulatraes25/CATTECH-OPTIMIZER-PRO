# Plan v0.2 - SMART y Hardware Avanzado

**Versión objetivo**: v0.2  
**Fecha**: 2026  
**Estado**: Planificación

---

## Objetivo General

Agregar diagnóstico avanzado de discos y hardware, priorizando seguridad, lectura no invasiva y reporte técnico claro.

---

## Alcance v0.2

### Fase A: SMART Completo (prioridad) — ✅ Completada

1. **Diagnóstico SMART de discos** mediante smartmontools/smartctl
2. **Estado de disco**: Bueno / Precaución / Crítico / No disponible
3. **Detección de tipos**: HDD, SSD SATA, NVMe, USB (si informa SMART)
4. **Lectura de atributos relevantes**:
   - Health status
   - Temperatura
   - Horas de uso
   - Power cycles
   - Sectores reasignados
   - Sectores pendientes
   - Errores no corregibles (UEC)
   - Porcentaje de vida útil (SSD/NVMe)
5. **Test SMART corto** (con confirmación)
6. **Test SMART extendido** (solo opción avanzada con advertencia)
7. **Inclusión de resultados SMART en informe HTML/PDF**

### Fase B: Hardware Avanzado (posterior dentro de v0.2) — ✅ Completada

1. ✅ Temperaturas en tiempo real (B.1 completada: B.1.1 fundación, B.1.2 muestreo repetido, B.1.3 UI inicial)
2. ✅ B.2.1: métricas dinámicas CPU/GPU (Load + Clock) capturadas con un solo Refresh
3. ✅ B.2.2: memoria GPU (SmallData) en MB con el mismo Refresh; sin interpretar por nombre
4. CPU detallada (núcleos, temperatura, uso)
5. GPU detallada (temperatura, uso, memoria)
6. Batería de notebooks (si aplica)
7. Placa madre (BIOS, chipset)
8. RAM avanzada (velocidad, timings, slots)

> B.1.3 creó la UI inicial exclusiva de temperaturas; B.4 ampliará esa misma pantalla con todo el hardware avanzado (CPU/GPU/batería/RAM/placa).
>
> La adquisición de datos de B.2 (CPU/GPU/Batería) está completa. Su presentación avanzada se implementará en B.4.

---

## Dependencias

| Dependencia | Licencia | Tipo | Uso |
|-------------|----------|------|-----|
| smartmontools (smartctl.exe) | GPL-2.0 | Binario externo | Diagnóstico SMART |
| LibreHardwareMonitorLib | MPL 2.0 | NuGet (0.9.6) | Sensores hardware |

---

## Reglas de Seguridad

1. SMART debe ser solo lectura por defecto
2. No ejecutar tests destructivos
3. Test corto solo con confirmación explícita
4. Test extendido solo con advertencia avanzada
5. No ejecutar pruebas si el disco reporta estado crítico sin recomendar backup primero
6. No bloquear la UI durante análisis
7. Manejar discos que no informan SMART
8. No asumir que un disco "sin SMART" está sano
9. No modificar configuración de discos
10. No ejecutar comandos de escritura en discos

---

## Cronograma Estimado

| Fase | Duración | Dependencias | Estado |
|------|----------|--------------|--------|
| Fase A.1: Integración smartctl | 1 semana | smartctl.exe en tools/ | ✅ Implementado |
| Fase A.2: Modelo SmartDiskReport | 1 semana | — | ✅ Implementado |
| Fase A.3: SmartctlParser | 1 semana | Salida JSON de smartctl | ✅ Implementado |
| Fase A.4: UI Discos SMART | 1 semana | Modelo + Parser | ✅ Implementado |
| Fase A.5: Test SMART corto | 0.5 semana | smartctl | ✅ Implementado |
| Fase A.6: Test extendido + advertencia | 0.5 semana | Test corto | ✅ Implementado |
| Fase A.7: Inclusión en informe | 0.5 semana | Modelo SMART | ✅ Implementado |
| A.7.1: Integración análisis SMART en informe | — | — | ✅ Implementado |
| A.7.2a: Self-tests persistidos en informe | — | — | ✅ Implementado |
| A.7.2b: Recomendaciones SMART/self-test | — | — | ✅ Implementado |
| **Total Fase A** | **4 semanas** | | ✅ Completada |
| B.1.1: Fundación LibreHardwareMonitor + lectura temperatura | — | LibreHardwareMonitorLib 0.9.6 | ✅ Implementado |
| B.1.2: Muestreo repetido + sesión reutilizable | — | B.1.1 | ✅ Implementado |
| B.1.3: UI de temperaturas en tiempo real | — | B.1.2 | ✅ Implementado |
| Fase B.1: Sensores temperatura | 1 semana | LibreHardwareMonitorLib | ✅ Completada |
| B.2.1: Fundación métricas dinámicas CPU/GPU (Load + Clock) | — | B.1.2 | ✅ Implementado |
| B.2.2: Métricas memoria GPU (SmallData) | — | B.2.1 | ✅ Implementado |
| B.2.3: Telemetría de batería LHM | — | B.2.2 | ✅ Implementado |
| Fase B.2: CPU/GPU/Batería — backend | 1 semana | LibreHardwareMonitorLib | ✅ Completada |
| B.3.1: Inventario RAM / módulos / slots WMI-SMBIOS | — | WMI | ✅ Implementado |
| B.3.2: SPD + timings mediante LibreHardwareMonitor | — | B.3.1 | ✅ Implementado |
| Fase B.3: RAM avanzada | 0.5 semana | LibreHardwareMonitorLib | ✅ Completada |
| B.4.1: UI live avanzada (LiveSnapshot + 5 pestañas) | — | B.1.3 + backends | ✅ Implementado |
| B.4.2: Inventario estático WMI/SMBIOS en HardwareView | — | B.4.1 + B.3.1 | ✅ Implementado |
| B.4.3: Integración/pulido final Hardware | — | B.4.2 | ✅ Implementado |
| Fase B.4: UI Hardware avanzado | 1 semana | Modelos | ✅ Completada |
| **Fase B: Hardware avanzado** | **3 semanas** | | ✅ Completada |
| S.1: Exit status + transporte smartctl | — | — | ✅ Implementado |
| S.2: Semántica de salud SMART / atributos | — | — | ✅ Implementado |
| S.3.1: Release Gate técnico + QA v0.2 | — | — | ✅ PASS |
| S.3.2: Versionado 0.2.0 + tag/release | — | — | ⏳ Pendiente |
| **Total Fase B** | **3 semanas** | |
| **Total v0.2** | **7 semanas** | |

---

## Criterios de Aceptación v0.2

### SMART
- [ ] Detectar discos HDD, SSD, NVMe
- [ ] Leer atributos SMART relevantes
- [ ] Mostrar estado: Bueno/Precaución/Crítico/No disponible
- [ ] Ejecutar test corto con confirmación
- [ ] Ejecutar test extendido con advertencia
- [ ] Incluir resultados en informe HTML/PDF
- [ ] Manejar discos sin SMART sin fallar
- [ ] No ejecutar tests destructivos

### Hardware
- [x] Fundación sensores de temperatura (B.1.1/B.1.2): sesión reutilizable, muestreo repetido cancelable
- [x] UI inicial de temperaturas en tiempo real (B.1.3): Actualizar una vez, Iniciar/Detener monitoreo, N/D, sin thresholds
- [x] Fundación métricas dinámicas CPU/GPU (B.2.1): Load + Clock con un solo Refresh, sin UI
- [x] Métricas memoria GPU (B.2.2): SmallData en MB, sin interpretar Used/Free/Total por nombre, sin usage calculado, mismo Refresh
- [x] Telemetría de batería (B.2.3): Level/Energy/Voltage/Current/Power/TimeSpan con IsBatteryEnabled, mismo Computer/sesión/Refresh; Battery Temperature en TemperatureSensors; cero/una/varias baterías; sin heurísticas ni salud calculada
- [x] Inventario RAM avanzado (B.3.1): módulos WMI/SMBIOS con slot, fabricante, part number, serial, capacidad, velocidad configurada, tipo DDR/LPDDR verificado contra spec DMTF, data/total width, rank; slots usados/totales
- [x] Timings SPD (B.3.2) via LibreHardwareMonitor: SensorType.Timing de hardware Memory en ns, nombres preservados sin parser, sin CL/ciclos, sin XMP/EXPO, sesión con vista dinámica de hardware (SPD tardío observable), sin correlación WMI↔SPD
- [x] UI live avanzada (B.4.1): 5 pestañas (Temperaturas, CPU/GPU, Memoria GPU, Batería, RAM SPD) alimentadas por un único HardwareLiveSnapshot; sin thresholds, sin selección semántica
- [x] Inventario estático WMI/SMBIOS (B.4.2): sexta pestaña con CPU/GPU/RAM/módulos/placa/BIOS; actualización manual independiente; WMI fuera del hilo UI; no consulta durante monitoreo; sin correlación con sensores live/SPD; no se consultan discos ni SO
- [x] Integración/pulido final Hardware (B.4.3): estados live consistentes ("Monitoreando" estable, derivación al detener, HasLiveReading, sin datos stale ante excepciones), hints de pestañas vacías solo en lectura disponible, flags de error sincronizados, proveedor visible, textos con tildes correctas, independencia live/inventario, reutilización del ViewModel al navegar
- [x] Mostrar CPU detallada
- [x] Mostrar GPU detallada
- [x] Mostrar batería (si aplica)
- [x] Mostrar placa madre
- [x] Mostrar RAM avanzada

### General
- [ ] 164+ tests pasando (777 actuales)
- [ ] Build sin errores
- [ ] Documentación actualizada
- [ ] Tests para SMART y hardware

---

*Plan v0.2 - CATTECH OPTIMIZER PRO*

Pendiente: estabilización final v0.2 y revisión de release.

### Estabilización v0.2
- [x] S.1 Exit status + transporte smartctl: bitmask de 8 bits (SmartctlExitFlags), bits 0-2 operativos vs 3-7 hallazgos; JSON exit_status numérico; -d TYPE preservado en análisis/self-tests/consulta; SmartctlDeviceType persistido en reportes/sesiones; ApproximateDiskType nunca se usa como transporte; legacy sin tipo → autodetección
- [x] S.2 Semántica de salud SMART / atributos: HealthStatus default Unknown; OverallHealthPassed nullable (Good solo con evidencia positiva); eliminada RawValue vs THRESH; when_failed/prefailure/VALUE-THRESH como señales del estándar; política ATA por ID con crítico primero (5/197/198); CRC 199 → warning de interfaz sin backup; SSD vendor-specific sin thresholds raw; temperatura vía temperature.current; NVMe _log + critical_warning numérico + spare threshold + percentage_used (100% no es Critical) + media_errors Critical + unsafe_shutdowns informativo; exit bits 3-7 como evidencia; backup solo por señales críticas reales
- [ ] S.3 Release gate / criterios de aceptación
