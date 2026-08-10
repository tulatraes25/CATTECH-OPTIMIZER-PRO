# CATTECH OPTIMIZER PRO

[![CATTECH CI](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/actions/workflows/ci.yml/badge.svg)](https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO/actions/workflows/ci.yml)

**Versión**: v0.2.0  
**Licencia**: MIT  
**Plataforma**: Windows 10 (1809+) / Windows 11

> Historial publicado: [v0.1.1 MVP estabilizado](CHANGELOG.md#011---2026).

---

## Descripción

CATTECH OPTIMIZER PRO es una aplicación de escritorio para Windows diseñada para técnicos informáticos. Ofrece herramientas de diagnóstico, optimización y mantenimiento de equipos, generando informes profesionales en PDF con datos de la empresa y técnico.

### Características principales (MVP v0.1)

- **Configuración personalizable**: Logo de empresa, datos del técnico
- **Gestión de clientes**: Registro de clientes y equipos
- **Diagnóstico básico**: Hardware, discos, estado del sistema
- **Optimización segura**: Limpieza de temporales, ajustes visuales
- **Puntos de restauración**: Creación antes de cambios
- **Informes profesionales**: HTML y PDF con logo personalizado

### Novedades v0.2.0 (SMART + Hardware avanzado)

- **SMART completo**: análisis read-only por disco, estados Bueno/Precaución/Crítico/No disponible/Desconocido, self-tests corto y extendido con persistencia, sección SMART en informes HTML/PDF, recomendaciones automáticas, exit status de smartctl interpretado como bitmask, transporte `-d TYPE` preservado
- **Hardware en tiempo real**: temperaturas, Load/Clock de CPU/GPU, memoria GPU, batería y timings SPD con una sola sesión LibreHardwareMonitor; inventario estático WMI/SMBIOS (CPU, GPU, RAM, módulos, placa madre/BIOS); sin thresholds de salud inventados
- **Dependencia externa smartctl**: smartmontools/smartctl se detecta desde ruta configurada (`config/herramientas.json`), instalación estándar, rutas junto a la app o PATH. No se distribuye dentro del paquete en v0.2.0.

---

## Requisitos del sistema

### Para ejecutar

- **Windows 10 (1809+) / Windows 11** (requerido para .NET 8)
- .NET 8 Desktop Runtime
- 512 MB RAM mínimo (1 GB recomendado)
- 100 MB de espacio en disco
- Permisos de administrador (para diagnóstico completo)

> **Nota**: Windows 7/8/8.1 no son soportados por .NET 8. Para esos sistemas se requeriría una rama legacy con .NET Framework 4.8 (no planificado en MVP).

### Para desarrollar

- Visual Studio 2022 17.8+ o JetBrains Rider 2023.3+
- .NET 8 SDK
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

### Versión portable (recomendada para v0.1)

1. Descargar la última versión desde Releases
2. Extraer el ZIP en una carpeta
3. Ejecutar `Cattech.Optimizer.Pro.UI.exe` como administrador

### Versión con instalador (disponible en v1.0)

1. Descargar el instalador desde Releases
2. Ejecutar el instalador
3. Seguir las instrucciones en pantalla

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
├── src/                          # Código fuente
│   ├── Cattech.Optimizer.Pro.Core/        # Lógica de negocio
│   ├── Cattech.Optimizer.Pro.Infrastructure/ # Integración
│   └── Cattech.Optimizer.Pro.UI/          # Interfaz WPF
├── tools/                        # Herramientas externas
├── docs/                         # Documentación
├── config/                       # Configuración
├── templates/                    # Plantillas de reportes
└── tests/                        # Tests unitarios
```

Para más detalles, ver [ARQUITECTURA_INICIAL.md](ARQUITECTURA_INICIAL.md)

---

## Documentación

- [Auditoría de Referencias](AUDITORIA_REFERENCIAS.md) - Análisis de herramientas existentes
- [Arquitectura Inicial](ARQUITECTURA_INICIAL.md) - Estructura y tecnologías
- [Seguridad](SEGURIDAD.md) - Reglas de seguridad obligatorias
- [Roadmap](ROADMAP.md) - Plan de desarrollo por versiones
- [Changelog](CHANGELOG.md) - Historial de cambios

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
| .NET 8 | MIT |
| CommunityToolkit.Mvvm | MIT |
| Serilog | Apache 2.0 |
| LibreHardwareMonitorLib | MPL 2.0 |
| Microsoft Edge | (pre-instalado, para exportación PDF) |

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

---

*Desarrollado con ❤️ para técnicos informáticos*
