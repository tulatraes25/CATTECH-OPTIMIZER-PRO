# CATTECH OPTIMIZER PRO

**Versión**: 0.1 (MVP en planificación)  
**Licencia**: MIT  
**Plataforma**: Windows 10 (1809+) / Windows 11

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
3. Ejecutar `CATTECH.Optimizer.Pro.exe` como administrador

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
| QuestPDF | MIT |
| Serilog | Apache 2.0 |
| LibreHardwareMonitorLib | MPL 2.0 |

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
- [QuestPDF](https://github.com/SimplyBlues/QuestPDF) - Generación de PDF

---

*Desarrollado con ❤️ para técnicos informáticos*
