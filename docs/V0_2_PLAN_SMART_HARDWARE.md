# Plan v0.2 - SMART y Hardware Avanzado

**Versión objetivo**: v0.2  
**Fecha**: 2026  
**Estado**: Planificación

---

## Objetivo General

Agregar diagnóstico avanzado de discos y hardware, priorizando seguridad, lectura no invasiva y reporte técnico claro.

---

## Alcance v0.2

### Fase A: SMART Completo (prioridad)

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

### Fase B: Hardware Avanzado (posterior dentro de v0.2)

1. Sensores de temperatura en tiempo real
2. CPU detallada (núcleos, temperatura, uso)
3. GPU detallada (temperatura, uso, memoria)
4. Batería de notebooks (si aplica)
5. Placa madre (BIOS, chipset)
6. RAM avanzada (velocidad, timings, slots)

---

## Dependencias

| Dependencia | Licencia | Tipo | Uso |
|-------------|----------|------|-----|
| smartmontools (smartctl.exe) | GPL-2.0 | Binario externo | Diagnóstico SMART |
| LibreHardwareMonitorLib | MPL 2.0 | NuGet | Sensores hardware |

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

| Fase | Duración | Dependencias |
|------|----------|--------------|
| Fase A.1: Integración smartctl | 1 semana | smartctl.exe en tools/ |
| Fase A.2: Modelo SmartDiskReport | 1 semana | — |
| Fase A.3: SmartctlParser | 1 semana | Salida JSON de smartctl |
| Fase A.4: UI Discos SMART | 1 semana | Modelo + Parser |
| Fase A.5: Test SMART corto | 0.5 semana | smartctl |
| Fase A.6: Test extendido + advertencia | 0.5 semana | Test corto |
| Fase A.7: Inclusión en informe | 0.5 semana | Modelo SMART |
| **Total Fase A** | **4 semanas** | |
| Fase B.1: Sensores temperatura | 1 semana | LibreHardwareMonitorLib |
| Fase B.2: CPU/GPU/Batería | 1 semana | LibreHardwareMonitorLib |
| Fase B.3: RAM avanzada | 0.5 semana | LibreHardwareMonitorLib |
| Fase B.4: UI Hardware avanzado | 1 semana | Modelos |
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
- [ ] Mostrar sensores de temperatura
- [ ] Mostrar CPU detallada
- [ ] Mostrar GPU detallada
- [ ] Mostrar batería (si aplica)
- [ ] Mostrar placa madre
- [ ] Mostrar RAM avanzada

### General
- [ ] 164+ tests pasando
- [ ] Build sin errores
- [ ] Documentación actualizada
- [ ] Tests para SMART y hardware

---

*Plan v0.2 - CATTECH OPTIMIZER PRO*
