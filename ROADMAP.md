# Roadmap - CATTECH OPTIMIZER PRO

**Versión actual**: 0.3.0 (publicada)  
**Runtime**: .NET 10 LTS  
**Última actualización**: 2026-11-22

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

| Versión | Estado | Contenido principal |
|---------|--------|---------------------|
| v0.1.1 | Publicada | MVP estabilizado: configuración, cliente/equipo, diagnóstico, inicio, limpieza, optimización visual, restauración, informes HTML/PDF |
| v0.2.x | Publicada | SMART completo, hardware en tiempo real, inventario WMI/SMBIOS, correcciones de informes y GPU |
| v0.3.x | **Publicada (v0.3.1, 2026-11-22)** | BSOD, centro de reparaciones con preview, Evidence Engine, Decision Center DEMO, pasaporte técnico, .NET 10 LTS |
| v0.4 | Planificada | Preservación y continuidad del sistema (backup/restore de configuraciones, migración de perfiles) |
| v0.5 | Planificada | Rescue USB (entorno de arranque) y diagnóstico fuera de línea |
| v1.0 | Planificada | Suite completa con instalador firmado, actualizaciones automáticas y soporte extendido |

---

## v0.3.0 - BSOD y Reparaciones Comunes (PUBLICADA)

**Objetivo**: Análisis de crashes y reparación de problemas comunes, con diagnóstico antes de reparar.

### Incluido

- [x] Lectura read-only de minidumps (`C:\Windows\Minidump`) y eventos `BugCheck`
- [x] Decodificación de Bug Check Codes desde `config/bugcheckcodes.json`
- [x] Listado de módulos candidatos sin atribución causal automática
- [x] Verificación opcional con DumpChk cuando está disponible
- [x] Diagnóstico de Windows Update y plan de reparación con consentimiento
- [x] Preflight de SFC/DISM y punto de restauración (sin ejecución automática)
- [x] Evidence Engine: hechos, reglas deterministas, preview y evaluación de resultado
- [x] Decision Center (DEMO) con explicaciones separadas para técnico y cliente
- [x] Pasaporte técnico redactado en memoria, con checksum y consentimiento revocable
- [x] Migración a .NET 10 LTS y paquete `win-x64` autocontenido

### Verificación

- Build Release sin warnings, 2055 tests xUnit (0 fallas, 0 omitidos) en Windows CI.
- Paquete con ProductVersion ligada al SHA de origen, SHA-256 y atestación de procedencia.
- Instalador `CATTECH-Optimizer-Pro-v0.3.1-Setup.exe` (Inno Setup) con instalación por usuario o para todos los usuarios, accesos directos y desinstalador.
- Pruebas E2E por UI Automation: limpieza real, desactivación/restauración de programas de inicio y navegación sin crashes.
- Protocolo de verificación física en `docs/QA_REAL_SMOKE.md`.

---

## v0.3.1 - Correcciones de funcionalidad (PUBLICADA 2026-11-22)

**Objetivo**: que cada pantalla ejecute de verdad sus acciones.

- [x] Limpieza recursiva de temporales, sin retención de 7 días, con confirmación
- [x] Vaciado real de la Papelera de reciclaje (Shell API)
- [x] Desactivación real de programas de inicio (clave de registro escribible)
- [x] Selección estable en grillas y clasificador Microsoft corregido
- [x] Restauración de backups de inicio y de optimización visual
- [x] Punto de restauración sin la sonda `srservice` inexistente
- [x] SMART con verificación automática y errores reales
- [x] Informes con auto-carga y apertura de carpeta funcional
- [x] UX técnica: barra de estado, panel de inicio, atajos, exportaciones y configuración de smartctl

---

## Próximos pasos (v0.4+)

1. **Preservación del sistema**: perfil de configuración exportable, respaldo de ajustes visuales/inicio y restauración guiada.
2. **Rescue USB**: medio de arranque con diagnóstico read-only (SMART, hardware, logs) sin depender del SO instalado.
3. **Distribución**: firma de código del ejecutable y del instalador, actualizaciones automáticas y empaquetado MSIX opcional.
4. **Hardware**: validación en más familias de equipos (NVMe, Thunderbolt, estaciones de trabajo) y telemetría térmica con umbrales explícitos por fabricante.
5. **Accesibilidad e internacionalización**: revisión de contraste/teclado y extracción de textos a recursos.

---

## Cómo contribuir

1. **Leer** `SEGURIDAD.md` antes de cualquier cambio.
2. **Revisar** este roadmap para ver tareas disponibles.
3. **Crear** un issue antes de PRs grandes.
4. **Seguir** las convenciones de código existentes.
5. **Agregar tests** para código nuevo (la suite debe quedar en verde).
6. **Documentar** los cambios en `CHANGELOG.md`.

---

*Roadmap de CATTECH OPTIMIZER PRO — documento vivo, se actualiza con cada release.*
