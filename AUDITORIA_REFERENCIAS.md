# Auditoría de Referencias - CATTECH OPTIMIZER PRO

Fecha de auditoría: 2026  
Objetivo: Analizar herramientas existentes para inspirar arquitectura y lógica sin copiar código directamente.

---

## Resumen de Herramientas Analizadas

| Herramienta | Licencia | Uso permitido en CATTECH |
|------------|----------|-------------------------|
| ChrisTitusTech/winutil | MIT | Usar solo como referencia |
| smartmontools | GPL-2.0 | Invocar binario externo |
| LibreHardwareMonitor | MPL 2.0 | Integrado vía NuGet (sensores read-only) |
| a1ive/nwinfo | Unlicense | Usar solo como referencia |
| memtest86plus | GPL-2.0 | Dejar para versión futura |
| script-wureset | MIT | Usar solo como referencia |
| Microsoft WinPE | Documentación | Dejar para versión futura |
| BlueScreenView | Freeware | No recomendado por licencia |

---

## 1. ChrisTitusTech/winutil

**Repositorio**: https://github.com/ChrisTitusTech/winutil  
**Licencia**: MIT  
**Estrellas**: 57.2k | **Forks**: 3.3k

### Qué problema resuelve
Utilidad integral de Windows que ofrece:
- Instalación de programas populares
- Tweaks de Windows (debloat, configuración)
- Solución de problemas (fixes)
- Gestión de actualizaciones de Windows
- Interfaz gráfica con PowerShell + XAML

### Qué lógica puede inspirar a CATTECH
1. **Organización modular**: Scripts PowerShell separados por función (tweaks, installs, fixes)
2. **Configuración en JSON**: Usa archivos de configuración para definir opciones
3. **Interfaz XAML**: Demuestra que PowerShell puede tener UI profesional
4. **Sistema de presets**: Perfiles predefinidos (Standard, Minimal, Advanced)
5. **Compilación centralizada**: Script Compile.ps1 que empaqueta todo

### Riesgos de licencia
- **MIT**: Muy permisiva, permite uso comercial
- No hay riesgos significativos para inspiración arquitectónica

### Uso permitido en CATTECH
✅ **Usar solo como referencia**  
- Estudiar estructura de carpetas y organización
- Analizar flujo de módulos
- Tomar ideas de UX/UI
- **NUNCA** copiar código directamente

### Lecciones clave para CATTECH
- Mantener módulos independientes y desacoplados
- Usar configuración externa para flexibilidad
- Documentar cada función con propósito claro
- Crear sistema de logging detallado

---

## 2. smartmontools/smartmontools

**Repositorio**: https://github.com/smartmontools/smartmontools  
**Licencia**: GPL-2.0  
**Estrellas**: 1.2k | **Forks**: 265

### Qué problema resuelve
Herramienta para diagnosticar estado de discos duros mediante tecnología SMART:
- smartctl: Controla y monitorea sistemas de almacenamiento
- Soporta ATA/SATA, SCSI/SAS y NVMe
- Advertencia temprana de degradación y fallos de disco
- Funciona en Windows, Linux, macOS y otros sistemas

### Qué lógica puede inspirar a CATTECH
1. **Parser de resultados SMART**: Estructura para interpretar datos de smartctl
2. **Mapeo de atributos**: Cómo interpretar valores de salud del disco
3. **Sistema de alertas**: Detección de problemas potenciales
4. **Exportación de datos**: Formato estructurado para reportes

### Riesgos de licencia
- **GPL-2.0**: Copyleft fuerte, no permite integración en software propietario
- El binario smartctl.exe es distribuido independientemente

### Uso permitido en CATTECH
⚠️ **Invocar binario externo**  
- Ejecutar smartctl.exe como proceso externo
- Capturar y parsear la salida (stdout)
- **NO** integrar código fuente de smartmontools
- **NO** vincular bibliotecas GPL

### Implementación recomendada
```
smartctl.exe -a -j /dev/sdX  →  Salida JSON  →  Parser propio  →  Modelo de datos CATTECH
```

### Consideraciones técnicas
- Requiere ejecución como administrador
- Los discos NVMe pueden requerir rutas diferentes
- Incluir smartctl.exe en carpeta `tools/` como binario independiente

### Implementación en v0.2
- **Plan detallado**: `docs/V0_2_PLAN_SMART_HARDWARE.md`
- **Decisión de integración**: `docs/SMART_INTEGRATION_DECISION.md`
- **Estrategia**: Invocar smartctl.exe como binario externo, parsear salida JSON
- **Carpeta**: `tools/smartmontools/smartctl.exe`
- **Parsing**: Usar smartctl `-j` para salida JSON (evitar dependencia de idioma)

---

## 3. LibreHardwareMonitor/LibreHardwareMonitor

**Repositorio**: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor  
**Licencia**: MPL 2.0  
**Estrellas**: 8.6k | **Forks**: 958

### Qué problema resuelve
Software de monitoreo de hardware que muestra:
- Temperaturas de CPU, GPU, discos
- Velocidades de ventiladores
- Voltajes
- Carga y relojes de CPU/GPU
- Información de tarjetas de red

### Qué lógica puede inspirar a CATTECH
1. **Arquitectura de librería**: LibreHardwareMonitorLib separada de la UI
2. **Patrón Visitor**: Para recorrer jerarquía de hardware
3. **Acceso a sensores**: APIs para cada tipo de hardware
4. **NuGet como distribución**: Librería reutilizable

### Riesgos de licencia
- **MPL 2.0**: Copyleft suave, compatible con software propietario
- Permite integración en aplicaciones comerciales
- Requiere attribution y compartir cambios en archivos MPL

### Uso permitido en CATTECH
✅ **Integración efectiva vía NuGet (Fase B.1.1)**  
- Paquete: `LibreHardwareMonitorLib` versión estable **0.9.6**
- Licencia: **MPL 2.0** (atribución en documentación)
- Integración mediante NuGet; CATTECH **no copia ni modifica código fuente** de la librería en esta fase
- Capa de abstracción propia sobre la librería (interfaces internas en Infrastructure)
- Solo lectura: no modifica valores, no controla ventiladores, no escribe configuraciones
- Algunos sensores pueden requerir privilegios administrativos

### Implementación en v0.2 (B.1.1 completada)
```csharp
// Referencia a NuGet: LibreHardwareMonitorLib 0.9.6
using LibreHardwareMonitor.Hardware;

var computer = new Computer {
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMemoryEnabled = true,
    IsMotherboardEnabled = true,
    IsStorageEnabled = true,
    IsControllerEnabled = true
};
computer.Open();
computer.Accept(new HardwareUpdateVisitor()); // UpdateVisitor no es público en 0.9.6
// Leer sensores...
```

### Consideraciones técnicas
- Requiere permisos de administrador para algunos sensores (no es error fatal; warning informativo)
- Soporta .NET Framework 4.7.2+ y .NET Standard 2.0
- En 0.9.6: `Computer` no expone `Update()` ni `UpdateVisitor` público; se implementa `IVisitor` propio
- El enum `HardwareType` de 0.9.6 no incluye `Controller`; `IsControllerEnabled` sigue disponible en `Computer`
- Se consumen `SensorType.Temperature`, `SensorType.Load` y `SensorType.Clock` (B.2.1); Load/Clock solo de hardware CPU/GPU; tipo determinado exclusivamente por `SensorType`, nunca por nombre del sensor
- Se consume `SensorType.SmallData` (B.2.2) solo de hardware GPU, conservado en MB sin interpretar Used/Free/Total por nombre; sin usage calculado
- B.2.3 habilita `Computer.IsBatteryEnabled` y consume `SensorType.Level/Energy/Voltage/Current/Power/TimeSpan` solo de hardware Battery (misma Computer/sesión/Refresh); `SensorType.Temperature` de batería entra en la lista de temperaturas con tipo "Batería"; cero/una/varias baterías soportadas; sin heurísticas por nombre ni salud calculada por CATTECH
- B.3.2 consume `SensorType.Timing` (B.3.2) solo de hardware Memory: valores en nanosegundos conservados tal cual (sin convertir a ciclos ni calcular CL), nombres preservados literalmente, sin parser por nombre, sin XMP/EXPO, sin correlación automática con WMI/SMBIOS. La sesión real expone la vista ACTUAL de `Computer.Hardware` (sin congelar la colección en Create) para permitir observar DIMM que MemoryGroup de LHM agregue tardíamente; esto no es un subsistema de hot-plug de CATTECH

### Auditoría de dependencia SPD (B.3.2)
- **Verificado en `librehardwaremonitorlib.nuspec` 0.9.6** (grupo net8.0): el acceso SPD/SMBus de LHM usa la dependencia transitiva **RAMSPDToolkit-NDD 1.4.2**, junto con DiskInfoToolkit 1.1.2, HidSharp 2.6.4, Mono.Posix.NETStandard 1.0.0, System.IO.Ports 10.0.3, System.Management 10.0.2 y System.Threading.AccessControl 10.0.3
- **Confirmado en `project.assets.json`**: RAMSPDToolkit-NDD/1.4.2 ya forma parte del grafo restaurado (dependencia transitiva del paquete ya instalado)
- CATTECH **no instala ni administra drivers ni componentes adicionales**: solo usa lo que entrega el paquete NuGet; sin UAC automático; si LHM no puede acceder al SPD, degrada a "timings no disponibles"
- MPL 2.0 del paquete: se respeta la atribución; CATTECH no copia ni modifica código fuente de la librería

---

## 4. a1ive/nwinfo

**Repositorio**: https://github.com/a1ive/nwinfo  
**Licencia**: Unlicense (dominio público)  
**Estrellas**: 608 | **Forks**: 58

### Qué problema resuelve
Utilidad de información de hardware para Windows:
- SMBIOS, CPUID, SMART, PCI, SPD, EDID
- Exportación en JSON, YAML y HTML
- Recolección directa sin depender de WMI
- Interfaz gráfica y línea de comandos

### Qué lógica puede inspirar a CATTECH
1. **Exportación multiformato**: JSON, YAML, HTML para reportes
2. **Recolección directa**: Acceso a hardware sin WMI (más confiable)
3. **Estructura de datos**: Modelo para información de hardware
4. **Formato de reporte**: Plantillas para exportación

### Riesgos de licencia
- **Unlicense**: Dominio público, sin restricciones
- Puede usarse libremente sin atribución requerida

### Uso permitido en CATTECH
✅ **Usar solo como referencia**  
- Estudiar estructura de datos de hardware
- Analizar formato de exportación JSON/HTML
- Tomar ideas para organizar información de sistema
- No ejecutar binario nwinfo directamente (preferir LibreHardwareMonitor)

### Lecciones clave para CATTECH
- Estructurar datos de hardware en formato jerárquico
- Mantener independencia de WMI cuando sea posible
- Ofrecer múltiples formatos de exportación

---

## 5. memtest86plus/memtest86plus

**Repositorio**: https://github.com/memtest86plus/memtest86plus  
**Licencia**: GPL-2.0  
**Estrellas**: 1.7k | **Forks**: 133

### Qué problema resuelve
Probador de memoria RAM standalone:
- Pruebas exhaustivas de memoria
- Soporta x86, x86-64 y LoongArch64
- No depende del sistema operativo (bootea directamente)
- Detección precisa de errores de memoria

### Qué lógica puede inspirar a CATTECH
1. **Integración futura**: Para CATTECH Rescue USB
2. **Formato de reporte**: Cómo reportar errores de memoria
3. **Pruebas de estrés**: Estrategias de testing de hardware

### Riesgos de licencia
- **GPL-2.0**: Copyleft fuerte
- El binario booteable es independiente del OS

### Uso permitido en CATTECH
⏭️ **Dejar para versión futura**  
- No implementar en MVP
- Documentar para futura integración en CATTECH Rescue USB
- El binario booteable se incluiría en USB de rescate

### Implementación futura (v0.5+)
```
CATTECH Rescue USB/
├── boot/
│   ├── memtest86plus.efi
│   └── memtest86plus.bin
└── CATTECH/
    └── (herramientas de rescate)
```

---

## 6. wureset-tools/script-wureset

**Repositorio**: https://github.com/wureset-tools/script-wureset  
**Licencia**: MIT  
**Estrellas**: 209 | **Forks**: 21

### Qué problema resuelve
Herramienta para reparar componentes de Windows Update:
- Reset completo de componentes de Windows Update
- Eliminación de archivos temporales
- Reparación del registro de Windows
- Escaneo y reparación de archivos del sistema (sfc /scannow)
- Reparación de imagen del sistema (DISM)
- Limpieza de componentes obsoletos

### Qué lógica puede inspirar a CATTECH
1. **Flujo de troubleshooting**: Pasos lógicos para reparación
2. **Reseteo de servicios**: Cómo detener, limpiar y reiniciar servicios
3. **Validación post-reparación**: Verificar que la reparación funcionó
4. **Menú de opciones**: Interfaz simple con opciones claras

### Riesgos de licencia
- **MIT**: Muy permisiva, uso comercial permitido

### Uso permitido en CATTECH
✅ **Usar solo como referencia**  
- Estudiar flujo de reparación de Windows Update
- Analizar servicios que se resetean
- Tomar estructura de menú de opciones
- **NO** copiar script batch directamente

### Servicios clave para futura implementación
- wuauserv (Windows Update)
- cryptSvc (Cryptographic Services)
- bits (Background Intelligent Transfer Service)
- msiserver (Windows Installer)

---

## 7. Microsoft WinPE (Documentación)

**Documentación**: https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/winpe-create-usb-bootable-drive  
**Tipo**: Documentación oficial de Microsoft

### Qué problema resuelve
Guía para crear medios bootables de Windows PE:
- Crear USB bootable con WinPE
- Personalizar imagen de WinPE
- Agregar drivers y actualizaciones
- Crear ISOs y VHDs bootables

### Qué lógica puede inspirar a CATTECH
1. **Flujo de creación USB**: Pasos para crear pendrive booteable
2. **Personalización**: Cómo agregar herramientas a WinPE
3. **Integración de drivers**: Proceso de inclusión de drivers
4. **Herramientas oficiales**: Copype y MakeWinPEMedia

### Riesgos de licencia
- Documentación oficial de Microsoft, no es código
- WinPE requiere licencia ADK de Microsoft

### Uso permitido en CATTECH
⏭️ **Dejar para versión futura**  
- Documentar para futura creación de CATTECH Rescue USB
- No implementar en MVP
- Requiere Windows ADK instalado

### Flujo futuro (v0.5+)
```
1. Instalar Windows ADK + WinPE Add-on
2. Ejecutar: copype amd64 C:\WinPE_CATTECH
3. Personalizar imagen:
   - Agregar herramientas CATTECH
   - Agregar drivers de red/video
   - Agregar PowerShell support
4. Crear USB: MakeWinPEMedia /UFD C:\WinPE_CATTECH X:
5. Incluir:
   - Memtest86+
   - Herramientas de diagnóstico
   - Utilidades de rescate
```

---

## 8. BlueScreenView (NirSoft)

**Página**: https://www.nirsoft.net/utils/blue_screen_view.html  
**Licencia**: Freeware (no open source)

### Qué problema resuelve
Análisis de minidumps de pantallazos azules (BSOD):
- Escanea archivos minidump
- Muestra información de crashes en tabla
- Identifica drivers que causaron el crash
- Muestra detalles del Bug Check Code
- Soporte para dumps de 32-bit y 64-bit

### Qué lógica puede inspirar a CATTECH
1. **Estructura de análisis**: Cómo parsear minidumps
2. **Reporte de crashes**: Formato para mostrar información de BSOD
3. **Identificación de drivers**: Cómo mapear direcciones de memoria a drivers

### Riesgos de licencia
- **Freeware**: No es open source
- Requiere revisión de licencia para integración
- Distribución permitida sin cargo

### Uso permitido en CATTECH
❌ **No recomendado por licencia**  
- No integrar binario de BlueScreenView
- No usar código fuente (no disponible)
- **Alternativa**: Desarrollar analizador propio de minidumps

### Alternativa propia (v0.3+)
```
CATTECH BSOD Analyzer/
├── src/
│   ├── MinidumpParser.cs      # Lee archivos .dmp
│   ├── BugCheckDecoder.cs     # Decodifica códigos de error
│   ├── DriverAnalyzer.cs      # Identifica drivers problemáticos
│   └── ReportGenerator.cs     # Genera informe legible
└── data/
    ├── BugCheckCodes.json     # Base de datos de códigos
    └── KnownDrivers.json      # Drivers conocidos problemáticos
```

---

## Resumen de Decisiones

### Herramientas para el MVP (v0.1)
| Herramienta | Uso en MVP |
|------------|------------|
| LibreHardwareMonitorLib | Detección de hardware (NuGet) |
| smartctl.exe | Diagnóstico SMART (binario externo) |
| PowerShell scripts | Limpieza y optimización segura |

### Herramientas para futuras versiones
| Herramienta | Versión | Uso |
|------------|---------|-----|
| smartctl.exe (análisis completo) | v0.2 | SMART detallado |
| nwinfo (referencia) | v0.2 | Estructura de datos |
| BSOD Analyzer propio | v0.3 | Análisis de minidumps |
| Memtest86+ | v0.5 | CATTECH Rescue USB |
| WinPE | v0.5 | CATTECH Rescue USB |

### Filosofía de integración
1. **Preferir librerías NuGet** sobre binarios externos
2. **Invocar procesos** solo cuando sea necesario
3. **Desarrollar propio** cuando la licencia no permita integración
4. **Documentar decisiones** de licencia en el código

---

*Documento generado como parte de la planificación de CATTECH OPTIMIZER PRO*
