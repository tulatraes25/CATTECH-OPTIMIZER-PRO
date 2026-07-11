# Decisión de Integración: smartmontools/smartctl

**Fecha**: 2026  
**Estado**: Decisión tomada

---

## Resumen

Decidimos usar `smartctl` de smartmontools como **binario externo** para el diagnóstico SMART, sin integrar código fuente GPL en nuestro proyecto MIT.

---

## Por qué usar smartmontools

1. **Estándar de la industria**: smartctl es la herramienta más utilizada para diagnóstico SMART en Windows/Linux
2. **Soporte completo**: ATA/SATA, SCSI/SAS, NVMe, USB (si informa SMART)
3. **Salida estructurada**: Soporta salida JSON (`-j`), ideal para parsing
4. **Test SMART**: Corto y extendido integrados
5. **Multiplataforma**: Windows, Linux, macOS
6. **Activo**: Mantenimiento continuo, actualizaciones regulares

---

## Licencia GPL-2.0 y Consecuencias

### Qué es GPL-2.0
- Licencia copyleft fuerte
- Si se distribuye código GPL, el código derivado también debe ser GPL
- No permite integración en software propietario sin abrir el código fuente

### Consecuencias para CATTECH
- **NO podemos** integrar el código fuente de smartmontools en nuestro proyecto MIT
- **NO podemos** copiar funciones de smartctl a nuestro código
- **SÍ podemos** ejecutar smartctl.exe como proceso externo
- **SÍ podemos** parsear la salida (texto/JSON) de smartctl
- **SÍ podemos** distribuir smartctl.exe junto con nuestra aplicación

### Por qué no usar otra alternativa
- No existe otra herramienta SMART tan completa y madura
- Usar WMI para SMART limita la información disponible
- Desarrollar nuestro propio parser SMART sería reinventar la rueda

---

## Decisión: Binario Externo

### Estrategia elegida
```
CATTECH OPTIMIZER PRO (MIT)
        │
        │ ejecuta como proceso
        ▼
smartctl.exe (GPL-2.0, binario independiente)
        │
        │ retorna JSON
        ▼
Parser propio (MIT) → SmartDiskReport
```

### Ventajas
- Sin conflicto de licencias
- smartctl.exe se distribuye como archivo independiente
- Actualizable sin recompilar CATTECH
- Parsing de JSON controlado por nosotros
- No dependemos de compilación de smartctl

### Limitaciones
- Requiere smartctl.exe presente en el sistema
- El usuario debe tener permisos para acceder a discos
- La salida puede variar entre versiones de smartctl
- Algunos discos USB pueden no informar SMART

---

## Carpeta Sugerida

```
tools/
├── smartmontools/
│   ├── smartctl.exe          # Binario principal
│   ├── smartctl.exe.config   # Configuración (si aplica)
│   └── README.md             # Licencia GPL-2.0 y atribución
```

### Alternativa: Ruta configurable

Permitir al técnico configurar la ruta a smartctl.exe en:
```
config/appsettings.json → SmartctlPath
```

Si no se configura, buscar en:
1. `tools/smartmontools/smartctl.exe` (junto a la app)
2. PATH del sistema
3. Rutas comunes de smartmontools

---

## Manejo de Ausencia de smartctl

```
┌─────────────────────────────────────────┐
│ smartctl.exe no encontrado              │
├─────────────────────────────────────────┤
│ 1. Mostrar mensaje claro en UI          │
│ 2. Deshabilitar botón de análisis SMART │
│ 3. Ofrecer opciones:                    │
│    a. Descargar smartctl                │
│    b. Configurar ruta manualmente       │
│    c. Continuar sin SMART               │
│ 4. No bloquear otras funciones         │
└─────────────────────────────────────────┘
```

---

## Registro de Versión de smartctl

Registrar la versión de smartctl usada en:
- Informe HTML/PDF: sección de discos
- Log de la aplicación
- Campo `SmartctlVersion` en `SmartDiskReport`

Obtener versión: `smartctl --version`

---

## Parsing de Salida JSON

### Formato de salida
```bash
smartctl -a -j /dev/sda
```

Retorna JSON con estructura:
```json
{
  "json_format_version": [1, 0],
  "smartctl": { "version": [], "exit_status": 0 },
  "device": { "name": "/dev/sda", "info_name": "...", "type": "...", "protocol": "..." },
  "model_family": "...",
  "model_name": "...",
  "serial_number": "...",
  "firmware_version": "...",
  "smart_status": { "passed": true },
  "smart_data": {
    "table": [
      {
        "id": 5,
        "name": "Reallocated Sector Ct",
        "value": 100,
        "worst": 100,
        "thresh": 10,
        "failed": false,
        "flags": { "value": 50, "string": "PO--CK ", ... }
      }
    ]
  },
  "temperature": { "current": 35 },
  "power_on_time": { "hours": 12345 },
  "power_cycle_count": { "value": 567 }
}
```

### Atributos relevantes a extraer

| ID | Nombre | Descripción | Umbral Crítico |
|----|--------|-------------|----------------|
| 1 | Raw Read Error Rate | Tasa de errores de lectura | > 0 |
| 3 | Spin Up Time | Tiempo de arranque del disco | > valor peor |
| 5 | Reallocated Sector Count | Sectores reasignados | > 0 |
| 9 | Power-On Hours | Horas encendido | Informativo |
| 12 | Power Cycle Count | Ciclos de encendido | Informativo |
| 187 | Reported Uncorrectable Errors | Errores no corregibles | > 0 |
| 188 | Command Timeout | Timeouts de comandos | > 0 |
| 197 | Current Pending Sector | Sectores pendientes | > 0 |
| 198 | Offline Uncorrectable | Sectores offline no corregibles | > 0 |

### Para SSD/NVMe

| Atributo | Descripción |
|----------|-------------|
| Percentage Used | Porcentaje de vida útil usado |
| Available Spare | Espacio de repuesto disponible |
| Data Units Written | Unidades de datos escritas |
| Data Units Read | Unidades de datos leídas |

---

## Evitar Dependencia de Idioma

### Problema
La salida textual de smartctl puede variar entre idiomas (español/inglés).

### Solución
Usar **exclusivamente** la salida JSON (`-j`):
- Los nombres de campo son siempre en inglés
- Los valores numéricos son universales
- No dependemos de traducciones
- Parsing predecible y confiable

```bash
# ❌ NO usar salida textual
smartctl -a /dev/sda

# ✅ USAR salida JSON
smartctl -a -j /dev/sda
```

---

## Integración con Informe HTML/PDF

### Nueva sección: "Estado de Discos"

```
┌─────────────────────────────────────────────┐
│ ESTADO DE DISCOS                            │
├─────────────────────────────────────────────┤
│ Disco 1: Samsung SSD 980 PRO (NVMe)        │
│ Estado: ✅ Bueno                             │
│ Temperatura: 35°C                            │
│ Horas de uso: 12,345                         │
│ Vida útil usada: 12%                         │
│ Sectores reasignados: 0                      │
│                                             │
│ Disco 2: Seagate Barracuda (HDD)            │
│ Estado: ⚠️ Precaución                        │
│ Temperatura: 42°C                            │
│ Horas de uso: 45,678                         │
│ Sectores reasignados: 5                      │
│ Sectores pendientes: 2                       │
└─────────────────────────────────────────────┘
```

---

*Decisión de integración smartmontools - CATTECH OPTIMIZER PRO*
