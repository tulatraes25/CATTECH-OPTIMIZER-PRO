# CATTECH OPTIMIZER PRO v0.2.0 — Release Notes

---

## Novedades

### SMART avanzado (Fase A)
- Análisis SMART read-only por disco (smartctl como proceso externo)
- Estados: Bueno / Precaución / Crítico / No disponible / Desconocido
- Atributos relevantes ATA (sectores reasignados, pendientes, offline, CRC, temperatura) y NVMe (critical_warning, percentage_used, media_errors, available_spare)
- Self-tests corto y extendido con persistencia de sesiones y consulta de estado
- Sección SMART en informes HTML/PDF + recomendaciones automáticas
- Exit status de smartctl interpretado como **bitmask** (bits 0-7) y transporte `-d TYPE` preservado
- Semántica de salud corregida: Good solo con evidencia positiva, sin falsos Critical

### Hardware avanzado (Fase B)
- Monitoreo en tiempo real con una sola sesión LibreHardwareMonitorLib 0.9.6:
  - Temperaturas
  - Load y Clock de CPU/GPU
  - Memoria GPU (SmallData)
  - Telemetría de batería (Level/Energy/Voltage/Current/Power/TimeSpan)
  - Timings SPD de RAM (en nanosegundos, sin convertir a ciclos)
- Inventario estático WMI/SMBIOS: CPU, GPU, RAM, módulos físicos, placa madre/BIOS
- Pantalla Hardware con 6 pestañas (Temperaturas, CPU/GPU, Memoria GPU, Batería, RAM SPD, Inventario)
- Estados live consistentes (Monitoreando / Sin lectura / Lectura disponible / No disponible / Sin sensores)
- Sin thresholds de salud inventados: presenta datos, no interpreta rendimiento

## Seguridad

- SMART read-only por defecto; sin comandos de escritura/configuración de discos
- Self-tests no destructivos (el test ocurre en el firmware del disco)
- Disco en estado Crítico bloquea self-tests adicionales
- Backup recomendado antes de acciones críticas
- Limpieza, optimización visual y desactivación de inicio con backup y reversión

## Correcciones

- smartctl exit status: bitmask real (antes se trataba como enum; exit 1 ya no es "éxito con warning")
- `-d TYPE` preservado en análisis y self-tests (antes se perdía el tipo detectado)
- Semántica de salud SMART: eliminados falsos Good/Critical; RawValue ya no se compara con THRESH
- NVMe: objeto `nvme_smart_health_information_log` y critical_warning numérico (antes GetString sobre número)
- CRC de interfaz ya no se trata como fallo físico crítico
- Estados Hardware: "Monitoreando" estable, sin datos stale ante excepciones, hints de pestañas correctos

## Limitaciones

- La disponibilidad de sensores depende del hardware y de los permisos (algunos requieren administrador)
- Los timings SPD pueden no estar disponibles según chipset/SMBus/driver
- La batería no aplica a equipos de escritorio (se muestra estado neutral, no error)
- smartctl (smartmontools) es dependencia externa en v0.2: se autodetecta desde `config/herramientas.json`, instalación estándar, rutas junto a la app o PATH; no se distribuye dentro del paquete
- Sin correlación automática entre inventario WMI/SMBIOS y sensores/SPD
- Windows 10 (1809+) / Windows 11; sin soporte Windows 7/8/8.1
