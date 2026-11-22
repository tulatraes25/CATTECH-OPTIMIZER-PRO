# CATTECH OPTIMIZER PRO

[![CATTECH CI](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/actions/workflows/ci.yml/badge.svg)](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/actions/workflows/ci.yml)

**Versión**: v0.3.1  
**Licencia**: MIT  
**Plataforma**: Windows 11 y ediciones Windows 10 LTSC admitidas oficialmente por .NET 10; otras ediciones de Windows 10 requieren verificación propia.  
**Runtime**: .NET 10 LTS. El paquete publicado es autocontenido (win-x64): no requiere instalar .NET.

> **Descarga**: [v0.3.1](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/releases/tag/v0.3.1) — ZIP autocontenido win-x64 + SHA-256.
>
> Soporte oficial de plataforma .NET 10: https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md .

> Historial completo de cambios: [CHANGELOG.md](CHANGELOG.md).

---

## Descripción

CATTECH OPTIMIZER PRO es una aplicación de escritorio para Windows diseñada para técnicos informáticos. Ofrece herramientas de diagnóstico, optimización y mantenimiento de equipos, generando informes profesionales en PDF con datos de la empresa y técnico.

### Características principales (v0.3.1)

- **Configuración personalizable**: Logo de empresa, datos del técnico
- **Gestión de clientes**: Registro de clientes y equipos, con detección WMI
- **Diagnóstico del sistema**: hardware, discos, inicio, seguridad y espacios
- **Discos SMART**: análisis read-only, self-tests corto/extendido y recomendaciones
- **Hardware en tiempo real**: temperaturas, carga, memoria GPU, batería y SPD
- **Pantallas azules**: lectura de dump/Event Log y decodificación de bug checks
- **Reparaciones**: vistas previas de Windows Update, SFC/DISM y restauración, con consentimiento
- **Decisiones · DEMO**: centro de decisiones explicable sobre un caso de ejemplo
- **Optimización segura**: limpieza de temporales, ajustes visuales y punto de restauración
- **Informes profesionales**: HTML y PDF con logo, historial de casos y trazabilidad

### UX para técnicos avanzados (v0.3.1)

La interfaz se rediseñó para trabajo técnico real, no para una demo:

- **Barra de estado global** (24 px): `Admin: sí/no`, build de Windows, arquitectura, runtime .NET, ruta de datos con **Abrir datos/Copiar**, estado de `smartctl` (config, autodetección o OK) y versión de la app con panel **Información**.
- **Panel técnico en Inicio**: sistema (equipo, usuario, admin, Windows, runtime, rutas, smartctl), contadores reales de almacenamiento (informes, diagnósticos, análisis de inicio, backups, limpiezas, optimizaciones, puntos de restauración, HTML) y acciones rápidas. F5 actualiza el panel.
- **Atajos**: `F5` ejecuta la acción principal de la sección visible y `Ctrl+0..9` navega entre secciones.
- **Configuración → Herramientas externas**: editor de `smartctl.exe` (ruta, Examinar, Autodetectar, **Probar** y **Guardar** atómico de `config/herramientas.json`) con estado real del binario y rutas de datos/backups/reports/logs.
- **Diagnóstico**: muestra diagnóstico **parcial** cuando una fuente falla (con lista de errores), JSON técnico en Consolas, **Copiar resumen**, **Exportar JSON** y abrir carpeta de diagnósticos.
- **Hardware**: **Copiar tabla**/**Exportar CSV** (sensores live + inventario WMI) y panel de diagnóstico técnico (proveedor, conteos válidos, warnings/errores).
- **SMART**: verificación automática de smartctl al entrar, grilla técnica (transporte `-d`, horas, ciclos, reasignados, pendientes, CRC, UDMA, NVMe %, serial, firmware), pestaña de **atributos crudos** (ID, valor, peor, umbral, raw, flags) y **sesiones self-test** persistidas; **Copiar informe** y **Exportar CSV**.
- **Programas de inicio**: filtros y búsqueda que funcionan de verdad, detalle técnico con comando/clave completos, **Copiar comando**, **Copiar entrada** y **Abrir ubicación** (regedit/explorer/taskschd).
- **Limpieza**: rutas completas con IDs técnicos, **historial** de limpiezas persistidas y apertura de carpetas de resultados.
- **Pantallas azules**: **Abrir carpeta de minidumps**, **Abrir Visor de eventos** y **Copiar análisis**.
- **Reparaciones**: comandos exactos del plan en panel monoespaciado + **Copiar comandos**/**Copiar plan** (la ejecución automática sigue fuera de alcance).
- **Informes**: panel de estado técnico (conteos, ruta `reports/`, archivos dañados listados), preselección de secciones con datos reales (incluye historial de limpieza/restauración y resultados de optimización visual) y motivos específicos cuando una revisión falla.
- **Manejo global de errores**: un fallo inesperado no cierra la app; queda registrado en `logs/app-YYYYMMDD.log` y se informa al técnico.

### Correcciones v0.3.1 (botones que antes no ejecutaban)

Esta versión corrige la causa raíz de las pantallas que "no hacían nada":

- **Limpieza**: ahora borra temporales de las subcarpetas (antes solo hijos directos), sin exigir 7 días de antigüedad, y pide confirmación con la cantidad real de archivos y MB. Incluye **vaciado de la Papelera de reciclaje** mediante la API de Windows (antes estaba deshabilitado).
- **Programas de inicio**: la desactivación ahora elimina de verdad el valor del registro (la clave se abría en solo lectura y el borrado fallaba siempre), permite seleccionar cualquier entrada de terceros (el clasificador marcaba todo como "Microsoft"), conserva el tipo de valor (`REG_EXPAND_SZ`) y la restauración desde Backups funciona.
- **Punto de restauración**: se eliminó la comprobación del servicio `srservice` (no existe en Windows 10/11) que mantenía "Crear" deshabilitado; la protección del sistema es informativa y ya no bloquea el intento.
- **Optimización visual**: los checkboxes ya quedan seleccionados (se cancelaban solos), "Aplicar" ejecuta y "Restaurar" usa el backup correcto, incluidos valores binarios.
- **SMART**: al abrir la pantalla se verifica smartctl automáticamente; "Analizar" informa errores reales en lugar de un éxito vacío y muestra el estado de la operación.
- **Informes**: carga automática de datos al entrar, "Abrir carpeta" crea la carpeta y avisa si el informe o PDF ya no existe.
- **Diálogos de confirmación** en Limpieza y Programas de inicio, con el detalle exacto de lo que se va a modificar.

### Novedades v0.3.0

- **Migración a .NET 10 LTS**: runtime soportado, build sin warnings y paquete win-x64 autocontenido
- **Pantallas azules (BSOD)**: descubrimiento read-only de minidumps y eventos `BugCheck`, decodificación desde catálogo (`config/bugcheckcodes.json`), verificación opcional con DumpChk y separación explícita entre síntomas y causa
- **Centro de reparaciones**: plan y preflight de reparaciones (Windows Update, SFC/DISM, restauración) con niveles de permiso, consentimiento explícito y verificación posterior; nunca ejecuta reparaciones sin confirmación del técnico
- **Evidence Engine**: hechos, reglas deterministas, vista previa de reparación y evaluación de resultado, aislados por cliente, equipo, sesión y componente; prohíbe conclusiones de éxito fabricadas
- **Pasaporte técnico**: exportación redactada en memoria, con checksum y consentimiento revocable por caso (superficie de revisión)
- **Informes endurecidos**: publicación atómica de bundles, control de exportaciones HTML/PDF, IDs desambiguados e inventario honesto de archivos dañados
- **Limpieza verificada**: raíces confiables, exclusiones por edad/atributos, auditoría inmutable y feedback que distingue omitidos, errores y fallas
- **2055 tests** automatizados (0 fallas, 0 omitidos)

### Correcciones v0.2.2

- Informes HTML/PDF siempre utilizan el snapshot actual de datos y ya no reutilizan HTML stale
- Validación para impedir omisión silenciosa de Cliente/Diagnóstico
- Inventario GPU usa DXGI DedicatedVideoMemory para memoria dedicada correcta en GPUs modernas
- La memoria compartida del sistema no se presenta como VRAM dedicada
- Si DXGI no puede correlacionarse de forma fiable, se muestra N/D en vez de un valor potencialmente incorrecto
- Release-gate para detectar mojibake UTF-8 antes de publicar
- 965 tests

### Correcciones v0.2.1

- Corregido el crash al abrir la sección Cliente/equipo
- Corregidos recursos XAML faltantes detectados preventivamente en Configuración, Diagnóstico y Optimización
- Agregado smoke test WPF STA para cargar las vistas principales
- Sin cambios funcionales en SMART/Hardware respecto de v0.2.0

### Novedades v0.2.0 (SMART + Hardware avanzado)

- **SMART completo**: análisis read-only por disco, estados Bueno/Precaución/Crítico/No disponible/Desconocido, self-tests corto y extendido con persistencia, sección SMART en informes HTML/PDF, recomendaciones automáticas, exit status de smartctl interpretado como bitmask, transporte `-d TYPE` preservado
- **Hardware en tiempo real**: temperaturas, Load/Clock de CPU/GPU, memoria GPU, batería y timings SPD con una sola sesión LibreHardwareMonitor; inventario estático WMI/SMBIOS (CPU, GPU, RAM, módulos, placa madre/BIOS); sin thresholds de salud inventados
- **Dependencia externa smartctl**: smartmontools/smartctl se detecta desde ruta configurada (`config/herramientas.json`), instalación estándar, rutas junto a la app o PATH. No se distribuye dentro del paquete en v0.2.0.

---

## Requisitos del sistema

### Para ejecutar

- **Windows 11** o **Windows 10 LTSC** (ediciones admitidas oficialmente por .NET 10)
- El ZIP publicado es **autocontenido**: incluye el runtime .NET 10, no hace falta instalarlo
- 512 MB RAM mínimo (1 GB recomendado)
- 100 MB de espacio en disco
- Permisos de administrador (para diagnóstico completo y operaciones que modifican el sistema)

> **Nota**: .NET 8 alcanzó su fin de soporte el 10-nov-2026, por eso v0.3.x migra a .NET 10 LTS. Windows 7/8/8.1 no son soportados; para esos sistemas se requeriría una rama legacy con .NET Framework 4.8 (no planificada).

### Para desarrollar

- Visual Studio 2022 17.10+ (o JetBrains Rider 2024.1+) con la carga de trabajo de escritorio .NET
- **.NET 10 SDK** (10.0.x)
- Git

---

## Compilación y Ejecución

### Usando Visual Studio

1. Abrir `src\Cattech.Optimizer.Pro.sln` en Visual Studio 2022
2. Seleccionar configuración `Debug` o `Release`
3. Presionar `F5` o click en "Start"

### Usando línea de comandos

```bash
# Navegar al directorio src
cd src

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build --configuration Release

# Ejecutar
dotnet run --project Cattech.Optimizer.Pro.UI
```

### Crear build portable

```bash
# Build self-contained (no requiere .NET Runtime instalado)
dotnet publish src\Cattech.Optimizer.Pro.UI\Cattech.Optimizer.Pro.UI.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output output\release

# El ejecutable estará en: output\release\Cattech.Optimizer.Pro.UI.exe
```

---

## Instalación

### Versión con instalador (recomendada para técnicos)

1. Descargar `CATTECH-Optimizer-Pro-v0.3.1-Setup.exe` desde [Releases](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/releases)
2. Ejecutar el asistente y elegir el modo de instalación:
   - **Solo para mí**: `%LOCALAPPDATA%\Programs\CATTECH OPTIMIZER PRO`. No pide UAC y la carpeta es escribible, por lo que informes y respaldos funcionan sin permisos especiales. **Recomendado**.
   - **Para todos los usuarios**: `C:\Program Files\CATTECH OPTIMIZER PRO`. Requiere administrador; ejecutá la app como administrador para que pueda escribir informes y respaldos.
3. El instalador crea el acceso directo en el menú Inicio (y opcionalmente en el escritorio) y la entrada de desinstalación en Windows.

> La desinstalación elimina el programa pero **conserva** `data\`, `reports\` y `backups\` con el historial de trabajo.

### Versión portable (sin instalador)

1. Descargar el ZIP `CATTECH-Optimizer-Pro-v0.3.1-win-x64.zip` desde [Releases](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/releases)
2. Verificar el SHA-256 publicado junto al ZIP
3. Extraer el ZIP en una carpeta escribible (por ejemplo `C:\CATTECH`)
4. Ejecutar `Cattech.Optimizer.Pro.UI.exe` como administrador

---

## Uso rápido

1. **Configurar empresa**: Ir a ⚙️ Configuración y agregar datos de la empresa/técnico
2. **Nuevo cliente**: Click en "Nuevo Servicio" y completar datos
3. **Diagnosticar**: Ir a 🔍 Diagnóstico y ejecutar análisis
4. **Optimizar**: Seleccionar optimizaciones deseadas (con confirmación)
5. **Generar informe**: Ir a 📊 Informes y generar PDF

## Configuración de empresa/técnico

Antes de usar la herramienta, configurá los datos de tu empresa en **⚙️ Configuración**. Estos datos aparecerán en todos los informes HTML y PDF generados.

**Campos disponibles:**
- Nombre comercial y técnico responsable (obligatorios)
- CUIT/DNI
- Teléfono y WhatsApp
- Email (con validación de formato)
- Dirección y Ciudad
- Logo de la empresa (PNG/JPG)
- Color principal de la marca (hex)
- Leyenda del pie de informe

La configuración se guarda automáticamente en `config/empresa.json`.

## Cliente y equipo

Para registrar un nuevo servicio, ir a **👤 Cliente y equipo** y completar:

**Datos del cliente:**
- Nombre (obligatorio), teléfono, email, empresa, dirección, observaciones

**Datos del equipo:**
- Marca, modelo, número de serie
- Tipo de equipo (Notebook / PC de escritorio / All-in-One / Otro)
- Motivo del servicio (obligatorio)
- Observaciones del equipo

**Detección automática:**
El botón **🔍 Detectar datos del equipo** identifica sin modificar el sistema:
- SO, edición, arquitectura, procesador, RAM
- Disco principal, capacidad, espacio libre, tipo de disco
- Nombre del equipo, usuario actual

Los reportes se guardan en `data/service-reports/service-report-YYYYMMDD-HHMMSS.json`.

## Diagnóstico rápido

Ir a **🔍 Diagnóstico** para ejecutar un análisis no invasivo del equipo. No se modifica nada en Windows.

**Qué analiza:**
- Sistema operativo, edición, arquitectura
- Procesador, RAM total y uso
- Disco principal, tipo (HDD/SSD/NVMe), capacidad, espacio libre
- Programas de inicio (HKCU, HKLM, carpetas de inicio)
- Tamaño estimado de archivos temporales
- Antivirus, Firewall, Windows Update
- Memoria virtual

**Alertas automáticas:**
- RAM baja (≤4 GB), RAM justa (4-8 GB)
- Poco espacio en disco (<15% libre)
- Disco HDD: recomendar SSD
- Muchos programas al inicio (>10)
- Temporales altos (>2 GB)
- Windows fuera de objetivo (no Win10/11)

Los diagnósticos se guardan en `data/diagnostics/diagnostic-YYYYMMDD-HHMMSS.json`.

## Pantallas azules (BSOD)

Ir a **🟦 Pantallas azules** para revisar caídas del sistema de forma **read-only**.

**Qué hace:**
- Descubre minidumps y eventos `BugCheck` del registro de eventos de Windows
- Decodifica el código de bug check con `config/bugcheckcodes.json` (nombre y descripción conservadora)
- Lista módulos candidatos y estado de disponibilidad de cada fuente, sin atribuir causa automáticamente
- Permite verificación opcional con **DumpChk** cuando la herramienta está disponible y el técnico lo solicita
- No borra, mueve ni repara archivos de volcado

Los resultados se muestran en pantalla y pueden incluirse en el informe; no se ejecutan reparaciones desde esta sección.

## Reparaciones (vista previa)

Ir a **🛠️ Reparaciones (vista previa)** para preparar y revisar reparaciones comunes **sin ejecutarlas automáticamente**.

**Qué hace:**
- Verifica preflight de **Windows Update**, **SFC/DISM** y **punto de restauración**: permisos, disponibilidad de servicios y límites
- Genera un plan con comandos propuestos, riesgos y criterios de verificación posterior
- Requiere consentimiento explícito del técnico para cualquier ejecución y registra el resultado
- No ejecuta reparaciones por una orden genérica ni desactiva Defender/firewall como "optimización"

> La vista previa es deliberadamente conservadora: si el entorno no permite verificar el resultado, lo informa en vez de asumir éxito.

## Decisiones · DEMO

Ir a **🧭 Decisiones · DEMO** para ver el **Decision Center**: un caso de demostración que explica cómo se combinan hechos, reglas deterministas y recomendaciones prudentes.

- Separa **técnico** y **cliente** en el texto de cada recomendación
- No diagnostica hardware real: es una superficie de demostración, no un reemplazo del diagnóstico
- Prohíbe conclusiones de éxito fabricadas y mantiene separados cliente, equipo, sesión y componente

## Programas de inicio

Ir a **🚀 Programas de inicio** para ver y analizar todos los programas que se ejecutan al iniciar Windows. Solo lectura: no se desactiva nada.

**Fuentes analizadas:**
- Registry: HKCU/HKLM Run y RunOnce
- Carpetas de inicio del usuario y comunes
- Tareas programadas de inicio de sesión (solo lectura)

**Para cada entrada se muestra:**
- Nombre, comando/ruta, origen, ubicación exacta
- Editor detectado (Microsoft, Google, NVIDIA, etc.)
- Es Microsoft probable: Sí/No
- Riesgo estimado: Bajo / Medio / Alto
- Recomendación: Mantener / Revisar / Posible desactivar

**Filtros disponibles:**
- Todos, No Microsoft, Revisar, Posible desactivar, Alertas

**Alertas automáticas:**
- Rutas inexistentes
- Comandos en Temp/AppData sospechosos
- Editor desconocido
- Entradas duplicadas

Los análisis se guardan en `data/startup-analysis/startup-analysis-YYYYMMDD-HHMMSS.json`.

## Desactivación segura de programas de inicio

Desde **🚀 Programas de inicio**, seleccioná entradas y desactivarlas con backup y reversión.

**Cómo funciona:**
1. Analizar programas de inicio
2. Seleccionar entradas con checkboxes (solo terceros)
3. Click en "Desactivar seleccionados"
4. Confirmar en el diálogo
5. Se crea backup y se desactivan las entradas

**Fuentes desactivables:**
- Registry Run (HKCU y HKLM)
- Carpetas de inicio (usuario y comunes)

**NO desactiva (por seguridad):**
- Entradas de Microsoft (bloqueadas)
- RunOnce
- Tareas programadas

**Estrategia de backup:**
- **Registro**: Se mueve a `HKCU/HKLM\Software\CATTECH\OptimizerPro\DisabledStartup\Run`
- **Archivos**: Se mueven a `backups/startup/YYYYMMDD-HHMMSS/`
- **Reversión**: Click en "Backups" → seleccionar → "Restaurar"

Los backups se guardan en `backups/startup/startup-backups.json`.

## Limpieza segura de temporales

Ir a **🧹 Limpieza** para escanear y limpiar archivos temporales de forma segura.

**Ubicaciones limpiables:**
- `%TEMP%` del usuario (seleccionado por defecto)
- `C:\Windows\Temp` (requiere permisos de admin)
- Miniaturas de Explorer (opcional)
- Papelera de reciclaje (opcional, no seleccionada por defecto)

**NO se limpia (por seguridad):**
- Descargas, Documentos, Escritorio
- AppData completo
- Perfiles de navegador
- WinSxS, System32, Program Files

**Cómo funciona:**
1. Click en "Escanear temporales"
2. Revisar ubicaciones detectadas y tamaño
3. Seleccionar qué limpiar
4. Click en "Limpiar seleccionados"
5. Confirmar en el diálogo
6. Ver resultado: espacio liberado, omitidos, errores

Los resultados se guardan en `data/cleanup-results/cleanup-result-YYYYMMDD-HHMMSS.json`.

## Optimización visual segura

Ir a **⚡ Optimización** para aplicar ajustes visuales que mejoran el rendimiento.

**Ajustes disponibles:**
- Desactivar animaciones de ventanas
- Desactivar animaciones de menús
- Desactivar sombras del mouse
- Configurar mostrar contenido al arrastrar
- Mantener suavizado de fuentes (ClearType)
- Desactivar Aero Peek
- Desactivar animaciones de minimizar/maximizar

**Cómo funciona:**
1. Click en "Analizar ajustes"
2. Revisar estado actual y valores recomendados
3. Seleccionar ajustes a aplicar
4. Click en "Aplicar seleccionados"
5. Confirmar en el diálogo
6. Ver resultado: aplicados, omitidos, fallidos

**Seguridad:**
- Backup de cada valor antes de modificar
- Reversión desde backups
- No se cambia resolución, drivers ni accesibilidad
- Requiere reinicio/cierre de sesión para algunos ajustes

Los backups se guardan en `backups/visual/visual-backups.json`.

## Punto de restauración

Ir a **🛡️ Punto de restauración** para crear un punto de restauración de Windows antes de aplicar cambios importantes.

**Cómo funciona:**
1. Click en "Verificar estado"
2. Revisar: permisos, servicio, protección del sistema
3. Ingresar nombre descriptivo
4. Click en "Crear punto de restauración"
5. Confirmar en el diálogo
6. Ver resultado

**Estado verificado:**
- Permisos de administrador
- Disponibilidad del servicio de Restaurar sistema
- Estado de la protección del sistema

**Errores manejados:**
- Permisos insuficientes
- Protección del sistema deshabilitada
- Frecuencia limitada por Windows
- Servicio no disponible

Los resultados se guardan en `data/restore-points/restore-point-result-YYYYMMDD-HHMMSS.json`.

## Hardware (tiempo real)

Ir a **🌡️ Hardware** para monitorear el hardware en tiempo real mediante LibreHardwareMonitorLib (solo lectura), todo desde **una única captura live** por muestra.

**Funcionalidades:**
- **Actualizar una vez**: lee un snapshot live único (temperaturas + Load/Clock + memoria GPU + batería + timings SPD juntos)
- **Iniciar monitoreo / Detener**: muestreo periódico (cada 2 segundos) reutilizando una sola sesión; se cancela automáticamente al salir de la sección
- **5 pestañas live**:
  - **Temperaturas**: Tipo, Hardware, Sensor, Actual, Mín., Máx. (N/D nunca 0 °C)
  - **CPU / GPU**: Load y Clock con sus unidades (% y MHz), métrica mostrada como Carga/Frecuencia
  - **Memoria GPU**: SmallData en MB, filas independientes ("GPU Memory Used/Free/Total" literales, sin usage calculado)
  - **Batería**: Nivel/Energía/Voltaje/Corriente/Potencia/Tiempo con sus unidades; sin salud ni estado de carga
  - **RAM SPD**: timings en nanosegundos (14,00 | ns — nunca CL14), nombres preservados
- **Pestaña Inventario (B.4.2)**: consulta manual e independiente de datos estáticos WMI/SMBIOS — CPU (nombre, fabricante, núcleos/hilos, velocidad reportada), GPU (memoria reportada), RAM (total, tipo, velocidad configurada, slots), módulos físicos (slot, banco, fabricante, part number, serie, capacidad, tipo, widths, rank) y placa madre/BIOS. Ejecutada en background; no se consulta durante el monitoreo live; no incluye discos ni SO; valores ausentes → N/D
- **Resumen compacto**: cantidad de sensores por familia (sin porcentajes semánticos)
- **Warnings y errores controlados**: batería/SPD vacíos no son errores; sin elevación → aviso informativo

**Consideraciones:**
- No interpreta salud ni rendimiento: sin colores de alerta, thresholds ni estados Hot/Crítico
- No selecciona sensores semánticos (CPU Total, GPU Core): muestra todo lo que entrega el backend
- El monitoreo nunca se inicia automáticamente: requiere acción explícita del técnico
- El inventario WMI/SMBIOS y la telemetría live son fuentes separadas: no se correlacionan entre sí
- Los estados live distinguen "sin lectura" (no consultado), "lectura disponible/no disponible" y "sin sensores disponibles" (consulta válida vacía); durante el monitoreo activo el estado permanece "Monitoreando"

## Memoria RAM (inventario)

El inventario de memoria RAM se obtiene mediante WMI/SMBIOS (B.3.1):

- **Módulos instalados**: slot (DeviceLocator), banco, fabricante, part number, serial
- **Capacidad** exacta en bytes por módulo (con equivalente en GB)
- **Velocidad configurada** en MHz (ConfiguredClockSpeed)
- **Tipo de memoria** desde SMBIOSMemoryType verificado contra la spec DMTF: DDR, DDR2, DDR3, DDR4, DDR5, LPDDR, LPDDR2, LPDDR3, LPDDR4, LPDDR5 (código desconocido → "Desconocida", sin inventar tipo por velocidad)
- **Anchos**: data width y total width en bits; rank desde Attributes
- **Topología de slots**: usados (módulos con capacidad válida) y totales (suma de arrays de memoria de sistema)
- Resúmenes: SpeedMHz solo si todos los módulos válidos coinciden; Type uniforme, "Mixta" o "Desconocida"

**Timings SPD (B.3.2):** los timings que LibreHardwareMonitor expone como `SensorType.Timing` para hardware Memory se capturan en el snapshot live con su unidad real en **nanosegundos** (ej: tAA = 14.0 ns, no CL14): sin conversión a ciclos, sin calcular CL, nombres preservados literalmente, sin XMP/EXPO, sin correlación automática con el inventario WMI. La sesión expone la vista actual del hardware, por lo que un DIMM detectado tardíamente por LHM puede aparecer en snapshots posteriores. Si el equipo o los permisos no permiten leer SPD, los timings simplemente quedan vacíos (no se asume RAM defectuosa). El acceso SPD usa la dependencia transitiva RAMSPDToolkit-NDD del paquete; CATTECH no instala drivers.

## Informe técnico HTML

Ir a **📊 Informes** para generar un informe profesional en HTML.

**Secciones del informe:**
1. Portada con logo, empresa, técnico y fecha
2. Datos del cliente y equipo
3. Diagnóstico inicial (RAM, disco, inicio, seguridad)
4. Acciones realizadas (limpieza, optimización, restauración)
5. Resultados y espacio liberado
6. Estado SMART de discos (si hay análisis guardado)
7. Recomendaciones automáticas
8. Observaciones finales del técnico
9. Firma

**Cómo funciona:**
1. Click en "Cargar datos" para buscar información disponible
2. Seleccionar qué secciones incluir
3. Seleccionar datos específicos (cliente, diagnóstico, análisis SMART, etc.)
4. Agregar observaciones finales
5. Click en "Generar informe"
6. Abrir y verificar el HTML generado

**Estado SMART de discos:**
El informe puede incluir un análisis SMART persistido: estado por disco (Bueno/Precaución/Crítico/No disponible), métricas ATA y NVMe, y recomendación de backup. El informe usa únicamente resultados ya guardados, no ejecuta smartctl.

**Pruebas SMART (Self-Test):**
También puede incluir sesiones de self-test Short/Extended persistidas, seleccionadas manualmente por el técnico. Las sesiones no están vinculadas automáticamente al cliente o servicio, por lo que solo se incluyen las seleccionadas explícitamente para el informe.

**Recomendaciones automáticas:**
- RAM ≤ 4 GB: recomendar ampliar
- Disco HDD: recomendar SSD
- Espacio libre < 15%: recomendar liberar
- Muchos programas al inicio: recomendar reducir
- Temporales > 2 GB: recomendar mantenimiento
- Estado SMART: crítico → backup prioritario y evaluación de reemplazo; backup recomendado → priorizar respaldo; advertencia → revisar indicadores; no disponible/no determinado → estado no concluyente (no asume salud)
- Self-tests seleccionados: completado con errores → backup y evaluación; en ejecución → esperar resultado final; no soportado → no determina salud; no iniciado → verificar soporte; abortado/interrumpido/desconocido → resultado no concluyente; última consulta fallida → estado posiblemente desactualizado

Las recomendaciones SMART solo se generan si la sección correspondiente fue incluida, y confían en los estados ya calculados por el análisis (no reinterpretan atributos raw).

Los informes se guardan en `reports/html/Informe_Tecnico_CATTECH_Cliente_YYYYMMDD-HHMMSS.html`.

**Trazabilidad y privacidad:**
- Los bundles de informe se publican de forma atómica y con checksum, de modo que un archivo a medio escribir nunca se presenta como válido
- La revisión de origen exige confirmar el caso y el origen de los datos antes de exportar; si el caso cambia, la selección y los consentimientos previos se revocan
- El pasaporte técnico se exporta **redactado en memoria** (sin escribir datos privados a disco) y su consentimiento es revocable por caso

## Exportación a PDF

Desde **📊 Informes**, podés exportar el informe HTML a PDF en formato A4.

**Cómo funciona:**
1. Generar el informe HTML primero (o hacerlo automáticamente)
2. Click en "Exportar PDF"
3. El PDF se guarda en `reports/pdf/`
4. Abrir y verificar el PDF generado

**Nombre del PDF:**
`Informe_Tecnico_CATTECH_Cliente_YYYYMMDD-HHMMSS.pdf`

**Requisitos:**
- Microsoft Edge instalado (pre-instalado en Windows 10/11)
- Si no está instalado, se muestra advertencia clara
- Si falla la exportación, se conserva el HTML generado

**Decisión técnica:**
- Método: Microsoft Edge en modo headless (`--print-to-pdf`)
- Dependencia: Microsoft Edge (pre-instalado en Win10/11)
- Ventajas: PDF real con cabecera `%PDF`, renderizado Chromium completo
- Validación: Se verifica cabecera `%PDF` del archivo generado

---

## Estructura del proyecto

```
CATTECH-OPTIMIZER-PRO/
├── src/                                   # Código fuente
│   ├── Cattech.Optimizer.Pro.Core/        # Modelos, interfaces y reglas de dominio
│   ├── Cattech.Optimizer.Pro.Infrastructure/ # WMI, SMART, hardware, reportes, reparaciones
│   └── Cattech.Optimizer.Pro.UI/          # Interfaz WPF (MVVM)
├── tests/                                 # Tests xUnit (Core + UI + smoke XAML)
├── config/                                # Configuración y catálogos (herramientas, bugchecks)
├── scripts/qa/                            # Scripts de QA y verificación de workflows
├── docs/                                  # Documentación técnica, QA y release notes
└── .github/workflows/                     # CI, packaging y publicación de releases
```

Para más detalles, ver [ARQUITECTURA_INICIAL.md](ARQUITECTURA_INICIAL.md).

---

## Documentación

- [Release notes v0.3.1](docs/RELEASE_NOTES_V0_3_1.md) - Cambios, certificación y descarga
- [Automatización de releases](docs/RELEASE_AUTOMATION.md) - CI, packaging y publicación controlada
- [QA real en Windows](docs/QA_REAL_SMOKE.md) - Protocolo de verificación en equipos reales
- [Seguridad](SEGURIDAD.md) - Reglas de seguridad obligatorias
- [Roadmap](ROADMAP.md) - Estado y plan de desarrollo por versiones
- [Arquitectura](ARQUITECTURA_INICIAL.md) - Estructura, capas y tecnologías
- [Auditoría de referencias](AUDITORIA_REFERENCIAS.md) - Herramientas analizadas y licencias
- [Changelog](CHANGELOG.md) - Historial completo de cambios

---

## Desarrollo

### Iniciar desarrollo

```bash
# Clonar repositorio
git clone https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO.git
cd CATTECH-OPTIMIZER-PRO

# Abrir en Visual Studio
start src\Cattech.Optimizer.Pro.sln

# O compilar desde línea de comandos
dotnet build
```

### Ejecutar tests

```bash
dotnet test
```

### Crear build de distribución

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## Contribuir

1. Leer [SEGURIDAD.md](SEGURIDAD.md) antes de cualquier cambio
2. Crear un issue-discusión para cambios grandes
3. Seguir las convenciones de código existentes
4. Agregar tests para código nuevo
5. Actualizar CHANGELOG.md

---

## Licencia

Este proyecto está bajo la licencia MIT. Ver [LICENSE](LICENSE) para más detalles.

### Dependencias y sus licencias

| Dependencia | Licencia |
|-------------|----------|
| .NET 10 | MIT |
| CommunityToolkit.Mvvm | MIT |
| Newtonsoft.Json | MIT |
| LibreHardwareMonitorLib | MPL 2.0 |
| System.Management | MIT |
| Microsoft Edge | (pre-instalado, para exportación PDF) |
| smartmontools / smartctl | GPLv2 (dependencia externa opcional, NO incluida en el ZIP) |

Ver [AUDITORIA_REFERENCIAS.md](AUDITORIA_REFERENCIAS.md) para detalles completos.

---

## Contacto

- **Website**: [cattech.com](https://cattech.com) (futuro)
- **Email**: info@cattech.com (futuro)
- **Issues**: [GitHub Issues](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/issues)

---

## Agradecimientos

- [ChrisTitusTech/winutil](https://github.com/ChrisTitusTech/winutil) - Inspiración para organización de módulos
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) - Monitoreo de hardware
- [smartmontools](https://github.com/smartmontools/smartmontools) - Diagnóstico SMART
- Microsoft Edge - Exportación HTML a PDF

## Automatización de releases

Ver [docs/RELEASE_AUTOMATION.md](docs/RELEASE_AUTOMATION.md) para el CI (CATTECH CI) y el workflow reutilizable de release packaging (CATTECH Release Package).

## QA real en Windows

Ver [docs/QA_REAL_SMOKE.md](docs/QA_REAL_SMOKE.md) para el protocolo de smoke testing en equipos reales (P.3).

---

*Desarrollado con ❤️ para técnicos informáticos*
